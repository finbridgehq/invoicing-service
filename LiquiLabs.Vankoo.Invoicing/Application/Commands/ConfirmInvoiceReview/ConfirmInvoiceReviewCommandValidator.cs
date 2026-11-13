using FluentValidation;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.ConfirmInvoiceReview;

public sealed class ConfirmInvoiceReviewCommandValidator : AbstractValidator<ConfirmInvoiceReviewCommand>
{
    public ConfirmInvoiceReviewCommandValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty().WithMessage("El InvoiceId es obligatorio.");
        RuleFor(x => x.MypeId).NotEmpty().WithMessage("El MypeId es obligatorio.");
        RuleFor(x => x.Series).NotEmpty().WithMessage("La serie es obligatoria.");
        RuleFor(x => x.Number).NotEmpty().WithMessage("El número es obligatorio.");
        RuleFor(x => x.DueDate)
            .GreaterThanOrEqualTo(x => x.IssueDate)
            .WithMessage("El vencimiento no puede ser anterior a la emisión.");
        RuleFor(x => x.IssuerRuc).NotEmpty().WithMessage("El RUC del emisor es obligatorio.");
        RuleFor(x => x.PayerRuc).NotEmpty().WithMessage("El RUC del pagador es obligatorio.");
        RuleFor(x => x.Subtotal).GreaterThanOrEqualTo(0).WithMessage("El subtotal no puede ser negativo.");
        RuleFor(x => x.Tax).GreaterThanOrEqualTo(0).WithMessage("El IGV no puede ser negativo.");
        RuleFor(x => x.Total).GreaterThan(0).WithMessage("El total debe ser mayor que cero.");
    }
}
