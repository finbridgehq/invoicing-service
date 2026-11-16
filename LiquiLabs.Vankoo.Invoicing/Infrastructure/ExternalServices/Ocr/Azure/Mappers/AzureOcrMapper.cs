using System.Globalization;
using System.Text.RegularExpressions;
using Azure.AI.DocumentIntelligence;
using LiquiLabs.Vankoo.Invoicing.Application.Internal.Ocr;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.ExternalServices.Ocr.Azure.Exceptions;
using OcrErrorCode = LiquiLabs.Vankoo.Invoicing.Shared.Infrastructure.Exceptions.OcrErrorCode;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.ExternalServices.Ocr.Azure.Mappers;

public class AzureOcrMapper
{
    private readonly ILogger<AzureOcrMapper> _logger;
    private readonly InvoiceLineItemResolver _lineItemResolver;

    private static readonly Regex PeruvianDatePattern =
        new(@"\b(\d{2})/(\d{2})/(\d{4})\b", RegexOptions.Compiled);

    private static readonly Regex LegalEntityMarkerPattern =
        new(@"\b(S\.?A\.?C\.?|S\.?R\.?L\.?|E\.?I\.?R\.?L\.?|SOCIEDAD\s+ANONIMA|EMPRESA\s+INDIVIDUAL)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex InvoiceNumberPattern =
        new(@"\b[A-Z0-9]{1,4}\s*[-–—]\s*\d{1,20}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex FiscalIdentityPattern =
        new(@"^\s*(?<series>[A-Z0-9]{1,4})\s*[-–—]\s*(?<number>\d{1,20})\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public AzureOcrMapper(
        ILogger<AzureOcrMapper> logger,
        InvoiceLineItemResolver lineItemResolver)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _lineItemResolver = lineItemResolver ?? throw new ArgumentNullException(nameof(lineItemResolver));
    }

    public OcrExtractionResult MapToOcrExtractionResult(AnalyzeResult azureResult)
    {
        var document = azureResult.Documents.FirstOrDefault();
        if (document == null)
            throw new OcrProcessingException(
                "Azure no encontró ningún documento en el archivo.",
                OcrErrorCode.InvalidDocument,
                isTransient: false);

        var fields = document.Fields;

        LogRawFields(fields);

        // ── Número de factura ──────────────────────────────────────────────
        var invoiceId = GetStringField(fields, "InvoiceId")
                     ?? GetStringField(fields, "InvoiceNumber")
                     ?? ExtractFromContent(azureResult, InvoiceNumberPattern)
                     ?? throw new OcrProcessingException(
                            "No se pudo extraer el número de factura.",
                            OcrErrorCode.InvalidDocument, isTransient: false);

        var (series, number) = ParseInvoiceNumber(invoiceId);

        // ── Fechas ─────────────────────────────────────────────────────────
        var issueDate = GetDateField(fields, "InvoiceDate")
                     ?? ParseDateFromContent(azureResult, "Fecha de Emisión")
                     ?? ParseDateFromContent(azureResult, "Fecha de Emision")
                     ?? ParseDateFromContent(azureResult, "Emisión")
                     ?? ParseDateFromContent(azureResult, "Emision")
                     ?? throw new OcrProcessingException(
                            "No se pudo extraer la fecha de emisión de la factura.",
                            OcrErrorCode.InvalidDocument, isTransient: false);

        var dueDate = GetDateField(fields, "DueDate")
                   ?? ParseDateFromContent(azureResult, "Fecha de Vencimiento")
                   ?? ParseDateFromContent(azureResult, "Vencimiento")
                   ?? throw new OcrProcessingException(
                          "No se pudo extraer la fecha de vencimiento de la factura.",
                          OcrErrorCode.InvalidDocument, isTransient: false);

        // ── Cliente ────────────────────────────────────────────────────────
        var payerRuc = GetStringField(fields, "CustomerTaxId")
                    ?? throw new OcrProcessingException(
                           "No se pudo extraer el RUC del cliente.",
                           OcrErrorCode.InvalidDocument, isTransient: false);

        var payerName = RequireText(
            SanitizeText(GetStringField(fields, "CustomerName")
                         ?? GetStringField(fields, "CustomerAddressRecipient")),
            "No se pudo extraer el nombre del cliente.");

        var payerAddress = SanitizeText(
            GetAddressField(fields, "CustomerAddress")
            ?? GetStringField(fields, "CustomerAddress"));

        var issuerRuc = GetStringField(fields, "VendorTaxId")
                     ?? throw new OcrProcessingException(
                         "No se pudo extraer el RUC del emisor.",
                         OcrErrorCode.InvalidDocument, isTransient: false);

        var (issuerLegalName, issuerTradeName) = ExtractIssuerNames(fields);
        var issuerAddress = SanitizeText(
            GetAddressField(fields, "VendorAddress")
            ?? GetStringField(fields, "VendorAddress"));

        // ── Montos ─────────────────────────────────────────────────────────
        var totalAmount = GetDecimalField(fields, "InvoiceTotal")
                       ?? GetDecimalField(fields, "AmountDue")
                       ?? throw new OcrProcessingException(
                              "No se pudo extraer el monto total de la factura.",
                              OcrErrorCode.InvalidDocument, isTransient: false);

        var subtotalAmount = GetDecimalField(fields, "SubTotal");
        var taxAmount = GetDecimalField(fields, "TotalTax") ?? 0m;
        var discountAmount = GetDecimalField(fields, "TotalDiscount") ?? 0m;

        // ── Moneda ─────────────────────────────────────────────────────────
        var currencyCode = GetStringField(fields, "CurrencyCode")
                        ?? GetCurrencyCode(fields, "InvoiceTotal")
                        ?? InferCurrencyFromContent(azureResult);
        var currency = ParseCurrency(currencyCode);

        subtotalAmount ??= totalAmount - taxAmount + discountAmount;

        // ── Confianza ──────────────────────────────────────────────────────
        var confidence = CalculateAverageConfidence(fields);

        // ── Items ──────────────────────────────────────────────────────────
        var resolvedItems = ExtractLineItems(
            fields,
            currency,
            Money.Of(subtotalAmount.Value, currency));

        // ── Construir Value Objects ────────────────────────────────────────
        var payerData = PayerData.Create(
            RucNumber.Of(CleanRuc(payerRuc)),
            payerName,
            tradeName: null,
            address: payerAddress);

        var issuerData = IssuerData.Create(
            RucNumber.Of(CleanRuc(issuerRuc)),
            issuerLegalName,
            issuerTradeName,
            issuerAddress);

        var metadata = InvoiceMetadata.Create(
            series,
            number,
            issueDate,
            dueDate,
            currency,
            confidence);

        var amounts = InvoiceAmounts.Create(
            Money.Of(subtotalAmount.Value, currency),
            Money.Of(taxAmount, currency),
            Money.Of(discountAmount, currency),
            Money.Of(totalAmount, currency));

        var fieldConfidences = BuildFieldConfidences(fields);
        var extractionWarnings = resolvedItems.Warnings.ToList();
        extractionWarnings.AddRange(BuildMissingConfidenceWarnings(fieldConfidences));

        _logger.LogInformation(
            "Mapped invoice {Series}-{Number} | Amount: {Amount} {Currency} | IssueDate: {IssueDate:d} | DueDate: {DueDate:d} | Confidence: {Confidence:P0}",
            series, number, totalAmount, currency, issueDate, dueDate, confidence);

        return new OcrExtractionResult(
            issuerData,
            payerData,
            metadata,
            amounts,
            resolvedItems.Items,
            fieldConfidences,
            extractionWarnings);
    }

    // ── Line Items ─────────────────────────────────────────────────────────

    private ResolvedInvoiceLineItems ExtractLineItems(
        IReadOnlyDictionary<string, DocumentField> fields,
        Currency currency,
        Money expectedSubtotal)
    {
        var candidates = new List<OcrLineItemCandidate>();

        if (!fields.TryGetValue("Items", out var itemsField) ||
            itemsField.FieldType != DocumentFieldType.List)
        {
            _logger.LogWarning("No Items field found in Azure result.");
            return new ResolvedInvoiceLineItems([], []);
        }

        foreach (var itemField in itemsField.ValueList)
        {
            if (itemField.FieldType != DocumentFieldType.Dictionary)
                continue;

            var itemFields = itemField.ValueDictionary;

            var description = GetStringField(itemFields, "Description") ?? "Unknown Item";
            var quantity = GetDecimalField(itemFields, "Quantity") ?? 1m;
            var unitPrice = GetDecimalField(itemFields, "UnitPrice");
            var amount = GetDecimalField(itemFields, "Amount");
            var confidence = GetMinimumKnownConfidence(
                itemFields,
                "Description", "Quantity", "UnitPrice", "Amount");

            try
            {
                candidates.Add(new OcrLineItemCandidate(
                    description,
                    quantity,
                    unitPrice,
                    amount,
                    confidence));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create line item: {Description}", description);
            }
        }

        return _lineItemResolver.Resolve(candidates, expectedSubtotal, currency);
    }

    // ── Field Extractors ───────────────────────────────────────────────────

    private static string? GetStringField(
        IReadOnlyDictionary<string, DocumentField> fields,
        string fieldName)
    {
        if (fields.TryGetValue(fieldName, out var field) &&
            field.FieldType == DocumentFieldType.String)
            return field.ValueString;

        return null;
    }

    /// Maneja campos de tipo Address — Azure los devuelve como objeto, no string
    private static string? GetAddressField(
        IReadOnlyDictionary<string, DocumentField> fields,
        string fieldName)
    {
        if (!fields.TryGetValue(fieldName, out var field)) return null;

        if (field.FieldType == DocumentFieldType.String)
            return field.ValueString;
        if (field.FieldType == DocumentFieldType.Address)
            return field.Content;

        return null;
    }

    /// Maneja Double, Int64 y Currency — facturas peruanas usan Currency type
    private static decimal? GetDecimalField(
        IReadOnlyDictionary<string, DocumentField> fields,
        string fieldName)
    {
        if (!fields.TryGetValue(fieldName, out var field)) return null;

        if (field.FieldType == DocumentFieldType.Double && field.ValueDouble.HasValue)
            return (decimal)field.ValueDouble.Value;
        if (field.FieldType == DocumentFieldType.Int64 && field.ValueInt64.HasValue)
            return field.ValueInt64.Value;
        if (field.FieldType == DocumentFieldType.Currency)
            return (decimal)field.ValueCurrency.Amount;

        return null;
    }

    private static string? GetCurrencyCode(
        IReadOnlyDictionary<string, DocumentField> fields,
        string fieldName)
    {
        if (!fields.TryGetValue(fieldName, out var field) ||
            field.FieldType != DocumentFieldType.Currency)
            return null;

        var currency = field.ValueCurrency;
        return currency.CurrencyCode ?? currency.CurrencySymbol;
    }

    private static float GetMinimumKnownConfidence(
        IReadOnlyDictionary<string, DocumentField> fields,
        params string[] fieldNames)
    {
        var values = fieldNames
            .Where(fields.ContainsKey)
            .Select(name => fields[name].Confidence)
            .Where(confidence => confidence.HasValue)
            .Select(confidence => confidence!.Value)
            .ToList();

        return values.Count == 0 ? 0.5f : values.Min();
    }

    private static IReadOnlyList<OcrFieldConfidence> BuildFieldConfidences(
        IReadOnlyDictionary<string, DocumentField> fields)
    {
        var confidences = new List<OcrFieldConfidence>();

        AddIfAvailable(confidences, "InvoiceId", GetSelectedFieldConfidence(fields, "InvoiceId", "InvoiceNumber"));
        AddIfAvailable(confidences, "InvoiceDate", GetSelectedFieldConfidence(fields, "InvoiceDate"));
        AddIfAvailable(confidences, "DueDate", GetSelectedFieldConfidence(fields, "DueDate"));
        AddIfAvailable(confidences, "IssuerTaxId", GetSelectedFieldConfidence(fields, "VendorTaxId"));
        AddIfAvailable(confidences, "IssuerName", GetSelectedFieldConfidence(fields, "VendorAddressRecipient", "VendorName"), critical: false);
        AddIfAvailable(confidences, "PayerTaxId", GetSelectedFieldConfidence(fields, "CustomerTaxId"));
        AddIfAvailable(confidences, "PayerName", GetSelectedFieldConfidence(fields, "CustomerName", "CustomerAddressRecipient"), critical: false);
        AddIfAvailable(confidences, "InvoiceTotal", GetSelectedFieldConfidence(fields, "InvoiceTotal", "AmountDue"));
        AddIfAvailable(confidences, "SubTotal", GetSelectedFieldConfidence(fields, "SubTotal"));
        AddIfAvailable(confidences, "Items", GetItemsMinimumConfidence(fields), critical: false);

        return confidences;
    }

    private static float? GetSelectedFieldConfidence(
        IReadOnlyDictionary<string, DocumentField> fields,
        params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            if (fields.TryGetValue(fieldName, out var field))
                return field.Confidence;
        }

        return null;
    }

    private static float? GetItemsMinimumConfidence(
        IReadOnlyDictionary<string, DocumentField> fields)
    {
        if (!fields.TryGetValue("Items", out var itemsField) ||
            itemsField.FieldType != DocumentFieldType.List)
            return null;

        var confidences = itemsField.ValueList
            .Where(item => item.FieldType == DocumentFieldType.Dictionary)
            .SelectMany(item => item.ValueDictionary.Values)
            .Where(field => field.Confidence.HasValue)
            .Select(field => field.Confidence!.Value)
            .ToList();

        return confidences.Count == 0 ? null : confidences.Min();
    }

    private static void AddIfAvailable(
        ICollection<OcrFieldConfidence> target,
        string field,
        float? confidence,
        bool critical = true)
    {
        if (confidence.HasValue)
            target.Add(new OcrFieldConfidence(field, confidence.Value, critical));
    }

    private static IReadOnlyList<string> BuildMissingConfidenceWarnings(
        IReadOnlyCollection<OcrFieldConfidence> confidences)
    {
        string[] criticalFields =
        [
            "InvoiceId", "InvoiceDate", "DueDate", "IssuerTaxId", "IssuerName",
            "PayerTaxId", "PayerName", "InvoiceTotal", "SubTotal"
        ];

        return criticalFields
            .Where(field => confidences.All(confidence => confidence.Field != field))
            .Select(field => $"Azure extrajo '{field}', pero no informó su nivel de confianza.")
            .ToList();
    }

    private static string RequireText(string? value, string errorMessage)
    {
        if (!string.IsNullOrWhiteSpace(value)) return value;

        throw new OcrProcessingException(
            errorMessage,
            OcrErrorCode.InvalidDocument,
            isTransient: false);
    }

    private static (string LegalName, string? TradeName) ExtractIssuerNames(
        IReadOnlyDictionary<string, DocumentField> fields)
    {
        var rawName = GetStringField(fields, "VendorAddressRecipient")
                      ?? GetStringField(fields, "VendorName");
        var lines = (rawName ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(SanitizeText)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count > 1)
        {
            var possibleTradeName = lines[0];
            var possibleLegalName = string.Join(" ", lines.Skip(1));

            if (!LegalEntityMarkerPattern.IsMatch(possibleTradeName) &&
                LegalEntityMarkerPattern.IsMatch(possibleLegalName))
            {
                return (
                    RequireText(possibleLegalName, "No se pudo extraer la razón social del emisor."),
                    possibleTradeName);
            }
        }

        return (
            RequireText(SanitizeText(rawName), "No se pudo extraer la razón social del emisor."),
            null);
    }

    private static DateTime? GetDateField(
        IReadOnlyDictionary<string, DocumentField> fields,
        string fieldName)
    {
        if (fields.TryGetValue(fieldName, out var field) &&
            field.FieldType == DocumentFieldType.Date &&
            field.ValueDate.HasValue)
        {
            var d = field.ValueDate.Value;
            return new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc);
        }
        return null;
    }

    // ── Fallbacks sobre texto crudo ────────────────────────────────────────

    /// Busca una fecha DD/MM/YYYY en la línea que contiene la keyword,
    /// o en la línea inmediatamente siguiente (Azure a veces las separa)
    private static DateTime? ParseDateFromContent(AnalyzeResult result, string nearKeyword)
    {
        foreach (var page in result.Pages)
        {
            var lines = page.Lines.ToList();

            for (var i = 0; i < lines.Count; i++)
            {
                if (!lines[i].Content.Contains(nearKeyword, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Buscar en la línea actual y la siguiente
                var searchTargets = new List<string> { lines[i].Content };
                if (i + 1 < lines.Count)
                    searchTargets.Add(lines[i + 1].Content);

                foreach (var target in searchTargets)
                {
                    var match = PeruvianDatePattern.Match(target);
                    if (!match.Success) continue;

                    if (DateTime.TryParseExact(
                            match.Value,
                            "dd/MM/yyyy",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out var date))
                        return DateTime.SpecifyKind(date, DateTimeKind.Utc);
                }
            }
        }
        return null;
    }

    /// Extrae el primer match de un patrón del texto crudo de todas las páginas
    private static string? ExtractFromContent(AnalyzeResult result, Regex pattern)
    {
        foreach (var page in result.Pages)
        {
            foreach (var line in page.Lines)
            {
                var match = pattern.Match(line.Content);
                if (match.Success) return match.Value;
            }
        }
        return null;
    }

    /// Infiere la moneda del texto crudo cuando Azure no la detecta como campo
    private static string InferCurrencyFromContent(AnalyzeResult result)
    {
        var content = string.Join(" ", result.Pages
            .SelectMany(p => p.Lines)
            .Select(l => l.Content));

        if (content.Contains("NUEVOS SOLES", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("SOLES", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("PEN", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("S/", StringComparison.OrdinalIgnoreCase))
            return "PEN";

        if (content.Contains("DOLARES", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("DÓLARES", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("USD", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("US$", StringComparison.OrdinalIgnoreCase))
            return "USD";

        return "PEN";
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static (string Series, string Number) ParseInvoiceNumber(string invoiceId)
    {
        var match = FiscalIdentityPattern.Match(invoiceId);
        if (match.Success)
        {
            return (
                match.Groups["series"].Value.ToUpperInvariant(),
                match.Groups["number"].Value);
        }

        throw new OcrProcessingException(
            $"El número fiscal '{invoiceId}' no tiene un formato válido de serie y correlativo.",
            OcrErrorCode.InvalidDocument,
            isTransient: false);
    }

    private static Currency ParseCurrency(string currencyCode)
    {
        return currencyCode.ToUpperInvariant() switch
        {
            "PEN" or "S/" or "SOLES" or "NUEVOS SOLES" => Currency.PEN,
            "USD" or "$" or "DOLARES" => Currency.USD,
            _ => Currency.PEN
        };
    }

    private static string CleanRuc(string ruc) =>
        new string(ruc.Where(char.IsDigit).ToArray());

    private static float CalculateAverageConfidence(
        IReadOnlyDictionary<string, DocumentField> fields)
    {
        var confidences = fields.Values
            .Where(f => f.Confidence.HasValue)
            .Select(f => f.Confidence!.Value)
            .ToList();

        return confidences.Any() ? confidences.Average() : 0.5f;
    }

    private void LogRawFields(IReadOnlyDictionary<string, DocumentField> fields)
    {
        if (!_logger.IsEnabled(LogLevel.Debug)) return;

        _logger.LogDebug("=== AZURE RAW FIELDS (development diagnostics) ===");
        foreach (var (key, field) in fields)
        {
            _logger.LogDebug(
                "Field: {Key} | Type: {Type} | Value: {Value} | Confidence: {Conf:P0}",
                key, field.FieldType, field.Content, field.Confidence ?? 0);
        }
    }

    private static string SanitizeText(string? text) =>
            string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : Regex.Replace(
                    text.Replace("\n", " ").Replace("\r", " ").Replace('`', ' '),
                    @"\s{2,}",
                    " ").Trim();


}
