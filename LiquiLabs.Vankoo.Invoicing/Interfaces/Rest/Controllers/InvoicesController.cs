using LiquiLabs.Vankoo.Invoicing.Application.Commands.DeleteAllInvoices;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.DeleteInvoice;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.OcrProcessing.ProcessOcrSynchronously;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.UploadInvoice;
using LiquiLabs.Vankoo.Invoicing.Application.Queries.DownloadInvoiceFile;
using LiquiLabs.Vankoo.Invoicing.Application.Queries.GetInvoiceById;
using LiquiLabs.Vankoo.Invoicing.Interfaces.Rest.Dto.Requests;
using LiquiLabs.Vankoo.Invoicing.Interfaces.Rest.Dto.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LiquiLabs.Vankoo.Invoicing.Interfaces.Rest.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class InvoicesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IWebHostEnvironment _environment;

    public InvoicesController(IMediator mediator, IWebHostEnvironment environment)
    {
        _mediator = mediator;
        _environment = environment;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(InvoiceResource), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UploadInvoice(
        [FromForm] UploadInvoiceResource request,
        CancellationToken cancellationToken)
    {
        // El API Gateway valida el JWT y agrega este header (BearerAuthorizationRequest
        // en vankoo-api-gateway) antes de reenviar — sin esa capa, no llega.
        var mypeId = Request.Headers["X-User-Id"].ToString();
        if (string.IsNullOrWhiteSpace(mypeId))
            return Unauthorized();

        await using var stream = request.File.OpenReadStream();
        var command = new UploadInvoiceCommand
        {
            MypeId = mypeId,
            OriginalName = request.File.FileName,
            ContentType = request.File.ContentType,
            FileSizeBytes = request.File.Length,
            FileStream = stream
        };

        var invoiceId = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction(
            nameof(DownloadInvoiceFile),
            new { id = invoiceId },
            new InvoiceResource { InvoiceId = invoiceId });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetInvoiceById(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetInvoiceByIdQuery(id), cancellationToken);
        return Ok(response);
    }

    [HttpGet("{id}/file")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadInvoiceFile(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DownloadInvoiceFileQuery(id), cancellationToken);
        return File(result.Stream, result.ContentType, result.FileName);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteInvoice(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteInvoiceCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAllInvoices(
        [FromQuery] bool confirm,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        if (!confirm)
        {
            return BadRequest(new
            {
                ErrorCode = "DELETION_CONFIRMATION_REQUIRED",
                Message = "Set confirm=true to delete every invoice and its stored document."
            });
        }

        var deletedCount = await _mediator.Send(new DeleteAllInvoicesCommand(), cancellationToken);
        return Ok(new { DeletedCount = deletedCount });
    }

    [HttpPost("{id}/ocr/sync")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ProcessOcrSync(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(
            new ProcessOcrSynchronouslyCommand(id),
            cancellationToken);
        return Ok(response);
    }
}