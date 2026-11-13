using FluentValidation;
using Amazon;
using LiquiLabs.Vankoo.Invoicing.Application.Behaviors;
using LiquiLabs.Vankoo.Invoicing.Application.Interfaces;
using LiquiLabs.Vankoo.Invoicing.Application.Internal.Files;
using LiquiLabs.Vankoo.Invoicing.Application.Internal.Ocr;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.Services;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;
using Amazon.Runtime;
using Amazon.S3;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.ExternalServices.Ocr.Azure;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.ExternalServices.Ocr.Azure.Mappers;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.HealthChecks;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Persistence.MongoDB.Contexts;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Persistence.MongoDB.Repositories;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Storage;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Workers;
using LiquiLabs.Vankoo.Invoicing.Shared.Infrastructure.Web;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Scalar.AspNetCore;
using Steeltoe.Discovery.Eureka;

var builder = WebApplication.CreateBuilder(args);

// 1. CONFIGURACIÓN DE SERIALIZACIÓN (UUID v7 y Decimales)
// Esto asegura que los Guids se guarden como UUIDs estándar legibles en Mongo
BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

// 2. CARGA DE CONFIGURACIONES (IOptions Pattern)
builder.Services.Configure<DbSettings>(builder.Configuration.GetSection("DbSettings"));
builder.Services.Configure<TokenSettings>(builder.Configuration.GetSection("TokenSettings"));
builder.Services.Configure<AzureOcrSettings>(builder.Configuration.GetSection("AzureOcrSettings"));
builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection("KafkaSettings"));
builder.Services.Configure<OcrWorkerSettings>(builder.Configuration.GetSection("OcrWorkerSettings"));
builder.Services.Configure<MinioSettings>(builder.Configuration.GetSection("MinioSettings"));
builder.Services.Configure<AwsS3Settings>(builder.Configuration.GetSection("AwsS3Settings"));
builder.Services.Configure<StorageProviderSettings>(builder.Configuration.GetSection("StorageProviderSettings"));

// Límite de tamaño de archivo: el framework rechaza requests que superen esto antes de llegar al dominio
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10MB
});

// 3. CLIENTE S3
builder.Services.AddSingleton<ObjectStorageRuntimeSettings>(sp =>
    ObjectStorageSettingsFactory.Create(
        sp.GetRequiredService<IOptions<StorageProviderSettings>>().Value,
        sp.GetRequiredService<IOptions<MinioSettings>>().Value,
        sp.GetRequiredService<IOptions<AwsS3Settings>>().Value));

builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var settings = sp.GetRequiredService<ObjectStorageRuntimeSettings>();
    var credentials = new BasicAWSCredentials(settings.AccessKey, settings.SecretKey);
    var config = settings.Provider switch
    {
        StorageProvider.Minio => new AmazonS3Config
        {
            ServiceURL = $"{(settings.UseSsl ? "https" : "http")}://{settings.Endpoint}",
            ForcePathStyle = true
        },
        StorageProvider.AwsS3 => new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(settings.Region)
        },
        _ => throw new InvalidOperationException($"Unsupported storage provider '{settings.Provider}'.")
    };
    return new AmazonS3Client(credentials, config);
});
builder.Services.AddScoped<IStorageService, S3StorageService>();

// 4. HEALTH CHECKS
// MongoDb 9.x resuelve MongoClient desde DI — registramos el singleton aquí.
// MongoContext crea su propio cliente internamente; este singleton es exclusivo para el health check.
builder.Services.AddSingleton<MongoClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<DbSettings>>().Value;
    return new MongoClient(settings.ConnectionString);
});
builder.Services.AddHealthChecks()
    // MongoDB: crítico — sin base de datos el servicio no puede operar
    .AddMongoDb(
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"])
    // Kafka:    degradado — la factura se persiste igual, solo falla la publicación del evento
    .AddKafka(
        setup =>
        {
            setup.BootstrapServers = builder.Configuration["KafkaSettings:BootstrapServers"];
        },
        failureStatus: HealthStatus.Degraded,
        tags: ["ready"])
    // Object storage: degradado — las subidas fallarían pero el servicio sigue en pie
    .AddCheck<S3HealthCheck>(
        name: "object-storage",
        failureStatus: HealthStatus.Degraded,
        tags: ["ready"]);

// 5. SERVICE DISCOVERY (Eureka)
// Steeltoe 4.x: AddEurekaDiscoveryClient() en IServiceCollection (no IHostApplicationBuilder)
builder.Services.AddEurekaDiscoveryClient();

// 6. AGREGAR SERVICIOS DE LA APLICACIÓN
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// 7. MediatR + ValidationBehavior pipeline
builder.Services.AddMediatR(config =>
{
    config.RegisterServicesFromAssembly(typeof(Program).Assembly);
    config.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

// 8. REPOSITORIOS, SERVICIOS DE DOMINIO E INFRAESTRUCTURA
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<IOcrTaskRepository, OcrTaskRepository>();
builder.Services.AddScoped<IOcrService, AzureOcrService>();
builder.Services.AddSingleton<InvoiceFileInspector>();
builder.Services.AddSingleton<InvoiceLineItemResolver>();
builder.Services.AddSingleton<InvoiceConsistencyValidator>();
builder.Services.AddScoped<OcrResultProcessor>();
builder.Services.AddSingleton<AzureOcrMapper>();
builder.Services.AddSingleton<MongoContext>();
builder.Services.AddSingleton<IEventBus, KafkaEventBus>();
builder.Services.AddHostedService<OcrTaskWorker>();

var app = builder.Build();

// 5. CONFIGURAR EL PIPELINE HTTP
if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Invoicing Service API")
            .WithTheme(ScalarTheme.Moon)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.UseExceptionHandler();

// Solo redirigir a HTTPS en desarrollo local (en Docker se usa solo HTTP en el puerto 8080)
if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

// 10. HEALTH CHECK ENDPOINTS
// Liveness: solo verifica que el proceso está vivo, sin checks externos.
// Si falla, Docker reinicia el contenedor.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

// Readiness: verifica que todas las dependencias críticas están disponibles.
// Si falla (503), el servicio no recibe tráfico hasta que se recupere.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResultStatusCodes =
    {
        [HealthStatus.Healthy]   = StatusCodes.Status200OK,
        [HealthStatus.Degraded]  = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

// 11. MAPEO DE CONTROLADORES
app.MapControllers();

app.Run();
