using LiquiLabs.Vankoo.Invoicing.Shared.Domain.Exceptions;

namespace LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;

public sealed class MypeNotVerifiedException : ForbiddenOperationException
{
    public MypeNotVerifiedException()
        : base("MYPE_NOT_VERIFIED",
               "The MYPE must be verified (KYC) before uploading invoices")
    { }
}
