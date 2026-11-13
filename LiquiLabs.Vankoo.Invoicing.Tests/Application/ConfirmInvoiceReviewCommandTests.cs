using FluentValidation;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.ConfirmInvoiceReview;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.OcrProcessing.ProcessOcrSynchronously;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.RecordMypeKycStatus;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.UploadInvoice;
using LiquiLabs.Vankoo.Invoicing.Application.IntegrationEvents;
using LiquiLabs.Vankoo.Invoicing.Application.Internal.Ocr;
using LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using LiquiLabs.Vankoo.Invoicing.Tests.Acceptance.Support;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Application;

/// <summary>La MYPE confirma, o corrige, lo que Azure leyó con dudas, y la factura sigue.</summary>
public sealed class ConfirmInvoiceReviewCommandTests : IDisposable
{
    private const string IssuerRuc = "20573093420";
    private const string PayerRuc = "20169004359";
    private const Currency Pen = Currency.PEN;

    private readonly InvoicingTestHost _host = new();
    private readonly string _mype = Guid.NewGuid().ToString();
    private readonly DateOnly _issued = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5);

    public void Dispose() => _host.Dispose();

    /// <summary>An invoice Azure read right, but with a total it was only 74 % sure of.</summary>
    private async Task<string> InvoiceInReview(float totalConfidence = 0.74f)
    {
        await _host.SendAsync(new RecordMypeKycStatusCommand(_mype, Guid.NewGuid().ToString(), MypeKycStatus.Verified, DateTime.UtcNow));
        var content = "%PDF-1.7\nfactura en revision\n%%EOF"u8.ToArray();
        var id = await _host.SendAsync(new UploadInvoiceCommand
        {
            MypeId = _mype,
            OriginalName = "E001-2.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = content.Length,
            FileStream = new MemoryStream(content)
        });

        var issued = _issued.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var items = _host.LineItemResolver.Resolve(
            [new OcrLineItemCandidate("Polo pima", 100m, 50.50m, 5050m, 0.71f)], Money.Of(5050m, Pen), Pen);
        _host.Ocr.Extraction = new OcrExtractionResult(
            IssuerData.Create(RucNumber.Of(IssuerRuc), "TEXTILES ALPACA ANDINA S.A.C."),
            PayerData.Create(RucNumber.Of(PayerRuc), "DISTRIBUIDORA ASIA S.A."),
            InvoiceMetadata.Create("E001", "2", issued, issued.AddDays(29), Pen, 0.9f),
            InvoiceAmounts.Create(Money.Of(5050m, Pen), Money.Of(909m, Pen), Money.Zero(Pen), Money.Of(5959m, Pen)),
            items.Items,
            [new OcrFieldConfidence("InvoiceTotal", totalConfidence), new OcrFieldConfidence("Items", 0.71f, critical: false)],
            items.Warnings);
        await _host.SendAsync(new ProcessOcrSynchronouslyCommand(id));
        return id;
    }

    private ConfirmInvoiceReviewCommand Confirm(
        string id,
        string? mype = null,
        decimal subtotal = 5050m,
        decimal tax = 909m,
        decimal total = 5959m,
        DateOnly? dueDate = null)
        => new(id, mype ?? _mype, "E001", "2", _issued, dueDate ?? _issued.AddDays(29),
            IssuerRuc, PayerRuc, subtotal, tax, total);

    [Fact]
    public async Task AConfirmedInvoice_MovesOnToRisk()
    {
        var id = await InvoiceInReview();
        Assert.Equal(InvoiceStatus.REQUIRES_REVIEW, _host.Invoices.Invoices.Single().Status);
        Assert.Empty(_host.EventBus.Published);

        var response = await _host.SendAsync(Confirm(id));

        Assert.Equal("CONSISTENCY_PASSED", response.Status);
        Assert.True(response.EligibleForFunding);
        Assert.Equal("PUBLISHED", response.IntegrationEventStatus);
        Assert.Contains(response.ValidationIssues, issue => issue.Code == "MYPE_CONFIRMED_DATA");
        var published = Assert.IsType<InvoiceEligibleForFundingIntegrationEvent>(Assert.Single(_host.EventBus.Published));
        Assert.Equal(5959m, published.TotalAmount);
    }

    [Fact]
    public async Task TheMypesCorrection_IsWhatGetsStoredAndPublished()
    {
        var id = await InvoiceInReview();

        var response = await _host.SendAsync(Confirm(id, subtotal: 5000m, tax: 900m, total: 5900m));

        Assert.Equal(5900m, response.Total);
        Assert.Equal(5000m, response.Subtotal);
        Assert.Equal(900m, response.Tax);
        var published = Assert.IsType<InvoiceEligibleForFundingIntegrationEvent>(Assert.Single(_host.EventBus.Published));
        Assert.Equal(5900m, published.TotalAmount);
    }

    [Fact]
    public async Task ConfirmedTotalsThatDoNotAddUp_StayInReview()
    {
        var id = await InvoiceInReview();

        var response = await _host.SendAsync(Confirm(id, total: 6000m));

        Assert.Equal("REQUIRES_REVIEW", response.Status);
        Assert.Contains(response.ValidationIssues, issue => issue.Code == "TOTALS_DO_NOT_RECONCILE");
        Assert.Empty(_host.EventBus.Published);
    }

    [Fact]
    public async Task AConfirmedDueDateInThePast_IsNotEligible()
    {
        var id = await InvoiceInReview();

        var response = await _host.SendAsync(Confirm(id, dueDate: _issued.AddDays(1)));

        Assert.Equal("NOT_ELIGIBLE", response.Status);
        Assert.Contains(response.ValidationIssues, issue => issue.Code == "INVOICE_EXPIRED");
        Assert.Empty(_host.EventBus.Published);
    }

    [Fact]
    public async Task AnInvoiceNotInReview_CannotBeConfirmed()
    {
        var id = await InvoiceInReview(totalConfidence: 0.95f);

        await Assert.ThrowsAsync<InvalidInvoiceStateException>(() => _host.SendAsync(Confirm(id)));
    }

    [Fact]
    public async Task AnotherMypesInvoice_ReadsAsNotFound()
    {
        var id = await InvoiceInReview();

        await Assert.ThrowsAsync<InvoiceNotFoundException>(() =>
            _host.SendAsync(Confirm(id, mype: Guid.NewGuid().ToString())));
    }

    [Fact]
    public async Task AnInvalidRuc_IsRefused()
    {
        var id = await InvoiceInReview();

        await Assert.ThrowsAsync<InvalidRucException>(() =>
            _host.SendAsync(Confirm(id) with { PayerRuc = "20169004358" }));
    }

    [Fact]
    public async Task MissingFieldsAndADueDateBeforeIssue_AreRefusedBeforeTouchingTheInvoice()
    {
        var command = new ConfirmInvoiceReviewCommand(
            "id", _mype, "", " ", _issued, _issued.AddDays(-1), "", "", -1m, -1m, 0m);

        var error = await Assert.ThrowsAsync<ValidationException>(() => _host.SendAsync(command));

        Assert.Equal(
            ["Series", "Number", "DueDate", "IssuerRuc", "PayerRuc", "Subtotal", "Tax", "Total"],
            error.Errors.Select(e => e.PropertyName));
    }
}
