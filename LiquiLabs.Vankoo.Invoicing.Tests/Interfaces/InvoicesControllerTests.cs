using LiquiLabs.Vankoo.Invoicing.Application.Commands.RecordMypeKycStatus;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.UploadInvoice;
using LiquiLabs.Vankoo.Invoicing.Application.Queries.ListMyInvoices;
using LiquiLabs.Vankoo.Invoicing.Application.Resources;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using LiquiLabs.Vankoo.Invoicing.Interfaces.Rest.Controllers;
using LiquiLabs.Vankoo.Invoicing.Tests.Acceptance.Support;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Interfaces;

public sealed class InvoicesControllerTests : IDisposable
{
    private readonly InvoicingTestHost _host = new();
    private readonly IServiceScope _scope;

    public InvoicesControllerTests() => _scope = _host.Services.CreateScope();

    public void Dispose()
    {
        _scope.Dispose();
        _host.Dispose();
    }

    private InvoicesController Controller(string? userId)
    {
        var context = new DefaultHttpContext();
        if (userId is not null) context.Request.Headers["X-User-Id"] = userId;
        return new InvoicesController(_scope.ServiceProvider.GetRequiredService<IMediator>(), null!)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    [Fact]
    public async Task EveryReadNeedsTheUserTheGatewayForwards()
    {
        var controller = Controller(userId: null);

        Assert.IsType<UnauthorizedResult>(await controller.ListMyInvoices(null, null, null));
        Assert.IsType<UnauthorizedResult>(await controller.GetInvoiceById("any", default));
        Assert.IsType<UnauthorizedResult>(await controller.DownloadInvoiceFile("any", default));
    }

    [Fact]
    public async Task ListsAndReadsTheCallersOwnInvoice()
    {
        var mype = Guid.NewGuid().ToString();
        await _host.SendAsync(new RecordMypeKycStatusCommand(mype, "company", MypeKycStatus.Verified, DateTime.UtcNow));
        var content = "%PDF-1.7\nfactura\n%%EOF"u8.ToArray();
        var id = await _host.SendAsync(new UploadInvoiceCommand
        {
            MypeId = mype,
            OriginalName = "E001-1.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = content.Length,
            FileStream = new MemoryStream(content)
        });
        var controller = Controller(mype);

        var list = Assert.IsType<OkObjectResult>(await controller.ListMyInvoices(null, "UPLOADED", null, 1, ListMyInvoicesQuery.DefaultPageSize));
        Assert.Equal([id], Assert.IsType<InvoicePageResponse>(list.Value).Items.Select(item => item.Id));
        var detail = Assert.IsType<OkObjectResult>(await controller.GetInvoiceById(id, default));
        Assert.Equal(id, Assert.IsType<InvoiceDetailsResponse>(detail.Value).InvoiceId);
        var file = Assert.IsType<FileStreamResult>(await controller.DownloadInvoiceFile(id, default));
        Assert.Equal("E001-1.pdf", file.FileDownloadName);
    }
}
