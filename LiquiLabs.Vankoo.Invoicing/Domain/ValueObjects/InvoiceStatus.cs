namespace LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

public enum InvoiceStatus
{
    /// <summary>PDF subido a S3, sin procesar aún</summary>
    UPLOADED = 1,

    /// <summary>Enviado a Azure Form Recognizer, esperando resultado</summary>
    OCR_PROCESSING = 2,

    /// <summary>OCR completado, datos extraídos y validados</summary>
    DATA_EXTRACTED = 3,

    /// <summary>Consultando SUNAT para verificar legitimidad</summary>
    SUNAT_VALIDATING = 4,

    /// <summary>SUNAT confirmó que la factura es válida</summary>
    SUNAT_VALIDATED = 5,

    /// <summary>Aprobada para subastarse en el marketplace</summary>
    APPROVED = 6,

    /// <summary>Publicada y visible en el marketplace</summary> Creo q no???
    PUBLISHED = 7,

    /// <summary>Datos extraídos y consistentes; lista para evaluación de riesgo</summary>
    CONSISTENCY_PASSED = 8,

    /// <summary>Hay baja confianza o ambigüedades que impiden avanzar automáticamente</summary>
    REQUIRES_REVIEW = 9,

    /// <summary>La factura es legible pero no es elegible para financiamiento</summary>
    NOT_ELIGIBLE = 10,

    /// <summary>Rechazada en cualquier paso del proceso</summary>
    REJECTED = 99
}
