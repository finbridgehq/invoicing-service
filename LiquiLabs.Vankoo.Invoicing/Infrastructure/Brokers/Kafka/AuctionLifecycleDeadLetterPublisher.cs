using System.Text;
using Confluent.Kafka;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using Microsoft.Extensions.Options;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;

public sealed class AuctionLifecycleDeadLetterPublisher : IAuctionLifecycleDeadLetterPublisher, IDisposable
{
    private readonly IProducer<string?, string> _producer;
    private readonly string _deadLetterTopic;

    public AuctionLifecycleDeadLetterPublisher(
        IProducer<string?, string> producer,
        IOptions<AuctionLifecycleConsumerSettings> settings)
    {
        _producer = producer;
        _deadLetterTopic = settings.Value.DeadLetterTopic;
    }

    public async Task PublishAsync(
        ConsumeResult<string?, string> original,
        Exception error,
        CancellationToken cancellationToken)
    {
        var headers = new Headers();
        foreach (var header in original.Message.Headers)
            headers.Add(header.Key, header.GetValueBytes());

        headers.Add("x-original-topic", Encoding.UTF8.GetBytes(original.Topic));
        headers.Add("x-original-partition", Encoding.UTF8.GetBytes(original.Partition.Value.ToString()));
        headers.Add("x-original-offset", Encoding.UTF8.GetBytes(original.Offset.Value.ToString()));
        headers.Add("x-error", Encoding.UTF8.GetBytes(error.Message));

        await _producer.ProduceAsync(
            _deadLetterTopic,
            new Message<string?, string>
            {
                Key = original.Message.Key,
                Value = original.Message.Value,
                Headers = headers
            },
            cancellationToken);
    }

    public void Dispose() => _producer.Dispose();
}
