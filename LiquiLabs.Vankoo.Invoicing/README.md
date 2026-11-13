# Invoicing Service — LiquiLabs.Vankoo.Invoicing

Microservicio de gestión de facturas dentro del ecosistema **Vankoo**. Permite a las MYPEs subir sus facturas, procesarlas con OCR (Azure AI Document Intelligence) y publicar eventos de integración hacia otros servicios vía Kafka.

---

## Para qué sirve

- Recibir y almacenar facturas en formato PDF/imagen (MinIO)
- Extraer datos estructurados mediante OCR asíncrono (Azure AI)
- Persistir facturas y tareas OCR en MongoDB
- Publicar eventos de integración (Kafka) al completarse el procesamiento
- Registrarse en Eureka para ser descubierto por el API Gateway

---

## Stack tecnológico

| Tecnología | Rol |
|---|---|
| .NET 10 / ASP.NET Core | Framework principal |
| MongoDB | Base de datos de facturas y tareas OCR |
| MinIO (S3-compatible) | Almacenamiento de archivos |
| Apache Kafka | Bus de eventos de integración |
| Azure AI Document Intelligence | Extracción OCR de facturas |
| Steeltoe Eureka | Service discovery |

---

## Requisitos previos

### Para correr en local

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- MongoDB corriendo en `localhost:27017`
- MinIO corriendo en `localhost:9100` (API S3)
- Kafka corriendo en `localhost:9092`
- Cuenta de Azure con el servicio **Azure AI Document Intelligence** creado

### Para correr con Docker

- [Docker Desktop](https://www.docker.com/products/docker-desktop) (o Docker Engine en Linux)
- Acceso al repositorio de infraestructura `vankoo/infrastructure`
- Variables de entorno configuradas (ver sección Docker)

---

## Configuración

El proyecto usa la jerarquía estándar de ASP.NET Core:

| Archivo | Entorno | Propósito |
|---|---|---|
| `appsettings.json` | Todos | Valores base (sin secretos, sin conexiones) |
| `appsettings.Development.json` | `Development` | Configuración local con valores de desarrollo |
| `appsettings.Docker.json` | `Docker` | Configuración para contenedor (hostnames internos de Docker) |

**Nunca** subir secretos a git. Las claves sensibles se inyectan como variables de entorno en Docker o se configuran localmente en `appsettings.Development.json` (ignorado por git si se configura así).

Secciones de configuración:

```json
{
  "DbSettings": { "ConnectionString": "", "DatabaseName": "" },
  "KafkaSettings": { "BootstrapServers": "", "ClientId": "", ... },
  "MinioSettings": { "Endpoint": "", "AccessKey": "", "SecretKey": "", ... },
  "AzureOcrSettings": { "Endpoint": "", "ApiKey": "", ... },a
  "OcrWorkerSettings": { "PollIntervalSeconds": 2, "LeaseDurationSeconds": 120, ... },
  "TokenSettings": { "Secret": "" },
  "Eureka": { ... }
}
```

---

## Cómo correr en local

1. Asegúrate de tener MongoDB, MinIO y Kafka corriendo localmente. Si tienes Docker puedes levantarlos individualmente o con un compose mínimo desde el repo de infraestructura.

2. Configura `appsettings.Development.json` con tus valores locales (ya existe una plantilla en el repositorio).

3. Ejecuta el proyecto:

```bash
dotnet run --project LiquiLabs.Vankoo.Invoicing
```

4. Accede a la documentación de la API en:

```
https://localhost:7XXX/scalar
```

El puerto exacto se define en `Properties/launchSettings.json`.

---

## Cómo levantar con Docker

El servicio forma parte del `docker-compose.yaml` del repositorio de infraestructura (`vankoo/infrastructure`). No tiene un compose propio.

### Variables de entorno requeridas

Crea un archivo `.env` en el directorio de infraestructura con al menos:

```env
JWT_SECRET=tu_secreto_jwt
AZURE_OCR_API_KEY=tu_api_key_de_azure
AZURE_OCR_ENDPOINT=https://tu-recurso.cognitiveservices.azure.com/
```

El resto de variables tienen valores por defecto definidos en el compose.

### Levantar toda la infraestructura

```bash
cd vankoo/infrastructure
docker compose up -d
```

### Reconstruir solo el servicio de invoicing (tras cambios de código)

```bash
docker compose up -d --build invoicing-service
```

### Verificar que el servicio está vivo

```bash
# Health check de liveness
curl http://localhost:8082/health/live

# Health check de readiness (MongoDB + Kafka + MinIO)
curl http://localhost:8082/health/ready
```

El servicio queda expuesto en el host en el puerto `8082` por defecto (configurable con `INVOICING_SERVICE_PORT`).

---

## Endpoints de la API

Base path: `http://localhost:8082/api/v1`

| Método | Ruta | Descripción |
|---|---|---|
| `POST` | `/invoices` | Sube una factura (multipart/form-data) |
| `GET` | `/invoices/{id}/file` | Descarga el archivo de una factura |
| `POST` | `/invoices/{id}/ocr/sync` | Procesa OCR de forma síncrona |

La documentación interactiva (Scalar) está disponible en entornos no productivos:

```
http://localhost:8082/scalar
```

---

## Health Checks

| Endpoint | Propósito | Fallo |
|---|---|---|
| `/health/live` | El proceso está vivo | Docker reinicia el contenedor |
| `/health/ready` | Dependencias disponibles | El servicio deja de recibir tráfico |

Dependencias verificadas en `/health/ready`:

| Dependencia | Impacto si falla |
|---|---|
| MongoDB | `Unhealthy` — el servicio no puede operar |
| Kafka | `Degraded` — la factura se persiste, falla la publicación del evento |
| MinIO | `Degraded` — las subidas fallan pero el proceso sigue vivo |

---

## Estructura de carpetas

```
LiquiLabs.Vankoo.Invoicing/
├── Application/                  # Casos de uso (CQRS con MediatR)
│   ├── Behaviors/                # Pipeline: validación automática con FluentValidation
│   ├── Commands/                 # Comandos que modifican estado
│   │   ├── OcrProcessing/        # Iniciar, continuar y consultar procesamiento OCR
│   │   └── UploadInvoice/        # Subida de facturas
│   ├── EventHandlers/            # Manejadores de eventos de dominio
│   ├── IntegrationEvents/        # Eventos publicados hacia otros microservicios
│   ├── Interfaces/               # Contratos que la capa de aplicación necesita
│   └── Queries/                  # Consultas que no modifican estado
│       └── DownloadInvoiceFile/
├── Domain/                       # Núcleo del negocio, sin dependencias externas
│   ├── Aggregates/               # Invoice — raíz de agregado principal
│   ├── Entities/                 # OcrTask — entidad con identidad propia
│   ├── Events/                   # Eventos de dominio
│   ├── Exceptions/               # Excepciones de negocio
│   ├── Repositories/             # Interfaces (contratos, sin implementación)
│   └── ValueObjects/             # Tipos inmutables (InvoiceId, RucNumber, Money, etc.)
├── Infrastructure/               # Implementaciones técnicas
│   ├── Brokers/Kafka/            # Implementación de IEventBus con Confluent.Kafka
│   ├── Configuration/Settings/   # Clases tipadas para IOptions<T>
│   ├── ExternalServices/Ocr/     # Integración con Azure AI Document Intelligence
│   ├── HealthChecks/             # Health check personalizado para MinIO
│   ├── Persistence/MongoDB/      # MongoContext + repositorios
│   ├── Storage/                  # Implementación de IStorageService con MinIO
│   └── Workers/                  # OcrTaskWorker: background service de procesamiento
├── Interfaces/                   # Capa de entrada (REST)
│   └── Rest/
│       ├── Controllers/          # InvoicesController
│       └── Dto/                  # Recursos de entrada y salida de la API
├── Shared/                       # Código transversal reutilizable
│   ├── Domain/                   # Excepciones base y códigos de error
│   └── Infrastructure/Web/       # GlobalExceptionHandler, ErrorResponse
├── Properties/
│   └── launchSettings.json
├── Program.cs                    # Composición de la aplicación (DI + pipeline HTTP)
├── Dockerfile
├── appsettings.json
├── appsettings.Development.json
└── appsettings.Docker.json
```

---

## Dependencias NuGet

| Paquete | Versión | Propósito |
|---|---|---|
| `MongoDB.Driver` | 3.6.0 | Acceso a MongoDB |
| `AWSSDK.S3` | 4.0.18.6 | Cliente S3 para MinIO |
| `Confluent.Kafka` | 2.13.1 | Productor/consumidor Kafka |
| `Azure.AI.FormRecognizer` | 4.1.0 | OCR con Azure AI Document Intelligence |
| `MediatR` | 14.0.0 | Implementación del patrón Mediator (CQRS) |
| `FluentValidation.AspNetCore` | 11.3.1 | Validación de comandos y queries |
| `Steeltoe.Discovery.Eureka` | 4.1.0 | Registro y descubrimiento de servicios |
| `Scalar.AspNetCore` | 2.12.40 | UI de documentación OpenAPI |
| `Microsoft.AspNetCore.OpenApi` | 10.0.3 | Generación del esquema OpenAPI |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.3 | Validación de JWT |
| `AspNetCore.HealthChecks.MongoDb` | 9.0.0 | Health check de MongoDB |
| `AspNetCore.HealthChecks.Kafka` | 9.0.0 | Health check de Kafka |
| `Humanizer` | 3.0.1 | Formateo legible de datos |
