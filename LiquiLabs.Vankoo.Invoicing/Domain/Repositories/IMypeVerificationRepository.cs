using LiquiLabs.Vankoo.Invoicing.Domain.Entities;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

namespace LiquiLabs.Vankoo.Invoicing.Domain.Repositories;

public interface IMypeVerificationRepository
{
    Task<MypeVerification?> GetByMypeIdAsync(MypeId mypeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda el estado solo si cambió después del que ya hay: un mensaje repetido o
    /// atrasado no deshace uno más nuevo.
    /// </summary>
    Task SaveIfNewerAsync(MypeVerification verification, CancellationToken cancellationToken = default);
}
