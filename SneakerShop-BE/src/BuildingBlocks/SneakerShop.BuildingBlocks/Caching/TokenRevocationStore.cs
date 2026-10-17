using StackExchange.Redis;

namespace SneakerShop.BuildingBlocks.Caching;

/// <summary>Revoked JWT ids (jti). Identity writes them on logout; every service checks them while validating tokens.</summary>
public interface ITokenRevocationStore
{
    Task RevokeAsync(string jti, DateTime expiresAtUtc);
    Task<bool> IsRevokedAsync(string jti);
}

public sealed class RedisTokenRevocationStore(IConnectionMultiplexer redis) : ITokenRevocationStore
{
    // JWT validation accepts tokens up to 30 seconds past "exp", so keep the entry a little longer than that.
    private static readonly TimeSpan ClockSkewMargin = TimeSpan.FromMinutes(1);

    private static RedisKey Key(string jti) => $"auth:v1:revoked:{jti}";

    public async Task RevokeAsync(string jti, DateTime expiresAtUtc)
    {
        var ttl = expiresAtUtc - DateTime.UtcNow + ClockSkewMargin;
        if (ttl <= TimeSpan.Zero) return;
        await redis.GetDatabase().StringSetAsync(Key(jti), 1, ttl);
    }

    public Task<bool> IsRevokedAsync(string jti) => redis.GetDatabase().KeyExistsAsync(Key(jti));
}
