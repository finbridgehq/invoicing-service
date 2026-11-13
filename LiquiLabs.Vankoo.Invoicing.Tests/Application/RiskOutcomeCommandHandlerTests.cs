using FluentValidation;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.InvoicePublication.MarkInvoiceAsApproved;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.InvoicePublication.MarkInvoiceAsNotEligible;
using LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using LiquiLabs.Vankoo.Invoicing.Tests.Acceptance.Support;
using LiquiLabs.Vankoo.Invoicing.Tests.TestData;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Application;

public sealed class RiskOutcomeCommandHandlerTests : IDisposable
{
    private readonly InvoicingTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task MarkAsApproved_MovesConsistencyPassedInvoiceToApproved()
    {
        var invoice = InvoiceTestData.CreateConsistencyPassed();
        await _host.Invoices.SaveAsync(invoice);

        await _host.SendAsync(new MarkInvoiceAsApprovedCommand(invoice.Id.Value));

        var stored = await _host.Invoices.GetByIdAsync(invoice.Id);
        Assert.Equal(InvoiceStatus.APPROVED, stored!.Status);
    }

    [Fact]
    public async Task MarkAsApproved_LeavesAnInvoiceAlreadyInAuctionAlone()
    {
        var invoice = InvoiceTestData.CreateConsistencyPassed();
        invoice.MarkPublished();
        await _host.Invoices.SaveAsync(invoice);
        var publishedAt = (await _host.Invoices.GetByIdAsync(invoice.Id))!.UpdatedAt;

        await _host.SendAsync(new MarkInvoiceAsApprovedCommand(invoice.Id.Value));
        await _host.SendAsync(new MarkInvoiceAsApprovedCommand(invoice.Id.Value));

        var stored = await _host.Invoices.GetByIdAsync(invoice.Id);
        Assert.Equal(InvoiceStatus.PUBLISHED, stored!.Status);
        Assert.Equal(publishedAt, stored.UpdatedAt);
    }

    [Fact]
    public async Task MarkAsApproved_FailsWhenTheInvoiceDoesNotExist()
    {
        await Assert.ThrowsAsync<InvoiceNotFoundException>(
            () => _host.SendAsync(new MarkInvoiceAsApprovedCommand(Guid.NewGuid().ToString())));
    }

    [Fact]
    public async Task MarkAsNotEligible_MovesConsistencyPassedInvoiceToNotEligibleOnce()
    {
        var invoice = InvoiceTestData.CreateConsistencyPassed();
        await _host.Invoices.SaveAsync(invoice);
        var command = new MarkInvoiceAsNotEligibleCommand(invoice.Id.Value);

        await _host.SendAsync(command);
        var rejectedAt = (await _host.Invoices.GetByIdAsync(invoice.Id))!.UpdatedAt;
        await _host.SendAsync(command);

        var stored = await _host.Invoices.GetByIdAsync(invoice.Id);
        Assert.Equal(InvoiceStatus.NOT_ELIGIBLE, stored!.Status);
        Assert.Equal(rejectedAt, stored.UpdatedAt);
    }

    [Fact]
    public async Task MarkAsNotEligible_FailsWhenTheInvoiceWasNotRead()
    {
        var invoice = InvoiceTestData.CreateUploaded();
        await _host.Invoices.SaveAsync(invoice);

        await Assert.ThrowsAsync<InvalidInvoiceStateException>(
            () => _host.SendAsync(new MarkInvoiceAsNotEligibleCommand(invoice.Id.Value)));
    }

    [Fact]
    public async Task BothCommands_RejectAnInvoiceIdThatIsNotAUuid()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _host.SendAsync(new MarkInvoiceAsApprovedCommand("no-es-un-uuid")));
        await Assert.ThrowsAsync<ValidationException>(
            () => _host.SendAsync(new MarkInvoiceAsNotEligibleCommand("no-es-un-uuid")));
    }
}
