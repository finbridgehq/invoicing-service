using Azure.AI.DocumentIntelligence;
using LiquiLabs.Vankoo.Invoicing.Application.Internal.Ocr;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.ExternalServices.Ocr.Azure.Mappers;
using Microsoft.Extensions.Logging.Abstractions;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Infrastructure;

public sealed class AzureOcrMapperTests
{
    [Fact]
    public void Map_ExtractsIssuerAndResolvesSecondInvoiceItems()
    {
        var fields = new Dictionary<string, DocumentField>
        {
            ["InvoiceId"] = StringField("E001-28", 0.95f),
            ["InvoiceDate"] = DateField(new DateTimeOffset(2020, 9, 26, 0, 0, 0, TimeSpan.Zero), null),
            ["DueDate"] = DateField(new DateTimeOffset(2020, 9, 27, 0, 0, 0, TimeSpan.Zero), null),
            ["VendorTaxId"] = StringField("20601144311", 0.95f),
            ["VendorName"] = StringField("GRUPO CREATIVO\nGRUPO CREATIVO PYME E.I.R.L.", 0.342f),
            ["VendorAddressRecipient"] = StringField("GRUPO CREATIVO\nGRUPO CREATIVO PYME E.I.R.L.", 0.342f),
            ["CustomerTaxId"] = StringField("10445899611", 0.956f),
            ["CustomerName"] = StringField("CHINCHA ASENCIO `YUVI ENSON", 0.916f),
            ["SubTotal"] = CurrencyField(330, "S/", "PEN", 0.938f),
            ["TotalTax"] = CurrencyField(59.40, "S/", "PEN", 0.917f),
            ["TotalDiscount"] = CurrencyField(0, "S/", "PEN", 0.936f),
            ["InvoiceTotal"] = CurrencyField(389.40, "S/", "PEN", 0.923f),
            ["Items"] = ListField(
            [
                DictionaryField(new Dictionary<string, DocumentField>
                {
                    ["Description"] = StringField("COJINES PERSONALIZADOS + FORRO 40X40 CM", 0.944f),
                    ["Quantity"] = NumberField(6, 0.945f),
                    ["Amount"] = CurrencyField(25, null, "PEN", 0.501f)
                }),
                DictionaryField(new Dictionary<string, DocumentField>
                {
                    ["Description"] = StringField("COJINES 50X50 CM + FORRO", 0.944f),
                    ["Quantity"] = NumberField(6, 0.944f),
                    ["UnitPrice"] = CurrencyField(30, null, "PEN", 0.32f)
                })
            ])
        };

        var document = DocumentIntelligenceModelFactory.AnalyzedDocument(
            "invoice",
            [],
            [],
            DocumentIntelligenceModelFactory.DocumentFieldDictionary(fields),
            1f);
        var analyzeResult = DocumentIntelligenceModelFactory.AnalyzeResult(
            modelId: "prebuilt-invoice",
            apiVersion: "2024-11-30",
            content: "",
            documents: [document]);
        var mapper = new AzureOcrMapper(
            NullLogger<AzureOcrMapper>.Instance,
            new InvoiceLineItemResolver());

        var result = mapper.MapToOcrExtractionResult(analyzeResult);

        Assert.Equal("20601144311", result.IssuerData.Ruc.Value);
        Assert.Equal("GRUPO CREATIVO PYME E.I.R.L.", result.IssuerData.LegalName);
        Assert.Equal("GRUPO CREATIVO", result.IssuerData.TradeName);
        Assert.Equal("10445899611", result.PayerData.Ruc.Value);
        Assert.Equal("CHINCHA ASENCIO YUVI ENSON", result.PayerData.LegalName);
        Assert.Equal(330m, result.Items.Sum(item => item.Subtotal.Amount));
        Assert.Equal(25m, result.Items[0].UnitPrice.Amount);
        Assert.Equal(389.40m, result.Amounts.Total.Amount);
        Assert.Contains(result.ExtractionWarnings, warning => warning.Contains("UnitPrice"));
        Assert.DoesNotContain(result.FieldConfidences, field => field.Field == "InvoiceDate");
        Assert.DoesNotContain(result.FieldConfidences, field => field.Field == "DueDate");
        Assert.False(Assert.Single(result.FieldConfidences, field => field.Field == "Items").Critical);
        Assert.False(Assert.Single(result.FieldConfidences, field => field.Field == "IssuerName").Critical);
        Assert.Contains(result.ExtractionWarnings, warning => warning.Contains("InvoiceDate"));
        Assert.Contains(result.ExtractionWarnings, warning => warning.Contains("DueDate"));
    }

    private static DocumentField StringField(string value, float confidence)
        => DocumentIntelligenceModelFactory.DocumentField(
            fieldType: DocumentFieldType.String,
            valueString: value,
            content: value,
            confidence: confidence);

    private static DocumentField DateField(DateTimeOffset value, float? confidence)
        => DocumentIntelligenceModelFactory.DocumentField(
            fieldType: DocumentFieldType.Date,
            valueDate: value,
            content: value.ToString("dd/MM/yyyy"),
            confidence: confidence);

    private static DocumentField NumberField(double value, float confidence)
        => DocumentIntelligenceModelFactory.DocumentField(
            fieldType: DocumentFieldType.Double,
            valueDouble: value,
            content: value.ToString(),
            confidence: confidence);

    private static DocumentField CurrencyField(double amount, string? symbol, string? code, float confidence)
        => DocumentIntelligenceModelFactory.DocumentField(
            fieldType: DocumentFieldType.Currency,
            valueCurrency: DocumentIntelligenceModelFactory.CurrencyValue(amount, symbol, code),
            content: $"{symbol} {amount}",
            confidence: confidence);

    private static DocumentField DictionaryField(IReadOnlyDictionary<string, DocumentField> fields)
        => DocumentIntelligenceModelFactory.DocumentField(
            fieldType: DocumentFieldType.Dictionary,
            valueDictionary: DocumentIntelligenceModelFactory.DocumentFieldDictionary(fields),
            content: "",
            confidence: 0.90f);

    private static DocumentField ListField(IEnumerable<DocumentField> fields)
        => DocumentIntelligenceModelFactory.DocumentField(
            fieldType: DocumentFieldType.List,
            valueList: fields,
            content: "",
            confidence: null);
}
