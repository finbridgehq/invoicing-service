using System.Text;
using Confluent.Kafka;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using Microsoft.Extensions.Options;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Infrastructure.Kafka;

public sealed class AuctionLifecycleDeadLetterPublisherTests
{
    private const string DeadLetterTopic = "invoicing.auction-lifecycle.dlq";

    private static (AuctionLifecycleDeadLetterPublisher Publisher, RecordingProxy Producer) CreatePublisher()
    {
        var (instance, proxy) = RecordingProxy.Create<IProducer<string?, string>>();
        var publisher = new AuctionLifecycleDeadLetterPublisher(
            instance,
            Options.Create(new AuctionLifecycleConsumerSettings { DeadLetterTopic = DeadLetterTopic }));
        return (publisher, proxy);
    }

    [Fact]
    public async Task PublishAsync_ForwardsTheOriginalMessageWithErrorDetailsToTheDeadLetterTopic()
    {
        var (publisher, producer) = CreatePublisher();
        var original = AuctionMessages.Event("AuctionPublished", Guid.NewGuid().ToString(), offset: 9);

        await publisher.PublishAsync(original, new InvalidOperationException("boom"), CancellationToken.None);

        var call = Assert.Single(producer.ArgumentsOf("ProduceAsync"));
        Assert.Equal(DeadLetterTopic, call[0]);
        var message = Assert.IsType<Message<string?, string>>(call[1]);
        Assert.Equal(original.Message.Key, message.Key);
        Assert.Equal(original.Message.Value, message.Value);
        Assert.Equal("AuctionPublished", HeaderText(message, "eventType"));
        Assert.Equal(AuctionMessages.Topic, HeaderText(message, "x-original-topic"));
        Assert.Equal("0", HeaderText(message, "x-original-partition"));
        Assert.Equal("9", HeaderText(message, "x-original-offset"));
        Assert.Equal("boom", HeaderText(message, "x-error"));
    }

    [Fact]
    public void Dispose_DisposesTheUnderlyingProducer()
    {
        var (publisher, producer) = CreatePublisher();

        publisher.Dispose();

        Assert.Single(producer.ArgumentsOf("Dispose"));
    }

    private static string HeaderText(Message<string?, string> message, string key)
        => Encoding.UTF8.GetString(message.Headers.GetLastBytes(key));
}
