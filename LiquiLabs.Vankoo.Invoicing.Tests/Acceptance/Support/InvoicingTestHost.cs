using FluentValidation;
using LiquiLabs.Vankoo.Invoicing.Application.Behaviors;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.UploadInvoice;
using LiquiLabs.Vankoo.Invoicing.Application.Interfaces;
using LiquiLabs.Vankoo.Invoicing.Application.Internal.Files;
using LiquiLabs.Vankoo.Invoicing.Application.Internal.Ocr;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Acceptance.Support;

/// <summary>
/// Arma el mismo pipeline de MediatR que Program.cs (handlers, ValidationBehavior y
/// validadores), con los adaptadores de infraestructura reemplazados por dobles en memoria.
/// Reqnroll crea una instancia por escenario, así que cada escenario parte de cero.
/// </summary>
public sealed class InvoicingTestHost : IDisposable
{
    private readonly ServiceProvider _provider;

    public InMemoryInvoiceRepository Invoices { get; } = new();
    public InMemoryStorageService Storage { get; } = new();
    public StubOcrService Ocr { get; } = new();
    public RecordingEventBus EventBus { get; } = new();
    public InMemoryOcrTaskRepository OcrTasks { get; } = new();

    public InvoicingTestHost()
    {
        var applicationAssembly = typeof(UploadInvoiceCommand).Assembly;
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(applicationAssembly);
            config.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(applicationAssembly);

        services.AddSingleton<IInvoiceRepository>(Invoices);
        services.AddSingleton<IStorageService>(Storage);
        services.AddSingleton<IOcrService>(Ocr);
        services.AddSingleton<IEventBus>(EventBus);
        services.AddSingleton<IOcrTaskRepository>(OcrTasks);
        services.AddSingleton<InvoiceFileInspector>();
        services.AddSingleton<InvoiceLineItemResolver>();
        services.AddSingleton<InvoiceConsistencyValidator>();
        services.AddScoped<OcrResultProcessor>();

        _provider = services.BuildServiceProvider();
    }

    public InvoiceLineItemResolver LineItemResolver => _provider.GetRequiredService<InvoiceLineItemResolver>();

    public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        await using var scope = _provider.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(request);
    }

    public void Dispose() => _provider.Dispose();
}
