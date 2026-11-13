namespace LiquiLabs.Vankoo.Invoicing.Application.Interfaces;

public interface IEventBus
{
    Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
        where T : class;
}