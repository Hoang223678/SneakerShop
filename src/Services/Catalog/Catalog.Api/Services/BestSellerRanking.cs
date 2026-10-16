using System.Globalization;
using SneakerShop.BuildingBlocks.Caching;
using StackExchange.Redis;

namespace Catalog.Api.Services;

public interface IBestSellerRanking
{
    /// <summary>Adds sold pairs per sneaker. Counting the same order twice is a no-op.</summary>
    Task RecordSaleAsync(Guid orderId, IReadOnlyDictionary<Guid, int> pairsBySneaker, DateTime soldAtUtc);

    /// <summary>Sneaker ids with the most pairs sold over the last <paramref name="days"/> days, best first.</summary>
    Task<IReadOnlyList<Guid>> TopAsync(int days, int take);
}

/// <summary>One Redis sorted set per UTC day (ZINCRBY per sale); a ranking window is the ZUNION of its days.</summary>
public sealed class RedisBestSellerRanking(IConnectionMultiplexer redis, ILogger<RedisBestSellerRanking> logger)
    : IBestSellerRanking
{
    public const int MaxDays = 30;
    private static readonly TimeSpan Retention = TimeSpan.FromDays(MaxDays + 1);

    private static RedisKey DayKey(DateTime day) =>
        $"catalog:v1:rank:day:{day.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}";

    private static RedisKey CountedKey(Guid orderId) => $"catalog:v1:rank:counted:{orderId}";

    public async Task RecordSaleAsync(Guid orderId, IReadOnlyDictionary<Guid, int> pairsBySneaker, DateTime soldAtUtc)
    {
        var dayKey = DayKey(soldAtUtc);
        var transaction = redis.GetDatabase().CreateTransaction();
        transaction.AddCondition(Condition.KeyNotExists(CountedKey(orderId)));
        _ = transaction.StringSetAsync(CountedKey(orderId), 1, Retention);
        foreach (var (sneakerId, pairs) in pairsBySneaker)
            _ = transaction.SortedSetIncrementAsync(dayKey, sneakerId.ToString(), pairs);
        _ = transaction.KeyExpireAsync(dayKey, Retention);
        await transaction.ExecuteAsync();
    }

    public async Task<IReadOnlyList<Guid>> TopAsync(int days, int take)
    {
        var today = DateTime.UtcNow.Date;
        var keys = Enumerable.Range(0, days).Select(offset => DayKey(today.AddDays(-offset))).ToArray();
        try
        {
            var entries = await redis.GetDatabase().SortedSetCombineWithScoresAsync(SetOperation.Union, keys);
            return entries
                .OrderByDescending(entry => entry.Score)
                .Take(take)
                .Select(entry => Guid.Parse(entry.Element.ToString()))
                .ToList();
        }
        catch (Exception exception) when (exception.IsRedisFailure())
        {
            logger.LogWarning(exception, "Best-seller ranking is unavailable because Redis cannot be reached.");
            return [];
        }
    }
}
