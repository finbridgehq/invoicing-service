using Confluent.Kafka;
using FluentValidation;
using LiquiLabs.Vankoo.Invoicing.Application.Behaviors;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.RecordMypeKycStatus;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using LiquiLabs.Vankoo.Invoicing.Tests.Acceptance.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Infrastructure;

public sealed class ProfileKycStatusConsumerTests
{
    private const string Verified = """
        {"eventId":"e1","eventType":"CompanyKycStatusChanged","schemaVersion":1,
         "occurredAt":"2026-10-29T18:00:05.000Z","companyId":"50000000-0000-4000-8000-000000000002",
         "userId":"40000000-0000-4000-8000-000000000002","kycStatus":"VERIFIED"}
        """;

    private readonly InMemoryMypeVerificationRepository _repository = new();

    private ProfileKycStatusConsumer Consumer(FakeStream stream, bool enabled = true)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(typeof(RecordMypeKycStatusCommand).Assembly);
            config.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(typeof(RecordMypeKycStatusCommand).Assembly);
        services.AddSingleton<IMypeVerificationRepository>(_repository);
        var handler = new ProfileKycMessageHandler(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ProfileKycMessageHandler>.Instance);
        return new ProfileKycStatusConsumer(
            handler,
            () => stream,
            Options.Create(new KafkaSettings { ProfileKycConsumerEnabled = enabled }),
            NullLogger<ProfileKycStatusConsumer>.Instance);
    }

    [Fact]
    public async Task ConsumeAsync_CommitsEachMessageOnlyAfterApplyingIt()
    {
        var savedWhenCommitted = new List<bool>();
        var stream = new FakeStream(
            [Message(Verified, 0), Message("""{"eventType":"ProfileCompleted"}""", 1)],
            onCommit: _ => savedWhenCommitted.Add(
                _repository.GetByMypeIdAsync(MypeId.Of("40000000-0000-4000-8000-000000000002"))
                    .GetAwaiter().GetResult()?.IsVerified == true));

        await Consumer(stream).ConsumeAsync(stream.Stopping);

        Assert.Equal([0L, 1L], stream.Committed);
        Assert.Equal([true, true], savedWhenCommitted);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task ConsumeAsync_WaitsAndGoesOnWhenTheBrokerDoesNotAnswer()
    {
        var stream = new FakeStream([null, Message(Verified, 5)]);

        await Consumer(stream).ConsumeAsync(stream.Stopping);

        Assert.Equal([5L], stream.Committed);
    }

    [Fact]
    public async Task StartAsync_RunsTheLoopInTheBackground()
    {
        var stream = new FakeStream([Message(Verified, 7)]);
        var consumer = Consumer(stream);

        await consumer.StartAsync(default);
        await (consumer.ExecuteTask ?? Task.CompletedTask);

        Assert.Equal([7L], stream.Committed);
    }

    [Fact]
    public async Task ConsumeAsync_DoesNothingWhenTurnedOff()
    {
        var stream = new FakeStream([Message(Verified, 0)]);

        await Consumer(stream, enabled: false).ConsumeAsync(stream.Stopping);

        Assert.Empty(stream.Committed);
        Assert.False(stream.Disposed);
    }

    private static ProfileEventMessage Message(string value, long offset) =>
        new(value, new TopicPartitionOffset("vankoo.profile.events", new Partition(0), new Offset(offset)));

    private sealed class FakeStream(IEnumerable<ProfileEventMessage?> messages, Action<long>? onCommit = null)
        : IProfileEventStream
    {
        private readonly Queue<ProfileEventMessage?> _messages = new(messages);
        private readonly CancellationTokenSource _stopping = new();

        public CancellationToken Stopping => _stopping.Token;
        public List<long> Committed { get; } = [];
        public bool Disposed { get; private set; }

        public ProfileEventMessage? Next(CancellationToken stoppingToken)
        {
            if (_messages.Count > 0) return _messages.Dequeue();
            // Sin más mensajes, el servicio se detiene: como Consume() de Kafka al cancelarse.
            _stopping.Cancel();
            throw new OperationCanceledException(_stopping.Token);
        }

        public void Commit(ProfileEventMessage message)
        {
            onCommit?.Invoke(message.Position.Offset.Value);
            Committed.Add(message.Position.Offset.Value);
        }

        public void Dispose() => Disposed = true;
    }
}
