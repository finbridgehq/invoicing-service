using LiquiLabs.Vankoo.Invoicing.Application.Commands.OcrProcessing.ProcessOcrSynchronously;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.UploadInvoice;
using LiquiLabs.Vankoo.Invoicing.Application.IntegrationEvents;
using LiquiLabs.Vankoo.Invoicing.Application.Internal.Ocr;
using LiquiLabs.Vankoo.Invoicing.Application.Resources;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using LiquiLabs.Vankoo.Invoicing.Tests.Acceptance.Support;
using Reqnroll;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Acceptance.Steps;

[Binding]
public sealed class SmartInvoiceUploadSteps
{
    private const string IssuerRuc = "20573093420";
    private const string IssuerName = "ALVACOR INGENIEROS";
    private const string PayerRuc = "20169004359";
    private const string PayerName = "UNIVERSIDAD NACIONAL DE INGENIERIA UNI";

    private static readonly byte[] ReadablePdf = "%PDF-1.7\nfactura electrónica E001-4\n%%EOF"u8.ToArray();

    private readonly InvoicingTestHost _host;

    private string _mypeId = string.Empty;
    private OcrExtractionResult? _extraction;
    private string? _invoiceId;
    private InvoiceDetailsResponse? _details;

    public SmartInvoiceUploadSteps(InvoicingTestHost host)
    {
        _host = host;
    }

    [Given("a MYPE business owner is signed in through the API Gateway")]
    public void GivenABusinessOwnerIsSignedIn()
    {
        // El gateway valida el JWT y reenvía el id del usuario en X-User-Id; ese valor es el MypeId.
        _mypeId = Guid.NewGuid().ToString();
    }

    [Given("the business owner has a readable PDF invoice")]
    public void GivenAReadablePdfInvoice()
    {
        // El contenido del PDF es irrelevante para el OCR simulado; solo debe superar la
        // validación de firma de InvoiceFileInspector.
    }

    [Given(@"^the OCR reads the invoice with (.*)$")]
    public void GivenTheOcrReadsAnInconsistentInvoice(string inconsistency)
    {
        var today = DateTime.UtcNow.Date;

        _extraction = inconsistency switch
        {
            "a total that does not reconcile" => BuildExtraction(
                PayerRuc, PayerName, total: 1800.00m,
                issueDate: today.AddDays(-5), dueDate: today.AddDays(60)),

            "a low-confidence due date" => BuildExtraction(
                PayerRuc, PayerName, total: 1650.00m,
                issueDate: today.AddDays(-5), dueDate: today.AddDays(60),
                fieldConfidences: [new OcrFieldConfidence("DueDate", 0.40f)]),

            "an expired due date" => BuildExtraction(
                PayerRuc, PayerName, total: 1650.00m,
                issueDate: today.AddDays(-90), dueDate: today.AddDays(-30)),

            "the same RUC for issuer and payer" => BuildExtraction(
                IssuerRuc, IssuerName, total: 1650.00m,
                issueDate: today.AddDays(-5), dueDate: today.AddDays(60)),

            _ => throw new ArgumentOutOfRangeException(nameof(inconsistency), inconsistency, "Unknown inconsistency")
        };
    }

    [When("the business owner uploads the invoice")]
    public async Task WhenTheBusinessOwnerUploadsTheInvoice()
    {
        _host.Ocr.Extraction = _extraction;

        _invoiceId = await _host.SendAsync(new UploadInvoiceCommand
        {
            MypeId = _mypeId,
            OriginalName = "E001-4.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = ReadablePdf.Length,
            FileStream = new MemoryStream(ReadablePdf)
        });
    }

    [When("the system finishes the extraction and initial validation")]
    public async Task WhenTheSystemFinishesTheExtraction()
    {
        Assert.NotNull(_invoiceId);
        Assert.Contains(_invoiceId, _host.OcrTasks.EnqueuedInvoiceIds);

        _details = await _host.SendAsync(new ProcessOcrSynchronouslyCommand(_invoiceId));
    }

    [Then("the invoice status is {string}")]
    public void ThenTheInvoiceStatusIs(string status)
    {
        Assert.NotNull(_details);
        Assert.Equal(status, _details.Status);
    }

    [Then("the invoice reports the issue {string}")]
    public void ThenTheInvoiceReportsTheIssue(string issueCode)
    {
        Assert.NotNull(_details);
        Assert.Contains(_details.ValidationIssues, issue => issue.Code == issueCode);
    }

    [Then("the invoice is not sent to the auction")]
    public void ThenTheInvoiceIsNotSentToTheAuction()
    {
        Assert.NotNull(_details);
        Assert.False(_details.EligibleForFunding);
        Assert.Equal(nameof(IntegrationEventPublicationStatus.NOT_APPLICABLE), _details.IntegrationEventStatus);
        Assert.Empty(_host.EventBus.Published.OfType<InvoiceEligibleForFundingIntegrationEvent>());
    }

    private OcrExtractionResult BuildExtraction(
        string payerRuc,
        string payerName,
        decimal total,
        DateTime issueDate,
        DateTime dueDate,
        IReadOnlyList<OcrFieldConfidence>? fieldConfidences = null)
    {
        const Currency currency = Currency.PEN;
        const decimal subtotal = 1398.30m;
        const decimal tax = 251.70m;

        var items = _host.LineItemResolver.Resolve(
            [new OcrLineItemCandidate("Servicio de mantenimiento", 1m, subtotal, subtotal, 0.95f)],
            Money.Of(subtotal, currency),
            currency);

        return new OcrExtractionResult(
            IssuerData.Create(RucNumber.Of(IssuerRuc), IssuerName),
            PayerData.Create(RucNumber.Of(payerRuc), payerName),
            InvoiceMetadata.Create("E001", "4", issueDate, dueDate, currency, 0.95f),
            InvoiceAmounts.Create(
                Money.Of(subtotal, currency),
                Money.Of(tax, currency),
                Money.Zero(currency),
                Money.Of(total, currency)),
            items.Items,
            fieldConfidences ?? [new OcrFieldConfidence("Items", 0.95f)],
            items.Warnings);
    }
}
