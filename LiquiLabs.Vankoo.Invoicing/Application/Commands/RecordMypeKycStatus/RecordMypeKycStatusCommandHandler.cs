using LiquiLabs.Vankoo.Invoicing.Domain.Entities;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.RecordMypeKycStatus;

public sealed class RecordMypeKycStatusCommandHandler : IRequestHandler<RecordMypeKycStatusCommand>
{
    private readonly IMypeVerificationRepository _repository;

    public RecordMypeKycStatusCommandHandler(IMypeVerificationRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(RecordMypeKycStatusCommand command, CancellationToken cancellationToken)
    {
        var verification = MypeVerification.Record(
            MypeId.Of(command.MypeId),
            command.CompanyId,
            command.Status,
            command.StatusChangedAtUtc);

        await _repository.SaveIfNewerAsync(verification, cancellationToken);
    }
}
