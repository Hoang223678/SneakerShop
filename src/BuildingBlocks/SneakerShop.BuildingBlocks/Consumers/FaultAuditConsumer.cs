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
