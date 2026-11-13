using FluentValidation;
using LiquiLabs.Vankoo.Invoicing.Shared.Domain;
using LiquiLabs.Vankoo.Invoicing.Shared.Domain.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Shared.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace LiquiLabs.Vankoo.Invoicing.Shared.Infrastructure.Web;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken ct)
    {
        var (statusCode, response) = MapException(exception);

        if (exception is InfrastructureException)
            _logger.LogError(exception, "Infrastructure error [{ErrorCode}]", response.ErrorCode);
        else if (statusCode == StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Unexpected error");
        else
            _logger.LogWarning("Domain error [{ErrorCode}]: {Message}", response.ErrorCode, response.Message);

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(response, ct);

        return true;
    }

    private static (int statusCode, ErrorResponse response) MapException(Exception exception) =>
        exception switch
        {
            EntityNotFoundException ex =>
                (StatusCodes.Status404NotFound,
                 new ErrorResponse(ex.ErrorCode, ex.Message)),

            BusinessRuleViolationException ex =>
                (StatusCodes.Status422UnprocessableEntity,
                 new ErrorResponse(ex.ErrorCode, ex.Message)),

            InvalidValueException ex =>
                (StatusCodes.Status400BadRequest,
                 new ErrorResponse(ex.ErrorCode, ex.Message)),

            DomainException ex =>
                (StatusCodes.Status422UnprocessableEntity,
                 new ErrorResponse(ex.ErrorCode, ex.Message)),

            ValidationException ex =>
                (StatusCodes.Status400BadRequest,
                 new ErrorResponse(
                     ErrorCodes.ValidationFailed,
                     "One or more validation errors occurred.",
                     ex.Errors.Select(e => e.ErrorMessage).ToList())),

            InfrastructureException =>
                (StatusCodes.Status500InternalServerError,
                 new ErrorResponse(ErrorCodes.InternalError, "An internal service error occurred.")),

            _ =>
                (StatusCodes.Status500InternalServerError,
                 new ErrorResponse(ErrorCodes.InternalError, "An unexpected error occurred."))
        };
}
