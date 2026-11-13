using System.Reflection;
using System.Text;
using Confluent.Kafka;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Infrastructure.Kafka;

// IConsumer e IProducer de Confluent tienen unos 40 miembros cada uno; DispatchProxy permite simular
// solo los que usa el código sin implementar el resto ni añadir una librería de mocks.

public class RecordingProxy : DispatchProxy
{
    public List<(string Method, object?[] Args)> Calls { get; } = [];
    public Func<MethodInfo, object?[], (bool Handled, object? Result)>? Handler { get; set; }

    public static (T Instance, RecordingProxy Proxy) Create<T>(
        Func<MethodInfo, object?[], (bool Handled, object? Result)>? handler = null)
        where T : class
    {
        var instance = Create<T, RecordingProxy>();
        var proxy = (RecordingProxy)(object)instance;
        proxy.Handler = handler;
        return (instance, proxy);
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var method = targetMethod!;
        var arguments = args ?? [];
        lock (Calls)
            Calls.Add((method.Name, arguments));

        if (Handler is not null)
        {
            var (handled, result) = Handler(method, arguments);
            if (handled)
                return result;
        }

        return DefaultFor(method.ReturnType);
    }

    private static object? DefaultFor(Type returnType)
    {
        if (returnType == typeof(void))
            return null;
        if (returnType == typeof(Task))
            return Task.CompletedTask;
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var inner = returnType.GetGenericArguments()[0];
            var value = inner.IsValueType ? Activator.CreateInstance(inner) : null;
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(inner).Invoke(null, [value]);
        }

        return returnType.IsValueType ? Activator.CreateInstance(returnType) : null;
    }

    public IReadOnlyList<object?[]> ArgumentsOf(string method)
    {
        lock (Calls)
            return Calls.Where(call => call.Method == method).Select(call => call.Args).ToList();
    }
}

public sealed class FakeConsumer
{
    private readonly Queue<object> _script = new();
    private readonly object _gate = new();

    public IConsumer<string?, string> Instance { get; }
    public RecordingProxy Proxy { get; }

    /// <summary>Se ejecuta una vez cuando se agota el guion; el test lo usa para cancelar el bucle.</summary>
    public Action? OnExhausted { get; set; }

    public FakeConsumer()
    {
        (Instance, Proxy) = RecordingProxy.Create<IConsumer<string?, string>>(Handle);
    }

    public FakeConsumer Enqueue(ConsumeResult<string?, string> result)
    {
        lock (_gate) _script.Enqueue(result);
        return this;
    }

    public FakeConsumer Enqueue(Exception error)
    {
        lock (_gate) _script.Enqueue(error);
        return this;
    }

    public IReadOnlyList<string> SubscribedTopics
        => Proxy.ArgumentsOf("Subscribe").Select(args => (string)args[0]!).ToList();

    public IReadOnlyList<ConsumeResult<string?, string>> Committed
        => Proxy.ArgumentsOf("Commit").Where(args => args is [ConsumeResult<string?, string>])
            .Select(args => (ConsumeResult<string?, string>)args[0]!).ToList();

    public bool Closed => Proxy.ArgumentsOf("Close").Count > 0;
    public bool Disposed => Proxy.ArgumentsOf("Dispose").Count > 0;

    private (bool, object?) Handle(MethodInfo method, object?[] args)
    {
        if (method.Name != "Consume" || args is not [CancellationToken token])
            return (false, null);

        object? next = null;
        lock (_gate)
        {
            if (_script.Count > 0)
                next = _script.Dequeue();
        }

        switch (next)
        {
            case ConsumeResult<string?, string> result:
                return (true, result);
            case Exception error:
                throw error;
        }

        OnExhausted?.Invoke();
        OnExhausted = null;
        token.WaitHandle.WaitOne(TimeSpan.FromSeconds(5));
        token.ThrowIfCancellationRequested();
        return (true, null);
    }
}

public sealed class RecordingDeadLetterPublisher : IAuctionLifecycleDeadLetterPublisher
{
    private int _failuresLeft;

    public List<(ConsumeResult<string?, string> Original, Exception Error)> Published { get; } = [];
    public int Attempts { get; private set; }

    public RecordingDeadLetterPublisher(int failuresBeforeSuccess = 0) => _failuresLeft = failuresBeforeSuccess;

    public Task PublishAsync(ConsumeResult<string?, string> original, Exception error, CancellationToken cancellationToken)
    {
        Attempts++;
        if (_failuresLeft > 0)
        {
            _failuresLeft--;
            throw new InvalidOperationException("DLQ no disponible");
        }

        Published.Add((original, error));
        return Task.CompletedTask;
    }
}

public static class AuctionMessages
{
    public const string Topic = "investment.auction-lifecycle.v1";

    public static ConsumeResult<string?, string> Event(
        string eventType, string invoiceId, long offset = 0, bool withHeader = true)
    {
        var headers = new Headers();
        if (withHeader)
            headers.Add("eventType", Encoding.UTF8.GetBytes(eventType));

        return new ConsumeResult<string?, string>
        {
            Topic = Topic,
            Partition = new Partition(0),
            Offset = new Offset(offset),
            Message = new Message<string?, string>
            {
                Key = "auction-1",
                Value = Envelope(eventType, invoiceId),
                Headers = headers
            }
        };
    }

    public static ConsumeResult<string?, string> Raw(string body, long offset = 0)
        => new()
        {
            Topic = Topic,
            Partition = new Partition(0),
            Offset = new Offset(offset),
            Message = new Message<string?, string> { Key = "auction-1", Value = body, Headers = new Headers() }
        };

    private static string Envelope(string eventType, string invoiceId)
        => $$$"""
           {"eventId":"10000000-0000-0000-0000-000000000001","eventType":"{{{eventType}}}","schemaVersion":1,
            "aggregateId":"20000000-0000-0000-0000-000000000001","sequence":2,"occurredAt":"2026-09-14T16:00:00Z",
            "data":{"invoiceId":"{{{invoiceId}}}","mypeId":"40000000-0000-0000-0000-000000000001"}}
           """;
}
