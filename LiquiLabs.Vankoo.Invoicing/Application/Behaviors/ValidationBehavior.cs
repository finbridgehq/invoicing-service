using FluentValidation;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Behaviors;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);

            // Ejecutar todos los validadores asociados a este comando
            var validationResults = await Task.WhenAll(
                _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

            // Recopilar errores
            var failures = validationResults
                .Where(r => r.Errors.Count > 0)
                .SelectMany(r => r.Errors)
                .ToList();

            if (failures.Count > 0)
            {
                // Si hay errores, lanza una excepción y corta el flujo (nunca llega al Handler)
                throw new ValidationException(failures);
            }
        }

        // Si es que está bien, continúa hacia el Handler
        return await next();
    }
}