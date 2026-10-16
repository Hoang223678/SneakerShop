using Ordering.Api.Dtos;
using SneakerShop.BuildingBlocks;
using SneakerShop.BuildingBlocks.Caching;
using SneakerShop.Contracts;
using StackExchange.Redis;

namespace Ordering.Api.Services;

/// <summary>
/// Sheds excess checkout requests during a flash sale before they reach the database.
/// It is only a gate: Inventory's reservation still decides who actually gets a pair.
/// </summary>
public interface IFlashSaleGate
{
    /// <summary>Takes the requested pairs from every running flash sale in the order, or returns null when one is sold out.</summary>
    Task<FlashSaleTicket?> TryAdmitAsync(IReadOnlyList<OrderLine> lines);

    /// <summary>Gives the pairs back when the order was not placed.</summary>
    Task ReleaseAsync(FlashSaleTicket ticket);
}

public interface IFlashSaleAdmin
{
    Task<FlashSaleStatus> StartAsync(Guid variantId, int quantity, DateTimeOffset endsAt);
    Task<FlashSaleStatus?> GetAsync(Guid variantId);
    Task<bool> EndAsync(Guid variantId);
}

public sealed record FlashSaleTicket(IReadOnlyList<OrderLine> Taken)
{
    public static readonly FlashSaleTicket None = new([]);
}

/// <summary>
/// One counter per variant, expiring when the sale ends. Keys share the {flash-sale} hash tag so a
/// multi-line order can be checked and decremented in one script on Redis Cluster too.
/// </summary>
public sealed class RedisFlashSaleService(IConnectionMultiplexer redis, ILogger<RedisFlashSaleService> logger)
    : IFlashSaleGate, IFlashSaleAdmin
{
    // Returns -1 when any running sale lacks pairs (nothing is taken), otherwise how many counters were decremented.
    private const string TakeScript = """
        for i, key in ipairs(KEYS) do
          local left = redis.call('GET', key)
          if left and tonumber(left) < tonumber(ARGV[i]) then return -1 end
        end
        local taken = 0
        for i, key in ipairs(KEYS) do
          if redis.call('EXISTS', key) == 1 then
            redis.call('DECRBY', key, ARGV[i])
            taken = taken + 1
          end
        end
        return taken
        """;

    // Only refills counters that still exist, so an ended sale is never recreated without a TTL.
    private const string ReleaseScript = """
        for i, key in ipairs(KEYS) do
          if redis.call('EXISTS', key) == 1 then redis.call('INCRBY', key, ARGV[i]) end
        end
        return 1
        """;

    private static RedisKey Key(Guid variantId) => $"ordering:v1:{{flash-sale}}:{variantId}";

    public async Task<FlashSaleTicket?> TryAdmitAsync(IReadOnlyList<OrderLine> lines)
    {
        try
        {
            var taken = (long)await redis.GetDatabase().ScriptEvaluateAsync(
                TakeScript,
                lines.Select(l => Key(l.VariantId)).ToArray(),
                lines.Select(l => (RedisValue)l.Quantity).ToArray());
            return taken switch
            {
                < 0 => null,
                0 => FlashSaleTicket.None,
                _ => new FlashSaleTicket(lines)
            };
        }
        catch (Exception exception) when (exception.IsRedisFailure())
        {
            logger.LogWarning(exception, "Flash-sale gate skipped because Redis is unavailable; Inventory still enforces stock.");
            return FlashSaleTicket.None;
        }
    }

    public async Task ReleaseAsync(FlashSaleTicket ticket)
    {
        if (ticket.Taken.Count == 0) return;
        try
        {
            await redis.GetDatabase().ScriptEvaluateAsync(
                ReleaseScript,
                ticket.Taken.Select(l => Key(l.VariantId)).ToArray(),
                ticket.Taken.Select(l => (RedisValue)l.Quantity).ToArray());
        }
        catch (Exception exception) when (exception.IsRedisFailure())
        {
            logger.LogWarning(exception, "Could not return flash-sale pairs because Redis is unavailable.");
        }
    }

    public Task<FlashSaleStatus> StartAsync(Guid variantId, int quantity, DateTimeOffset endsAt) => RunAsync(async db =>
    {
        await db.StringSetAsync(Key(variantId), quantity, endsAt - DateTimeOffset.UtcNow);
        return new FlashSaleStatus(variantId, quantity, endsAt.UtcDateTime);
    });

    public Task<FlashSaleStatus?> GetAsync(Guid variantId) => RunAsync(async db =>
    {
        var left = await db.StringGetAsync(Key(variantId));
        var ttl = await db.KeyTimeToLiveAsync(Key(variantId));
        return left.IsNull || ttl is null
            ? null
            : new FlashSaleStatus(variantId, Math.Max(0, (int)left), DateTime.UtcNow.Add(ttl.Value));
    });

    public Task<bool> EndAsync(Guid variantId) => RunAsync(db => db.KeyDeleteAsync(Key(variantId)));

    private async Task<T> RunAsync<T>(Func<IDatabase, Task<T>> action)
    {
        try
        {
            return await action(redis.GetDatabase());
        }
        catch (Exception exception) when (exception.IsRedisFailure())
        {
            throw new AppException(StatusCodes.Status503ServiceUnavailable, "Flash sales are temporarily unavailable.");
        }
    }
}
