using SneakerShop.BuildingBlocks.Caching;
using StackExchange.Redis;

namespace Identity.Api.Services;

public interface ILoginAttemptLimiter
{
    /// <summary>How long the email stays locked, or null when it may try to log in.</summary>
    Task<TimeSpan?> GetLockoutAsync(string email);
    Task RecordFailureAsync(string email);
    Task ResetAsync(string email);
}

/// <summary>
/// Counts failed logins per email in Redis: <see cref="MaxFailures"/> failures within <see cref="Window"/>
/// lock the email until the window expires. Fails open while Redis is unreachable.
/// </summary>
public sealed class RedisLoginAttemptLimiter(IConnectionMultiplexer redis, ILogger<RedisLoginAttemptLimiter> logger)
    : ILoginAttemptLimiter
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    // INCR and PEXPIRE in one script so the counter can never be left without a TTL.
    private const string IncrementWithTtlScript = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then redis.call('PEXPIRE', KEYS[1], ARGV[1]) end
        return count
        """;

    private static RedisKey Key(string email) => $"identity:v1:login-failures:{email}";

    public async Task<TimeSpan?> GetLockoutAsync(string email)
    {
        try
        {
            var db = redis.GetDatabase();
            var failures = (int?)await db.StringGetAsync(Key(email)) ?? 0;
            return failures < MaxFailures ? null : await db.KeyTimeToLiveAsync(Key(email));
        }
        catch (Exception exception) when (exception.IsRedisFailure())
        {
            logger.LogWarning(exception, "Login attempt limit skipped because Redis is unavailable.");
            return null;
        }
    }

    public async Task RecordFailureAsync(string email)
    {
        try
        {
            await redis.GetDatabase().ScriptEvaluateAsync(
                IncrementWithTtlScript, [Key(email)], [(long)Window.TotalMilliseconds]);
        }
        catch (Exception exception) when (exception.IsRedisFailure())
        {
            logger.LogWarning(exception, "Could not record a failed login because Redis is unavailable.");
        }
    }

    public async Task ResetAsync(string email)
    {
        try
        {
            await redis.GetDatabase().KeyDeleteAsync(Key(email));
        }
        catch (Exception exception) when (exception.IsRedisFailure())
        {
            logger.LogWarning(exception, "Could not reset failed logins because Redis is unavailable.");
        }
    }
}
