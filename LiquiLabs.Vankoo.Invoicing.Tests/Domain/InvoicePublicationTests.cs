using LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using LiquiLabs.Vankoo.Invoicing.Tests.TestData;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Domain;

public sealed class InvoicePublicationTests
{
    [Fact]
    public void MarkPublished_MovesConsistencyPassedInvoiceToPublished()
    {
        var invoice = InvoiceTestData.CreateConsistencyPassed();
        Assert.Equal(InvoiceStatus.CONSISTENCY_PASSED, invoice.Status);

        invoice.MarkPublished();

        Assert.Equal(InvoiceStatus.PUBLISHED, invoice.Status);
    }

    [Fact]
    public void MarkPublished_RejectsInvoiceThatHasNotPassedConsistency()
    {
        var invoice = InvoiceTestData.CreateUploaded();

        var error = Assert.Throws<InvalidInvoiceStateException>(invoice.MarkPublished);

        Assert.Equal(InvoiceStatus.UPLOADED, error.CurrentStatus);
        Assert.Equal(InvoiceStatus.UPLOADED, invoice.Status);
    }

    [Fact]
    public void MarkPublished_RejectsInvoiceThatIsAlreadyPublished()
    {
        var invoice = InvoiceTestData.CreateConsistencyPassed();
        invoice.MarkPublished();

        Assert.Throws<InvalidInvoiceStateException>(invoice.MarkPublished);
    }

    [Fact]
    public void MarkPublished_MovesApprovedInvoiceToPublished()
    {
        var invoice = InvoiceTestData.CreateConsistencyPassed();
        invoice.MarkApproved();

        invoice.MarkPublished();

        Assert.Equal(InvoiceStatus.PUBLISHED, invoice.Status);
    }

    [Fact]
    public void MarkApproved_MovesConsistencyPassedInvoiceToApproved()
    {
        var invoice = InvoiceTestData.CreateConsistencyPassed();

        invoice.MarkApproved();

        Assert.Equal(InvoiceStatus.APPROVED, invoice.Status);
    }

    [Fact]
    public void MarkApproved_RejectsInvoiceThatHasNotPassedConsistency()
    {
        var invoice = InvoiceTestData.CreateUploaded();

        var error = Assert.Throws<InvalidInvoiceStateException>(invoice.MarkApproved);

        Assert.Equal(InvoiceStatus.UPLOADED, error.CurrentStatus);
    }

    [Fact]
    public void MarkNotEligibleByRisk_MovesConsistencyPassedInvoiceToNotEligible()
    {
        var invoice = InvoiceTestData.CreateConsistencyPassed();

        invoice.MarkNotEligibleByRisk();

        Assert.Equal(InvoiceStatus.NOT_ELIGIBLE, invoice.Status);
    }

    [Fact]
    public void MarkNotEligibleByRisk_RejectsInvoiceTheRiskServiceAlreadyApproved()
    {
        var invoice = InvoiceTestData.CreateConsistencyPassed();
        invoice.MarkApproved();

        var error = Assert.Throws<InvalidInvoiceStateException>(invoice.MarkNotEligibleByRisk);

        Assert.Equal(InvoiceStatus.APPROVED, error.CurrentStatus);
        Assert.Equal(InvoiceStatus.APPROVED, invoice.Status);
    }
}
