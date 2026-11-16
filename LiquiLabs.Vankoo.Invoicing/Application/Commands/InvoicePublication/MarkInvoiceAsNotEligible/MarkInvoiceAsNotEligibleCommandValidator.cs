using FluentValidation;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.InvoicePublication.MarkInvoiceAsNotEligible;

public sealed class MarkInvoiceAsNotEligibleCommandValidator : AbstractValidator<MarkInvoiceAsNotEligibleCommand>
{
    public MarkInvoiceAsNotEligibleCommandValidator()
    {
        RuleFor(command => command.InvoiceId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("El InvoiceId debe ser un UUID válido.");
    }
}
