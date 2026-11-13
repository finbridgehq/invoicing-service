using LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

namespace LiquiLabs.Vankoo.Invoicing.Domain.Aggregates;

public sealed class Invoice
{
    public InvoiceId Id { get; private set; } = null!;
    public MypeId MypeId { get; private set; } = null!;
    public IssuerData? IssuerData { get; private set; }
    public PayerData? PayerData { get; private set; }
    public InvoiceStatus Status { get; private set; }
    public InvoiceDocument Document { get; private set; } = null!;
    public Money? TotalAmount { get; private set; }
    public Money? SubtotalAmount { get; private set; }
    public Money? TaxAmount { get; private set; }
    public Money? DiscountAmount { get; private set; }
    public OcrOperationId? OcrOperationId { get; private set; }
    public InvoiceMetadata? Metadata { get; private set; }
    public SunatValidation? SunatValidation { get; private set; }
    public SunatVerificationStatus SunatVerificationStatus { get; private set; }
    public InvoiceConsistencyResult ConsistencyResult { get; private set; } = InvoiceConsistencyResult.NotChecked();
    public IntegrationEventPublicationStatus IntegrationEventPublicationStatus { get; private set; }
    public IReadOnlyList<InvoiceLineItem> Items => _items.AsReadOnly();
    public RejectionReason? RejectionReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<InvoiceLineItem> _items = [];

    private Invoice() { }

    private Invoice(InvoiceId id, MypeId mypeId, InvoiceDocument document)
    {
        Id = id;
        MypeId = mypeId;
        Document = document;
        Status = InvoiceStatus.UPLOADED;
        SunatVerificationStatus = SunatVerificationStatus.NOT_VERIFIED;
        ConsistencyResult = InvoiceConsistencyResult.NotChecked();
        IntegrationEventPublicationStatus = IntegrationEventPublicationStatus.NOT_APPLICABLE;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public static Invoice Create(MypeId mypeId, InvoiceDocument document)
    {
        ArgumentNullException.ThrowIfNull(mypeId);
        ArgumentNullException.ThrowIfNull(document);
        return new Invoice(InvoiceId.NewId(), mypeId, document);
    }

    public void StartOcrProcessing()
    {
        if (Status != InvoiceStatus.UPLOADED)
            throw new InvalidInvoiceStateException(Status, "start OCR processing");

        Status = InvoiceStatus.OCR_PROCESSING;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetOcrOperationId(OcrOperationId operationId)
    {
        if (Status != InvoiceStatus.OCR_PROCESSING)
            throw new InvalidInvoiceStateException(Status, "set OCR operation ID");

        OcrOperationId = operationId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reject(RejectionReason reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        if (!CanBeRejected())
            throw new InvalidInvoiceStateException(Status, "reject");

        RejectionReason = reason;
        Status = InvoiceStatus.REJECTED;
        IntegrationEventPublicationStatus = IntegrationEventPublicationStatus.NOT_APPLICABLE;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RegisterOcrResults(
        OcrExtractionResult result,
        InvoiceConsistencyResult consistencyResult)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(consistencyResult);

        if (Status != InvoiceStatus.OCR_PROCESSING)
            throw new InvalidInvoiceStateException(Status, "register OCR results");

        IssuerData = result.IssuerData;
        PayerData = result.PayerData;
        TotalAmount = result.Amounts.Total;
        SubtotalAmount = result.Amounts.Subtotal;
        TaxAmount = result.Amounts.Tax;
        DiscountAmount = result.Amounts.Discount;
        Metadata = result.Metadata;
        ConsistencyResult = consistencyResult;

        _items.Clear();
        _items.AddRange(result.Items);

        Status = consistencyResult.Status switch
        {
            InvoiceConsistencyStatus.PASSED => InvoiceStatus.CONSISTENCY_PASSED,
            InvoiceConsistencyStatus.REQUIRES_REVIEW => InvoiceStatus.REQUIRES_REVIEW,
            InvoiceConsistencyStatus.FAILED => InvoiceStatus.NOT_ELIGIBLE,
            _ => InvoiceStatus.DATA_EXTRACTED
        };

        IntegrationEventPublicationStatus = consistencyResult.IsEligibleForFunding
            ? IntegrationEventPublicationStatus.PENDING
            : IntegrationEventPublicationStatus.NOT_APPLICABLE;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkIntegrationEventPublished()
    {
        IntegrationEventPublicationStatus = IntegrationEventPublicationStatus.PUBLISHED;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkIntegrationEventPublicationFailed()
    {
        IntegrationEventPublicationStatus = IntegrationEventPublicationStatus.FAILED;
        UpdatedAt = DateTime.UtcNow;
    }

    private bool CanBeRejected()
        => Status is InvoiceStatus.UPLOADED
            or InvoiceStatus.OCR_PROCESSING
            or InvoiceStatus.DATA_EXTRACTED
            or InvoiceStatus.REQUIRES_REVIEW
            or InvoiceStatus.CONSISTENCY_PASSED
            or InvoiceStatus.SUNAT_VALIDATING
            or InvoiceStatus.SUNAT_VALIDATED;
}
