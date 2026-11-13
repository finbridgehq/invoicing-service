using FluentValidation;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.DeleteInvoice;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.OcrProcessing.ProcessOcrSynchronously;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.RecordMypeKycStatus;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.UploadInvoice;
using LiquiLabs.Vankoo.Invoicing.Application.Internal.Ocr;
using LiquiLabs.Vankoo.Invoicing.Application.Queries.DownloadInvoiceFile;
using LiquiLabs.Vankoo.Invoicing.Application.Queries.GetInvoiceById;
using LiquiLabs.Vankoo.Invoicing.Application.Queries.ListMyInvoices;
using LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using LiquiLabs.Vankoo.Invoicing.Tests.Acceptance.Support;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Application;

/// <summary>La lista de facturas de la MYPE y que cada una solo la vea su dueña.</summary>
public sealed class ListMyInvoicesQueryTests : IDisposable
{
    private const string IssuerRuc = "20573093420";
    private const string PayerRuc = "20169004359";
    private const string OtherPayerRuc = "20601144311";

    private readonly InvoicingTestHost _host = new();
    private readonly string _mype = Guid.NewGuid().ToString();
    private readonly string _otherMype = Guid.NewGuid().ToString();
    private int _uploads;

    public void Dispose() => _host.Dispose();

    private async Task Verify(string mypeId) =>
        await _host.SendAsync(new RecordMypeKycStatusCommand(mypeId, Guid.NewGuid().ToString(), MypeKycStatus.Verified, DateTime.UtcNow));

    private async Task<string> Upload(string mypeId, string? number = null, string payerRuc = PayerRuc, DateTime? issueDate = null)
    {
        var content = System.Text.Encoding.UTF8.GetBytes($"%PDF-1.7\nfactura {++_uploads}\n%%EOF");
        var id = await _host.SendAsync(new UploadInvoiceCommand
        {
            MypeId = mypeId,
            OriginalName = $"E001-{_uploads}.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = content.Length,
            FileStream = new MemoryStream(content)
        });
        if (number is not null)
        {
            _host.Ocr.Extraction = Extraction(number, payerRuc, issueDate ?? DateTime.UtcNow.Date.AddDays(-5));
            await _host.SendAsync(new ProcessOcrSynchronouslyCommand(id));
        }
        // CreatedAt ordena la lista: que dos subidas no caigan en el mismo instante.
        await Task.Delay(15);
        return id;
    }

    private OcrExtractionResult Extraction(string number, string payerRuc, DateTime issueDate)
    {
        const Currency currency = Currency.PEN;
        var items = _host.LineItemResolver.Resolve(
            [new OcrLineItemCandidate("Servicio", 1m, 1000m, 1000m, 0.95f)], Money.Of(1000m, currency), currency);
        return new OcrExtractionResult(
            IssuerData.Create(RucNumber.Of(IssuerRuc), "ALVACOR INGENIEROS"),
            PayerData.Create(RucNumber.Of(payerRuc), $"PAGADOR {payerRuc}"),
            InvoiceMetadata.Create("E001", number, issueDate, issueDate.AddDays(90), currency, 0.95f),
            InvoiceAmounts.Create(Money.Of(1000m, currency), Money.Of(180m, currency), Money.Zero(currency), Money.Of(1180m, currency)),
            items.Items,
            [new OcrFieldConfidence("Items", 0.95f)],
            items.Warnings);
    }

    [Fact]
    public async Task Lists_OnlyTheCallersInvoices_NewestFirst_WithNullsUntilRead()
    {
        await Verify(_mype);
        await Verify(_otherMype);
        var read = await Upload(_mype, number: "10");
        await Upload(_otherMype, number: "20");
        var unread = await Upload(_mype);

        var page = await _host.SendAsync(new ListMyInvoicesQuery(_mype));

        Assert.Equal(2, page.TotalItems);
        Assert.Equal([unread, read], page.Items.Select(item => item.Id));
        var pending = page.Items[0];
        Assert.Equal("UPLOADED", pending.Status);
        Assert.Null(pending.Number);
        Assert.Null(pending.Amount);
        var done = page.Items[1];
        Assert.Equal("E001-10", done.Number);
        Assert.Equal(PayerRuc, done.PayerRuc);
        Assert.Equal(1180m, done.Amount?.Value);
        Assert.Equal("PEN", done.Amount?.Currency);
        Assert.NotNull(done.DueDate);
    }

    [Fact]
    public async Task Filters_BySearchStatusAndDate()
    {
        await Verify(_mype);
        var e10 = await Upload(_mype, number: "10", issueDate: new DateTime(2026, 10, 1));
        var e11 = await Upload(_mype, number: "11", payerRuc: OtherPayerRuc, issueDate: new DateTime(2026, 10, 20));
        var unread = await Upload(_mype);

        Assert.Equal([e10], (await _host.SendAsync(new ListMyInvoicesQuery(_mype, Search: "e001-10"))).Items.Select(i => i.Id));
        Assert.Equal([e11], (await _host.SendAsync(new ListMyInvoicesQuery(_mype, Search: OtherPayerRuc))).Items.Select(i => i.Id));
        Assert.Equal([unread], (await _host.SendAsync(new ListMyInvoicesQuery(_mype, Status: "UPLOADED"))).Items.Select(i => i.Id));
        // Varios estados separados por comas: la web pide así los que muestra con la misma etiqueta.
        Assert.Equal([e11, e10], (await _host.SendAsync(new ListMyInvoicesQuery(_mype, Status: "CONSISTENCY_PASSED, SUNAT_VALIDATED"))).Items.Select(i => i.Id));
        Assert.Equal([unread, e11, e10], (await _host.SendAsync(new ListMyInvoicesQuery(_mype, Status: "UPLOADED,CONSISTENCY_PASSED"))).Items.Select(i => i.Id));
        // El 15 de octubre deja fuera la emitida el 1; la no leída cuenta por su día de subida, hoy.
        Assert.Equal([unread, e11], (await _host.SendAsync(new ListMyInvoicesQuery(_mype, From: new DateOnly(2026, 10, 15)))).Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Paginates_AndCountsEveryMatch()
    {
        await Verify(_mype);
        var ids = new List<string>();
        for (var i = 0; i < 3; i++) ids.Add(await Upload(_mype));

        var second = await _host.SendAsync(new ListMyInvoicesQuery(_mype, Page: 2, PageSize: 2));

        Assert.Equal(3, second.TotalItems);
        Assert.Equal(2, second.Page);
        Assert.Equal(2, second.PageSize);
        Assert.Equal([ids[0]], second.Items.Select(item => item.Id));
    }

    [Theory]
    [InlineData("PAGADA", 1, 10)]
    [InlineData("UPLOADED,PAGADA", 1, 10)]
    [InlineData(" , ", 1, 10)]
    [InlineData(null, 0, 10)]
    [InlineData(null, 1, 101)]
    public async Task RefusesAnUnknownStatusOrAnInvalidPage(string? status, int page, int pageSize)
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _host.SendAsync(new ListMyInvoicesQuery(_mype, Status: status, Page: page, PageSize: pageSize)));
    }

    [Fact]
    public async Task AnotherMypesInvoice_ReadsAsNotFound_InDetailAndFile()
    {
        await Verify(_mype);
        var id = await Upload(_mype, number: "10");

        await Assert.ThrowsAsync<InvoiceNotFoundException>(() => _host.SendAsync(new GetInvoiceByIdQuery(id, _otherMype)));
        await Assert.ThrowsAsync<InvoiceNotFoundException>(() => _host.SendAsync(new DownloadInvoiceFileQuery(id, _otherMype)));

        Assert.Equal(id, (await _host.SendAsync(new GetInvoiceByIdQuery(id, _mype))).InvoiceId);
        var file = await _host.SendAsync(new DownloadInvoiceFileQuery(id, _mype));
        Assert.Equal("application/pdf", file.ContentType);
    }

    [Fact]
    public async Task AnotherMypesInvoice_CannotBeRereadOrDeleted()
    {
        await Verify(_mype);
        var id = await Upload(_mype);
        _host.Ocr.Extraction = Extraction("12", PayerRuc, DateTime.UtcNow.Date.AddDays(-5));

        await Assert.ThrowsAsync<InvoiceNotFoundException>(() => _host.SendAsync(new ProcessOcrSynchronouslyCommand(id, _otherMype)));
        await Assert.ThrowsAsync<InvoiceNotFoundException>(() => _host.SendAsync(new DeleteInvoiceCommand(id, _otherMype)));
        Assert.Equal("UPLOADED", (await _host.SendAsync(new GetInvoiceByIdQuery(id, _mype))).Status);

        Assert.Equal("E001-12", (await _host.SendAsync(new ProcessOcrSynchronouslyCommand(id, _mype))).FiscalInvoiceNumber);
        await _host.SendAsync(new DeleteInvoiceCommand(id, _mype));
        await Assert.ThrowsAsync<InvoiceNotFoundException>(() => _host.SendAsync(new GetInvoiceByIdQuery(id, _mype)));
    }

    [Fact]
    public async Task TheOcrWorker_RereadsWithoutAMype()
    {
        await Verify(_mype);
        var id = await Upload(_mype);
        _host.Ocr.Extraction = Extraction("13", PayerRuc, DateTime.UtcNow.Date.AddDays(-5));

        var details = await _host.SendAsync(new ProcessOcrSynchronouslyCommand(id));

        Assert.Equal("E001-13", details.FiscalInvoiceNumber);
    }
}
