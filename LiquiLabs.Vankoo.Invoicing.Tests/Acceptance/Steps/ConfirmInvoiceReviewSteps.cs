using LiquiLabs.Vankoo.Invoicing.Application.Commands.ConfirmInvoiceReview;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.OcrProcessing.ProcessOcrSynchronously;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.RecordMypeKycStatus;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.UploadInvoice;
using LiquiLabs.Vankoo.Invoicing.Application.Internal.Ocr;
using LiquiLabs.Vankoo.Invoicing.Application.Resources;
using LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using LiquiLabs.Vankoo.Invoicing.Tests.Acceptance.Support;
using Reqnroll;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Acceptance.Steps;

[Binding]
public sealed class ConfirmInvoiceReviewSteps
{
    private const string IssuerRuc = "20573093420";
    private const string PayerRuc = "20169004359";
    private const Currency Pen = Currency.PEN;

    private static readonly byte[] ReadablePdf = "%PDF-1.7\nfactura en revision\n%%EOF"u8.ToArray();

    private readonly InvoicingTestHost _host;
    private readonly string _mypeId = Guid.NewGuid().ToString();
    private readonly DateOnly _issueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5);

    private string _invoiceId = string.Empty;
    private InvoiceDetailsResponse? _result;
    private InvoiceNotFoundException? _notFound;

    public ConfirmInvoiceReviewSteps(InvoicingTestHost host)
    {
        _host = host;
    }

    [Given("a verified MYPE has an invoice awaiting review after low-confidence OCR")]
    public async Task GivenAVerifiedMypeHasAnInvoiceAwaitingReview()
    {
        await _host.SendAsync(new RecordMypeKycStatusCommand(
            _mypeId,
            Guid.NewGuid().ToString(),
            MypeKycStatus.Verified,
            DateTime.UtcNow));

        _invoiceId = await _host.SendAsync(new UploadInvoiceCommand
        {
            MypeId = _mypeId,
            OriginalName = "E001-2.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = ReadablePdf.Length,
            FileStream = new MemoryStream(ReadablePdf)
        });

        _host.Ocr.Extraction = BuildLowConfidenceExtraction();
        var review = await _host.SendAsync(new ProcessOcrSynchronouslyCommand(_invoiceId));

        Assert.Equal("REQUIRES_REVIEW", review.Status);
    }

    [When("the MYPE confirms the extracted invoice data")]
    public async Task WhenTheMypeConfirmsTheExtractedInvoiceData()
    {
        _result = await _host.SendAsync(Confirmation());
    }

    [When("the MYPE confirms an incorrect invoice total")]
    public async Task WhenTheMypeConfirmsAnIncorrectInvoiceTotal()
    {
        _result = await _host.SendAsync(Confirmation(total: 6000m));
    }

    [When("another MYPE tries to confirm the invoice")]
    public async Task WhenAnotherMypeTriesToConfirmTheInvoice()
    {
        _notFound = await Assert.ThrowsAsync<InvoiceNotFoundException>(() =>
            _host.SendAsync(Confirmation(mypeId: Guid.NewGuid().ToString())));
    }

    [Then("the confirmed invoice status is {string}")]
    public void ThenTheConfirmedInvoiceStatusIs(string status)
    {
        Assert.NotNull(_result);
        Assert.Equal(status, _result.Status);
    }

    [Then("the confirmation reports the issue {string}")]
    public void ThenTheConfirmationReportsTheIssue(string issueCode)
    {
        Assert.NotNull(_result);
        Assert.Contains(_result.ValidationIssues, issue => issue.Code == issueCode);
    }

    [Then("the confirmation is rejected with InvoiceNotFoundException")]
    public void ThenTheConfirmationIsRejectedWithInvoiceNotFoundException()
    {
        Assert.NotNull(_notFound);
        Assert.Equal("INVOICE_NOT_FOUND", _notFound.ErrorCode);
    }

    private ConfirmInvoiceReviewCommand Confirmation(
        string? mypeId = null,
        decimal total = 5959m)
        => new(
            _invoiceId,
            mypeId ?? _mypeId,
            "E001",
            "2",
            _issueDate,
            _issueDate.AddDays(29),
            IssuerRuc,
            PayerRuc,
            5050m,
            909m,
            total);

    private OcrExtractionResult BuildLowConfidenceExtraction()
    {
        var issuedAt = _issueDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var items = _host.LineItemResolver.Resolve(
            [new OcrLineItemCandidate("Polo pima", 100m, 50.50m, 5050m, 0.95f)],
            Money.Of(5050m, Pen),
            Pen);

        return new OcrExtractionResult(
            IssuerData.Create(RucNumber.Of(IssuerRuc), "TEXTILES ALPACA ANDINA S.A.C."),
            PayerData.Create(RucNumber.Of(PayerRuc), "DISTRIBUIDORA ASIA S.A."),
            InvoiceMetadata.Create("E001", "2", issuedAt, issuedAt.AddDays(29), Pen, 0.95f),
            InvoiceAmounts.Create(
                Money.Of(5050m, Pen),
                Money.Of(909m, Pen),
                Money.Zero(Pen),
                Money.Of(5959m, Pen)),
            items.Items,
            [new OcrFieldConfidence("InvoiceTotal", 0.74f)],
            items.Warnings);
    }
}
