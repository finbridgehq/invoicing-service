# CONVENTIONS.md — Convenciones de código

> Reglas de estilo, nombres y estructura. Para decisiones de arquitectura (capas, CQRS, eventos)
> ver [ARCHITECTURE.md](ARCHITECTURE.md).

---

## Nomenclatura

- Clases en PascalCase, métodos en PascalCase, variables locales en camelCase.
- Commands: `[Acción][Recurso]Command` — ej: `UploadInvoiceCommand`
- Handlers: `[Acción][Recurso]Handler` — ej: `UploadInvoiceCommandHandler`
- Queries: `[Acción][Recurso]Query` — ej: `DownloadInvoiceFileQuery`
- Validators: `[Nombre del Command/Query]Validator`
- Integration events: `[Hecho]IntegrationEvent` — ej: `InvoiceOcrProcessedIntegrationEvent`
- Domain events: `[Hecho]Event` o `[Hecho]DomainEvent` — ej: `InvoiceCreatedEvent`, `InvoiceOcrProcessedDomainEvent`
- Términos del dominio en español (`MypeId`, `RucNumber`, etc.), código en inglés.

## Idioma

Los nombres de conceptos del dominio (mype, RUC, factura) van en español porque así los conoce el negocio.
El resto del código (métodos, comentarios técnicos, nombres de infraestructura) va en inglés.

## Simplicidad

- No crear helpers ni abstracciones genéricas sin un caso de uso concreto que lo justifique. Prefiere repetir 2-3 líneas antes que introducir una abstracción prematura.
- No agregues manejo de errores para escenarios que no pueden ocurrir. Valida en los bordes (`Validator`, controlador), confía en las invariantes del dominio hacia adentro.
- Cambios de configuración van siempre por el patrón `IOptions<T>` (ver `docs/ARCHITECTURE.md`), nunca leyendo `IConfiguration` a mano dentro de un handler.

## Comentarios

Sin comentarios que describan qué hace el código (los nombres ya lo dicen). Solo cuando hay una razón no obvia:
una limitación de una librería, un workaround temporal, una invariante que sorprendería a quien lea el código.
Ejemplo real en este repo (`KafkaEventBus.cs`): el comentario sobre creación dinámica de tópicos documenta una
decisión cuestionable a propósito, no una explicación de sintaxis.
