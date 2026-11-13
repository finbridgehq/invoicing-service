using FluentValidation;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.OcrProcessing.ProcessOcrSynchronously;

public class ProcessOcrSynchronouslyValidator : AbstractValidator<ProcessOcrSynchronouslyCommand>
{
    public ProcessOcrSynchronouslyValidator()
    {
        RuleFor(x => x.InvoiceId)
            .NotEmpty().WithMessage("El InvoiceId es obligatorio.");
    }
}