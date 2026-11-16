using FluentValidation;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.RecordMypeKycStatus;

public sealed class RecordMypeKycStatusCommandValidator : AbstractValidator<RecordMypeKycStatusCommand>
{
    public RecordMypeKycStatusCommandValidator()
    {
        RuleFor(command => command.MypeId).NotEmpty();
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Status).IsInEnum();
    }
}
