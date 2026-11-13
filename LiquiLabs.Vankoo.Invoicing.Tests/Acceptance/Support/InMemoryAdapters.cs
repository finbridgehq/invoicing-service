using LiquiLabs.Vankoo.Invoicing.Application.Interfaces;
using LiquiLabs.Vankoo.Invoicing.Domain.Aggregates;
using LiquiLabs.Vankoo.Invoicing.Domain.Entities;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Acceptance.Support;

// Adaptadores en memoria que reemplazan MongoDB, el object storage, Azure OCR y Kafka,
// para que los escenarios ejerciten los handlers reales sin infraestructura externa.

public sealed class InMemoryInvoiceRepository : IInvoiceRepository
{
    private readonly Dictionary<string, Invoice> _invoices = new();

    public IReadOnlyCollection<Invoice> Invoices => _invoices.Values;

    public Task SaveAsync(Invoice invoice, CancellationToken ct = default)
    {
        _invoices[invoice.Id.Value] = invoice;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(InvoiceId invoiceId, CancellationToken cancellationToken = default)
    {
        _invoices.Remove(invoiceId.Value);
        return Task.CompletedTask;
    }

    public Task<Invoice?> GetByIdAsync(InvoiceId invoiceId, CancellationToken cancellationToken = default)
        => Task.FromResult(_invoices.GetValueOrDefault(invoiceId.Value));

    public Task<IReadOnlyList<Invoice>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Invoice>>(_invoices.Values.ToList());

    public Task<Invoice?> GetByOcrOperationIdAsync(
        OcrOperationId operationId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_invoices.Values.FirstOrDefault(invoice => invoice.OcrOperationId == operationId));

    public Task<IReadOnlyList<Invoice>> GetByMypeIdAsync(MypeId mypeId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Invoice>>(
            _invoices.Values.Where(invoice => invoice.MypeId == mypeId).ToList());

    public Task<bool> ExistsAsync(InvoiceId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_invoices.ContainsKey(id.Value));

    public Task<bool> ExistsByContentHashAsync(string contentHash, CancellationToken cancellationToken = default)
        => Task.FromResult(_invoices.Values.Any(invoice => invoice.Document.ContentHash == contentHash));

    public Task<bool> ExistsByFiscalIdentityAsync(
        RucNumber issuerRuc,
        string series,
        string number,
        InvoiceId excludingInvoiceId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_invoices.Values.Any(invoice =>
            invoice.Id != excludingInvoiceId &&
            invoice.IssuerData?.Ruc == issuerRuc &&
            invoice.Metadata?.InvoiceSeries == series &&
            invoice.Metadata?.InvoiceNumber == number));
}

public sealed class InMemoryStorageService : IStorageService
{
    private readonly Dictionary<string, byte[]> _files = new();

    public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        _files[key] = buffer.ToArray();
    }

    public Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
        => Task.FromResult<Stream>(new MemoryStream(_files[key]));

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        _files.Remove(key);
        return Task.CompletedTask;
    }

    public Task<Stream> GetFileStreamAsync(FileKey fileKey, CancellationToken cancellationToken = default)
        => DownloadAsync(fileKey.Value, cancellationToken);
}

public sealed class StubOcrService : IOcrService
{
    public OcrExtractionResult? Extraction { get; set; }

    public Task<OcrExtractionResult> ExtractInvoiceDataAsync(
        Stream documentStream,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Extraction
            ?? throw new InvalidOperationException("The scenario did not define what the OCR reads"));
}

public sealed class RecordingEventBus : IEventBus
{
    private readonly List<object> _published = [];

    public IReadOnlyList<object> Published => _published;

    public Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
        where T : class
    {
        _published.Add(integrationEvent);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryOcrTaskRepository : IOcrTaskRepository
{
    private readonly List<string> _enqueuedInvoiceIds = [];

    public IReadOnlyList<string> EnqueuedInvoiceIds => _enqueuedInvoiceIds;

    public Task EnqueueIfNotExistsAsync(string invoiceId, CancellationToken cancellationToken = default)
    {
        if (!_enqueuedInvoiceIds.Contains(invoiceId))
            _enqueuedInvoiceIds.Add(invoiceId);
        return Task.CompletedTask;
    }

    // El worker en segundo plano no participa: el escenario dispara el OCR de forma síncrona.
    public Task<OcrTask?> ClaimNextAsync(TimeSpan leaseDuration, CancellationToken cancellationToken = default)
        => Task.FromResult<OcrTask?>(null);

    public Task SaveAsync(OcrTask task, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
