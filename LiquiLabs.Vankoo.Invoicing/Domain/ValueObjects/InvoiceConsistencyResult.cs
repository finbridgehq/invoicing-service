namespace LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

public enum InvoiceConsistencyStatus
{
    NOT_CHECKED = 0,
    PASSED = 1,
    REQUIRES_REVIEW = 2,
    FAILED = 3
}

public enum InvoiceValidationSeverity
{
    WARNING = 1,
    REVIEW = 2,
    ERROR = 3
}

public sealed record InvoiceValidationIssue(
    string Code,
    string Message,
    InvoiceValidationSeverity Severity);

public sealed record InvoiceConsistencyResult
{
    public InvoiceConsistencyStatus Status { get; init; }
    public IReadOnlyList<InvoiceValidationIssue> Issues { get; init; }
    public DateTime? CheckedAt { get; init; }

    private InvoiceConsistencyResult(
        InvoiceConsistencyStatus status,
        IReadOnlyList<InvoiceValidationIssue> issues,
        DateTime? checkedAt)
    {
        Status = status;
        Issues = issues;
        CheckedAt = checkedAt;
    }

    public static InvoiceConsistencyResult NotChecked()
        => new(InvoiceConsistencyStatus.NOT_CHECKED, [], null);

    public static InvoiceConsistencyResult FromIssues(
        IReadOnlyList<InvoiceValidationIssue> issues,
        DateTime checkedAt)
    {
        var status = issues.Any(issue => issue.Severity == InvoiceValidationSeverity.ERROR)
            ? InvoiceConsistencyStatus.FAILED
            : issues.Any(issue => issue.Severity == InvoiceValidationSeverity.REVIEW)
                ? InvoiceConsistencyStatus.REQUIRES_REVIEW
                : InvoiceConsistencyStatus.PASSED;

        return new InvoiceConsistencyResult(status, issues, checkedAt);
    }

    public bool IsEligibleForFunding => Status == InvoiceConsistencyStatus.PASSED;
}
