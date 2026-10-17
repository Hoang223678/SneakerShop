using SneakerShop.BuildingBlocks;
using SneakerShop.BuildingBlocks.Caching;
using StackExchange.Redis;

namespace Cart.Api.Services;

public interface ICartStore
{
    /// <summary>Variant id to quantity.</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetAsync(Guid userId);

    /// <summary>Returns false when the variant is new and the cart already holds <see cref="RedisCartStore.MaxLines"/> lines.</summary>
    Task<bool> SetQuantityAsync(Guid userId, Guid variantId, int quantity);

    /// <summary>
    /// Adds quantities onto existing lines. Returns 1 on success, 0 when a new line would exceed
    /// <see cref="RedisCartStore.MaxLines"/>, or -1 when a line would exceed <see cref="RedisCartStore.MaxQuantity"/>.
    /// </summary>
    Task<long> AddQuantitiesAsync(Guid userId, IReadOnlyDictionary<Guid, int> additions);

    Task RemoveAsync(Guid userId, IEnumerable<Guid> variantIds);
    Task ClearAsync(Guid userId);
}

/// <summary>
/// Redis is the system of record for carts: one hash per user (variant id to quantity) that expires
/// <see cref="Lifetime"/> after the last change.
/// </summary>
public sealed class RedisCartStore(IConnectionMultiplexer redis) : ICartStore
{
    public const int MaxLines = 50;
    public const int MaxQuantity = 1_000_000;
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    // Line-count check, write and TTL refresh in one script so concurrent requests cannot exceed the limit.
    private const string SetLineScript = """
        if redis.call('HEXISTS', KEYS[1], ARGV[1]) == 0 and redis.call('HLEN', KEYS[1]) >= tonumber(ARGV[3]) then
          return 0
        end
        redis.call('HSET', KEYS[1], ARGV[1], ARGV[2])
        redis.call('PEXPIRE', KEYS[1], ARGV[4])
        return 1
        """;

    // ARGV: maxLines, ttlMs, maxQty, then repeating variantId, quantity pairs.
    private const string AddLinesScript = """
        local maxLines = tonumber(ARGV[1])
        local ttl = ARGV[2]
        local maxQty = tonumber(ARGV[3])
        local newLines = 0
        for i = 4, #ARGV, 2 do
          if redis.call('HEXISTS', KEYS[1], ARGV[i]) == 0 then
            newLines = newLines + 1
          end
        end
        if redis.call('HLEN', KEYS[1]) + newLines > maxLines then
          return 0
        end
        for i = 4, #ARGV, 2 do
          local current = tonumber(redis.call('HGET', KEYS[1], ARGV[i]) or '0')
          local next = current + tonumber(ARGV[i + 1])
          if next > maxQty then
            return -1
          end
        end
        for i = 4, #ARGV, 2 do
          local current = tonumber(redis.call('HGET', KEYS[1], ARGV[i]) or '0')
          redis.call('HSET', KEYS[1], ARGV[i], current + tonumber(ARGV[i + 1]))
        end
        redis.call('PEXPIRE', KEYS[1], ttl)
        return 1
        """;

    private static RedisKey Key(Guid userId) => $"cart:v1:{userId}";

    public Task<IReadOnlyDictionary<Guid, int>> GetAsync(Guid userId) => RunAsync<IReadOnlyDictionary<Guid, int>>(async db =>
    {
        var entries = await db.HashGetAllAsync(Key(userId));
        var lines = new Dictionary<Guid, int>(entries.Length);
        foreach (var entry in entries)
        {
            if (Guid.TryParse(entry.Name.ToString(), out var variantId) && entry.Value.TryParse(out int quantity))
                lines[variantId] = quantity;
        }
        return lines;
    });

    public Task<bool> SetQuantityAsync(Guid userId, Guid variantId, int quantity) => RunAsync(async db =>
        (long)await db.ScriptEvaluateAsync(
            SetLineScript,
            [Key(userId)],
            [variantId.ToString(), quantity, MaxLines, (long)Lifetime.TotalMilliseconds]) == 1);

    public Task<long> AddQuantitiesAsync(Guid userId, IReadOnlyDictionary<Guid, int> additions) => RunAsync(async db =>
    {
        if (additions.Count == 0) return 1L;
        var args = new List<RedisValue>(3 + additions.Count * 2)
        {
            MaxLines,
            (long)Lifetime.TotalMilliseconds,
            MaxQuantity
        };
        foreach (var line in additions)
        {
            args.Add(line.Key.ToString());
            args.Add(line.Value);
        }

        return (long)await db.ScriptEvaluateAsync(AddLinesScript, [Key(userId)], [.. args]);
    });

    public Task RemoveAsync(Guid userId, IEnumerable<Guid> variantIds) => RunAsync(db =>
        db.HashDeleteAsync(Key(userId), variantIds.Select(id => (RedisValue)id.ToString()).ToArray()));

    public Task ClearAsync(Guid userId) => RunAsync(db => db.KeyDeleteAsync(Key(userId)));

    private async Task<T> RunAsync<T>(Func<IDatabase, Task<T>> action)
    {
        try
        {
            return await action(redis.GetDatabase());
        }
        catch (Exception exception) when (exception.IsRedisFailure())
        {
            throw new AppException(StatusCodes.Status503ServiceUnavailable, "The cart is temporarily unavailable. Please try again.");
        }
    }
}
