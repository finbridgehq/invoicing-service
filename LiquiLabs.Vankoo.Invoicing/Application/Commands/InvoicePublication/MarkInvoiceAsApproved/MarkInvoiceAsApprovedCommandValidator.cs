using FluentValidation;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.InvoicePublication.MarkInvoiceAsApproved;

public sealed class MarkInvoiceAsApprovedCommandValidator : AbstractValidator<MarkInvoiceAsApprovedCommand>
{
    public MarkInvoiceAsApprovedCommandValidator()
    {
        RuleFor(command => command.InvoiceId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("El InvoiceId debe ser un UUID válido.");
    }
}
