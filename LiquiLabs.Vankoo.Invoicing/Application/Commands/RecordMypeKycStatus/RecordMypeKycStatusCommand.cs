using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.RecordMypeKycStatus;

public sealed record RecordMypeKycStatusCommand(
    string MypeId,
    string CompanyId,
    MypeKycStatus Status,
    DateTime StatusChangedAtUtc) : IRequest;
