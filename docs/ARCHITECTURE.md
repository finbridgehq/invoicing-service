# ARCHITECTURE.md — Arquitectura de Invoicing

> Qué significa "hacer un buen trabajo" en este proyecto a nivel estructural.
> Para nombres, estilo y convenciones de código ver [CONVENTIONS.md](CONVENTIONS.md).

---

## Arquitectura general

Clean Architecture + DDD + CQRS. Las capas en orden de dependencia (de adentro hacia afuera):

```
Domain → Application → Infrastructure
                    ↗
             Interfaces (REST)
```

- **Domain** no depende de nada externo. Sin NuGet, sin frameworks.
- **Application** depende solo de Domain y de sus propias interfaces (contratos).
- **Infrastructure** implementa los contratos de Application y Domain.
- **Interfaces/Rest** traduce HTTP → comandos/queries de Application.
- **Shared** contiene código transversal (excepciones base, handler global de errores). No tiene lógica de negocio.

**Regla crítica:** nunca importar Infrastructure desde Domain o Application. Si Application necesita algo externo, define una interfaz en `Application/Interfaces/` y deja que Infrastructure la implemente.

**Qué no hacer (capas):**
- No poner lógica de negocio en los controladores. El controlador solo traduce HTTP → MediatR.
- No inyectar repositorios directamente en controladores. Todo pasa por Application.
- No acceder a la base de datos desde Domain.

---

## CQRS con MediatR

Cada caso de uso vive en su propia carpeta dentro de `Application/Commands/` o `Application/Queries/`. Estructura de una carpeta de caso de uso:

```
UploadInvoice/
├── UploadInvoiceCommand.cs        # IRequest<TResponse> — datos de entrada
├── UploadInvoiceCommandHandler.cs # IRequestHandler<TCommand, TResponse>
└── UploadInvoiceCommandValidator.cs # AbstractValidator<TCommand> (FluentValidation)
```

El `ValidationBehavior<TRequest, TResponse>` en `Application/Behaviors/` intercepta automáticamente todos los requests de MediatR y ejecuta los validadores registrados. No hace falta invocar la validación manualmente.

Para agregar un nuevo caso de uso:
1. Crear carpeta en `Commands/` o `Queries/`
2. Definir el Command/Query, Handler y Validator
3. El controlador llama a `mediator.Send(command)` — nada más

### Eventos: dominio vs integración

Dos tipos de eventos, no los confundas:

- **Domain events** (`Domain/Events/`, ej. `InvoiceCreatedEvent`, `InvoiceOcrProcessedDomainEvent`) — se publican con `IMediator.Publish(...)` desde los command handlers y se manejan dentro del propio servicio vía `INotificationHandler<T>` en `Application/EventHandlers/`. Sirven para encadenar lógica interna (ej. `InvoiceCreatedEventHandler` dispara el OCR).
- **Integration events** (`Application/IntegrationEvents/`, ej. `InvoiceOcrProcessedIntegrationEvent`) — se publican hacia Kafka con `IEventBus.PublishAsync(...)` (implementado por `KafkaEventBus` en `Infrastructure/Brokers/Kafka/`). Sirven para avisar a *otros* microservicios. El topic se resuelve automáticamente a partir del nombre de la clase (`invoicing.<kebab-case-sin-sufijo-integrationevent>`).

El flujo típico es: domain event → event handler en Application → construye un integration event → `IEventBus.PublishAsync`.

---

## Dominio

El agregado principal es `Invoice` (`Domain/Aggregates/Invoice.cs`). Encapsula toda la lógica de negocio de una factura.

Value objects relevantes:
- `InvoiceId` — wrapper de Guid para evitar primitive obsession
- `MypeId` — identidad de la MYPE propietaria de la factura
- `RucNumber` — RUC peruano con validación de formato
- `FileKey` — referencia al archivo en MinIO (no es una URL, es una clave de objeto S3)
- `Money` + `Currency` — importe con divisa
- `OcrTaskStatus` — enum del estado de una tarea OCR (no modificar sin revisar la máquina de estados)

`OcrTask` es una entidad (tiene identidad propia) que vive en `Domain/Entities/`. Representa una tarea de procesamiento OCR con estado y reintentos.

Repositorios: interfaz en `Domain/Repositories/`, implementación en `Infrastructure/Persistence/MongoDB/Repositories/`.

---

## Configuración (IOptions Pattern)

Todas las configuraciones tienen su clase tipada en `Infrastructure/Configuration/Settings/`. Para agregar una nueva sección de configuración:
1. Crear la clase en `Infrastructure/Configuration/Settings/`
2. Registrarla en `Program.cs` con `builder.Services.Configure<MiSettings>(builder.Configuration.GetSection("MiSettings"))`
3. Inyectarla con `IOptions<MiSettings>` o `IOptionsMonitor<MiSettings>`

La jerarquía de appsettings es: `appsettings.json` → `appsettings.{Environment}.json` → variables de entorno del compose. Las variables de entorno sobreescriben con el formato `Section__Key` (doble guion bajo).

`appsettings.Docker.json` está en disco pero **no está trackeado por git** (es un archivo nuevo no confirmado). Contiene los hostnames internos de Docker (`invoicing-db-mongo`, `kafka-broker`, `minio`). No moverlo ni renombrarlo, y no agregarlo al `.gitignore` si no lo está — verificar antes de hacer commit.

### Almacenamiento de objetos

Application usa únicamente `IStorageService`; `S3StorageService` es el adapter de Infrastructure para
proveedores que exponen la API S3. El proveedor activo se elige con
`StorageProviderSettings:Provider`: `Minio` para desarrollo o `AwsS3` para AWS.

- MinIO usa `MinioSettings`, endpoint propio, path-style y puede crear el bucket si falta.
- AWS S3 usa `AwsS3Settings` (región, bucket y credenciales), no fuerza path-style y su bucket se
  aprovisiona externamente mediante IaC.
- Las credenciales se inyectan por configuración de entorno; nunca se agregan al repositorio.

Para AWS S3 se definen `StorageProviderSettings__Provider=AwsS3`,
`AwsS3Settings__Region`, `AwsS3Settings__BucketName`, `AwsS3Settings__AccessKey` y
`AwsS3Settings__SecretKey`. La identidad configurada necesita `s3:GetBucketLocation` sobre el
bucket y `s3:PutObject`, `s3:GetObject` y `s3:DeleteObject` sobre sus objetos.

Azure Blob Storage requeriría un adapter `AzureBlobStorageService` que implemente el mismo puerto y
mapee el bucket a un container. No se agregan capacidades específicas de proveedor al puerto hasta
que exista un caso de uso que las necesite.

---

## Workers en background

`OcrTaskWorker` (`Infrastructure/Workers/`) es un `BackgroundService` que hace polling a MongoDB buscando tareas OCR pendientes. Su configuración está en `OcrWorkerSettings`. No bloquea el pipeline HTTP.

---

## Infraestructura Docker

- El servicio corre en el puerto **8080 dentro del contenedor**, expuesto en el host en el **8082** por defecto.
- `ASPNETCORE_ENVIRONMENT=Docker` activa `appsettings.Docker.json`.
- El Dockerfile (`LiquiLabs.Vankoo.Invoicing/Dockerfile`) es multi-stage: `base` → `build` → `publish` → `final`.
- `curl` se instala en el stage final exclusivamente para los health checks del compose.
- Este repo **no tiene compose propio**: la orquestación (Mongo, Kafka, MinIO, el propio servicio) vive en el repo `vankoo/infrastructure`. La reconstrucción se hace con `docker compose up -d --build invoicing-service` desde ahí.

---

## Scalar / OpenAPI

Scalar y el esquema OpenAPI solo se registran cuando `!app.Environment.IsProduction()`. En local (`Development`) y en Docker (`Docker`) están disponibles en `/scalar`.
