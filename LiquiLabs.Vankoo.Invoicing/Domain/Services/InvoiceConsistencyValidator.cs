using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

namespace LiquiLabs.Vankoo.Invoicing.Domain.Services;

public sealed class InvoiceConsistencyValidator
{
    private const decimal MoneyTolerance = 0.02m;
    private const float CriticalConfidenceThreshold = 0.75f;

    public InvoiceConsistencyResult Validate(
        OcrExtractionResult extraction,
        DateTime checkedAt,
        bool duplicateFiscalIdentity = false)
    {
        ArgumentNullException.ThrowIfNull(extraction);
        var issues = new List<InvoiceValidationIssue>();

        if (extraction.IssuerData.Ruc == extraction.PayerData.Ruc)
            issues.Add(Error("ISSUER_EQUALS_PAYER", "El emisor y el pagador no pueden tener el mismo RUC."));

        if (extraction.Metadata.IssueDate.Date > checkedAt.Date)
            issues.Add(Review("FUTURE_ISSUE_DATE", "La fecha de emisión está en el futuro y debe revisarse."));

        if (extraction.Metadata.IsExpired(checkedAt))
        {
            issues.Add(Error(
                "INVOICE_EXPIRED",
                $"La factura venció el {extraction.Metadata.DueDate:yyyy-MM-dd} y no es elegible para factoring."));
        }

        if (!extraction.Amounts.IsConsistent(MoneyTolerance))
        {
            issues.Add(Review(
                "TOTALS_DO_NOT_RECONCILE",
                "Subtotal, descuentos, impuestos y total no cuadran dentro de la tolerancia permitida."));
        }

        if (!extraction.Items.Any())
            issues.Add(Review("NO_LINE_ITEMS", "Azure no extrajo ítems de la factura."));
        else if (!extraction.HasConsistentLineSubtotal(MoneyTolerance))
            issues.Add(Review("ITEMS_DO_NOT_MATCH_SUBTOTAL", "La suma de los ítems no coincide con el subtotal."));

        if (duplicateFiscalIdentity)
        {
            issues.Add(Error(
                "DUPLICATE_FISCAL_INVOICE",
                "Ya existe una factura con el mismo RUC de emisor, serie y número."));
        }

        foreach (var field in extraction.FieldConfidences
                     .Where(field => field.Critical && field.Confidence < CriticalConfidenceThreshold))
        {
            issues.Add(Review(
                "LOW_OCR_CONFIDENCE",
                $"El campo crítico '{field.Field}' tiene confianza OCR de {field.Confidence:P0}."));
        }

        foreach (var field in extraction.FieldConfidences
                     .Where(field => !field.Critical && field.Confidence < CriticalConfidenceThreshold))
        {
            issues.Add(Warning(
                "LOW_NON_CRITICAL_OCR_CONFIDENCE",
                $"El campo no crítico '{field.Field}' tiene confianza OCR de {field.Confidence:P0}."));
        }

        issues.AddRange(extraction.ExtractionWarnings.Select(warning =>
            new InvoiceValidationIssue("OCR_EXTRACTION_WARNING", warning, InvoiceValidationSeverity.WARNING)));

        return InvoiceConsistencyResult.FromIssues(issues, checkedAt);
    }

    private static InvoiceValidationIssue Review(string code, string message)
        => new(code, message, InvoiceValidationSeverity.REVIEW);

    private static InvoiceValidationIssue Error(string code, string message)
        => new(code, message, InvoiceValidationSeverity.ERROR);

    private static InvoiceValidationIssue Warning(string code, string message)
        => new(code, message, InvoiceValidationSeverity.WARNING);
}
