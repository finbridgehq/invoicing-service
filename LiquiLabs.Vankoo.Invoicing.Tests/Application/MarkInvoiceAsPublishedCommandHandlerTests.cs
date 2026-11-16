using FluentValidation;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.InvoicePublication.MarkInvoiceAsPublished;
using LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using LiquiLabs.Vankoo.Invoicing.Tests.Acceptance.Support;
using LiquiLabs.Vankoo.Invoicing.Tests.TestData;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Application;

public sealed class MarkInvoiceAsPublishedCommandHandlerTests : IDisposable
{
    private readonly InvoicingTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task Handle_MarksConsistencyPassedInvoiceAsPublished()
    {
        var invoice = InvoiceTestData.CreateConsistencyPassed();
        await _host.Invoices.SaveAsync(invoice);

        await _host.SendAsync(new MarkInvoiceAsPublishedCommand(invoice.Id.Value));

        var stored = await _host.Invoices.GetByIdAsync(invoice.Id);
        Assert.Equal(InvoiceStatus.PUBLISHED, stored!.Status);
    }

    [Fact]
    public async Task Handle_IsIdempotentWhenTheEventIsDeliveredTwice()
    {
        var invoice = InvoiceTestData.CreateConsistencyPassed();
        await _host.Invoices.SaveAsync(invoice);
        var command = new MarkInvoiceAsPublishedCommand(invoice.Id.Value);

        await _host.SendAsync(command);
        var publishedAt = (await _host.Invoices.GetByIdAsync(invoice.Id))!.UpdatedAt;
        await _host.SendAsync(command);

        var stored = await _host.Invoices.GetByIdAsync(invoice.Id);
        Assert.Equal(InvoiceStatus.PUBLISHED, stored!.Status);
        Assert.Equal(publishedAt, stored.UpdatedAt);
    }

    [Fact]
    public async Task Handle_FailsWhenTheInvoiceDoesNotExist()
    {
        var command = new MarkInvoiceAsPublishedCommand(Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<InvoiceNotFoundException>(() => _host.SendAsync(command));
    }

    [Fact]
    public async Task Handle_FailsWhenTheInvoiceIsRejected()
    {
        var invoice = InvoiceTestData.CreateConsistencyPassed();
        invoice.Reject(RejectionReason.Create("Rechazada para la prueba"));
        await _host.Invoices.SaveAsync(invoice);

        await Assert.ThrowsAsync<InvalidInvoiceStateException>(
            () => _host.SendAsync(new MarkInvoiceAsPublishedCommand(invoice.Id.Value)));

        var stored = await _host.Invoices.GetByIdAsync(invoice.Id);
        Assert.Equal(InvoiceStatus.REJECTED, stored!.Status);
    }

    [Fact]
    public async Task Handle_RejectsAnInvoiceIdThatIsNotAUuid()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _host.SendAsync(new MarkInvoiceAsPublishedCommand("no-es-un-uuid")));
    }
}
