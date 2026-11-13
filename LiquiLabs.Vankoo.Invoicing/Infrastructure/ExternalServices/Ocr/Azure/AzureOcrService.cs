using Azure;
using Azure.AI.DocumentIntelligence;
using LiquiLabs.Vankoo.Invoicing.Application.Interfaces;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.ExternalServices.Ocr.Azure.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.ExternalServices.Ocr.Azure.Mappers;
using LiquiLabs.Vankoo.Invoicing.Shared.Infrastructure.Exceptions;
using Microsoft.Extensions.Options;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.ExternalServices.Ocr.Azure;

public sealed class AzureOcrService : IOcrService
{
    private readonly DocumentIntelligenceClient _client;
    private readonly AzureOcrMapper _mapper;
    private readonly ILogger<AzureOcrService> _logger;

    public AzureOcrService(
        IOptions<AzureOcrSettings> settings,
        AzureOcrMapper mapper,
        ILogger<AzureOcrService> logger)
    {
        var azureSettings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        azureSettings.Validate();

        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _client = new DocumentIntelligenceClient(
            new Uri(azureSettings.Endpoint),
            new AzureKeyCredential(azureSettings.ApiKey));
    }

    public async Task<OcrExtractionResult> ExtractInvoiceDataAsync(
        Stream documentStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentStream);
        _logger.LogInformation(
            "Starting invoice OCR with Azure Document Intelligence API 2024-11-30");

        try
        {
            var document = await BinaryData.FromStreamAsync(documentStream, cancellationToken);
            var operation = await _client.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                "prebuilt-invoice",
                document,
                cancellationToken);

            var result = operation.Value;
            _logger.LogInformation(
                "OCR analysis completed. Documents found: {DocumentCount}",
                result.Documents.Count);

            return _mapper.MapToOcrExtractionResult(result);
        }
        catch (RequestFailedException ex) when (ex.Status == 429)
        {
            throw new OcrProcessingException(
                "Límite de peticiones a Azure excedido.",
                OcrErrorCode.RateLimitExceeded,
                isTransient: true,
                ex);
        }
        catch (RequestFailedException ex) when (ex.Status is 400 or 415)
        {
            throw new OcrInvalidDocumentException(
                "El documento no es válido, está borroso o tiene un formato no soportado.",
                ex);
        }
        catch (RequestFailedException ex) when (ex.Status >= 500)
        {
            throw new OcrProcessingException(
                "El servicio de OCR de Azure está fallando.",
                OcrErrorCode.ServiceError,
                isTransient: true,
                ex);
        }
        catch (RequestFailedException ex)
        {
            throw new OcrProcessingException(
                $"Fallo desconocido en OCR: {ex.Message}",
                OcrErrorCode.Unknown,
                isTransient: false,
                ex);
        }
    }
}
