using FluentValidation;
using LiquiLabs.Vankoo.Invoicing.Application.Behaviors;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.RecordMypeKycStatus;
using LiquiLabs.Vankoo.Invoicing.Domain.Entities;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;
using LiquiLabs.Vankoo.Invoicing.Tests.Acceptance.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Infrastructure;

public sealed class ProfileKycMessageHandlerTests
{
    private const string MypeIdValue = "40000000-0000-4000-8000-000000000002";
    private const string Changed = """
        {"eventId":"e1","eventType":"CompanyKycStatusChanged","schemaVersion":1,
         "occurredAt":"2026-10-29T18:00:05.000Z","companyId":"50000000-0000-4000-8000-000000000002",
         "userId":"40000000-0000-4000-8000-000000000002","kycStatus":"VERIFIED"}
        """;

    private readonly List<TimeSpan> _delays = [];

    private ProfileKycMessageHandler Handler(IMypeVerificationRepository repository)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(typeof(RecordMypeKycStatusCommand).Assembly);
            config.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(typeof(RecordMypeKycStatusCommand).Assembly);
        services.AddSingleton(repository);
        var provider = services.BuildServiceProvider();
        return new ProfileKycMessageHandler(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ProfileKycMessageHandler>.Instance,
            (delay, _) =>
            {
                _delays.Add(delay);
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task ApplyUntilDone_StoresTheStatusOfACompanyKycStatusChanged()
    {
        var repository = new InMemoryMypeVerificationRepository();

        await Handler(repository).ApplyUntilDoneAsync(Changed, "p-0@1", default);

        var verification = await repository.GetByMypeIdAsync(MypeId.Of(MypeIdValue));
        Assert.True(verification?.IsVerified);
        Assert.Equal("50000000-0000-4000-8000-000000000002", verification?.CompanyId);
    }

    [Theory]
    [InlineData("""{"eventType":"SustainabilityCertificateSubmitted","schemaVersion":1}""")]
    [InlineData("{not json")]
    [InlineData(null)]
    public async Task ApplyUntilDone_SkipsOtherEventsAndInvalidMessages(string? value)
    {
        var repository = new FailingRepository(failures: int.MaxValue);

        await Handler(repository).ApplyUntilDoneAsync(value, "p-0@2", default);

        Assert.Equal(0, repository.Calls);
        Assert.Empty(_delays);
    }

    [Fact]
    public async Task ApplyUntilDone_RetriesWithAGrowingWaitUntilTheStatusIsSaved()
    {
        var repository = new FailingRepository(failures: 8);

        await Handler(repository).ApplyUntilDoneAsync(Changed, "p-0@3", default);

        Assert.Equal(9, repository.Calls);
        Assert.Equal(
            [1, 2, 4, 8, 16, 32, 60, 60],
            _delays.Select(delay => (int)delay.TotalSeconds));
    }

    private sealed class FailingRepository(int failures) : IMypeVerificationRepository
    {
        public int Calls { get; private set; }

        public Task<MypeVerification?> GetByMypeIdAsync(MypeId mypeId, CancellationToken cancellationToken = default)
            => Task.FromResult<MypeVerification?>(null);

        public Task SaveIfNewerAsync(MypeVerification verification, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Calls <= failures
                ? throw new InvalidOperationException("Mongo is down")
                : Task.CompletedTask;
        }
    }
}
