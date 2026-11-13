using FluentValidation;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

namespace LiquiLabs.Vankoo.Invoicing.Application.Queries.ListMyInvoices;

public sealed class ListMyInvoicesQueryValidator : AbstractValidator<ListMyInvoicesQuery>
{
    public ListMyInvoicesQueryValidator()
    {
        RuleFor(query => query.MypeId).NotEmpty();
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, ListMyInvoicesQuery.MaxPageSize);
        RuleFor(query => query.Status)
            .Must(status => Enum.TryParse<InvoiceStatus>(status, ignoreCase: false, out _))
            .When(query => !string.IsNullOrWhiteSpace(query.Status))
            .WithMessage("El estado no es uno de los de InvoiceStatus.");
    }
}
