using System.Collections.Concurrent;
using MassTransit;

namespace Ordering.Api.Tests;

public class FaultCaptureConsumer<T> : IConsumer<Fault<T>> where T : class
{
    public static ConcurrentQueue<string> Errors { get; } = new();

    public Task Consume(ConsumeContext<Fault<T>> context)
    {
        foreach (var exception in context.Message.Exceptions)
            Errors.Enqueue($"{typeof(T).Name}: {exception.Message}");
        return Task.CompletedTask;
    }
}
