using MassTransit;
using Microsoft.Extensions.Logging;

namespace SneakerShop.BuildingBlocks.Consumers;

public class FaultAuditConsumer<T>(ILogger<FaultAuditConsumer<T>> logger) : IConsumer<Fault<T>>
    where T : class
{
    public Task Consume(ConsumeContext<Fault<T>> context)
    {
        var exceptions = string.Join("; ", context.Message.Exceptions.Select(x => x.Message));
        logger.LogError(
            "Message {MessageType} failed permanently. FaultId={FaultId}, Exceptions={Exceptions}",
            typeof(T).Name, context.Message.FaultId, exceptions);
        return Task.CompletedTask;
    }
}

public static class FaultAuditRegistration
{
    /// <summary>
    /// Registers <see cref="FaultAuditConsumer{T}"/> on its own "{service}-{message}-fault" queue. The default name
    /// would be the message name (e.g. "order-created"), which is the queue of the real consumer in another
    /// service: both services would then compete for it and the one without a handler would skip the message.
    /// </summary>
    public static void AddFaultAudit<T>(this IBusRegistrationConfigurator x, string serviceName) where T : class =>
        x.AddConsumer<FaultAuditConsumer<T>>()
            .Endpoint(e => e.Name =
                $"{serviceName}-{KebabCaseEndpointNameFormatter.Instance.SanitizeName(typeof(T).Name)}-fault");
}
