using FluentValidation;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.UploadInvoice;

public sealed class UploadInvoiceCommandValidator : AbstractValidator<UploadInvoiceCommand>
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;
    private static readonly string[] SupportedContentTypes =
        ["application/pdf", "image/jpeg", "image/png"];

    public UploadInvoiceCommandValidator()
    {
        RuleFor(command => command.MypeId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("El MypeId debe ser un UUID válido.");

        RuleFor(command => command.OriginalName).NotEmpty();
        RuleFor(command => command.FileSizeBytes)
            .GreaterThan(0).WithMessage("El archivo no puede estar vacío.")
            .LessThanOrEqualTo(MaxFileSizeBytes).WithMessage("El archivo no puede superar los 10MB.");
        RuleFor(command => command.ContentType)
            .NotEmpty()
            .Must(contentType => SupportedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            .WithMessage("La factura debe ser PDF, JPEG o PNG.");
        RuleFor(command => command.FileStream).NotNull();
    }
}
