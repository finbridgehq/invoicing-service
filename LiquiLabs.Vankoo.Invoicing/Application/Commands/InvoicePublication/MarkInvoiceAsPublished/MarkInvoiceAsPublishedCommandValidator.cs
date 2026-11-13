using FluentValidation;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.InvoicePublication.MarkInvoiceAsPublished;

public sealed class MarkInvoiceAsPublishedCommandValidator : AbstractValidator<MarkInvoiceAsPublishedCommand>
{
    public MarkInvoiceAsPublishedCommandValidator()
    {
        RuleFor(command => command.InvoiceId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("El InvoiceId debe ser un UUID válido.");
    }
}
