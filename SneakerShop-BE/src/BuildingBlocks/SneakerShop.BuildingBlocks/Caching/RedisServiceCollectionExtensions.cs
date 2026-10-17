using System.Text.Json;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace SneakerShop.BuildingBlocks.Caching;

public static class RedisServiceCollectionExtensions
{
    /// <summary>One shared Redis connection per process. Commands fail fast while Redis is down instead of queueing.</summary>
    public static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration config)
    {
        services.TryAddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(config.GetConnectionString("Redis")
                ?? throw new InvalidOperationException("ConnectionStrings:Redis is not configured."));
            options.AbortOnConnectFail = false;
            options.BacklogPolicy = BacklogPolicy.FailFast;
            return ConnectionMultiplexer.Connect(options);
        });
        services.TryAddSingleton<ITokenRevocationStore, RedisTokenRevocationStore>();
        return services;
    }

    /// <summary>
    /// FusionCache: in-process memory (L1) + Redis (L2). The Redis pub/sub backplane tells every other
    /// instance to drop its L1 copy when an entry is changed or removed, so replicas never disagree for long.
    /// Every entry gets <paramref name="defaultDuration"/> unless the caller passes its own, so nothing is stored without a TTL.
    /// </summary>
    public static IServiceCollection AddAppCache(
        this IServiceCollection services, IConfiguration config, string serviceName, TimeSpan defaultDuration)
    {
        services.AddRedis(config);

        services.AddFusionCache()
            .WithOptions(options =>
            {
                options.CacheKeyPrefix = $"{serviceName}:";
                options.BackplaneChannelPrefix = serviceName;
                options.DistributedCacheCircuitBreakerDuration = TimeSpan.FromSeconds(5);
                options.BackplaneCircuitBreakerDuration = TimeSpan.FromSeconds(5);
            })
            .WithDefaultEntryOptions(options =>
            {
                options.Duration = defaultDuration;
                options.DistributedCacheHardTimeout = TimeSpan.FromSeconds(1);
            })
            .WithSerializer(new FusionCacheSystemTextJsonSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web)))
            .WithDistributedCache(sp => new RedisCache(Options.Create(new RedisCacheOptions
            {
                ConnectionMultiplexerFactory = () => Task.FromResult(sp.GetRequiredService<IConnectionMultiplexer>())
            })))
            .WithBackplane(sp => new RedisBackplane(Options.Create(new RedisBackplaneOptions
            {
                ConnectionMultiplexerFactory = () => Task.FromResult(sp.GetRequiredService<IConnectionMultiplexer>())
            })));

        return services;
    }

    public static bool IsRedisFailure(this Exception exception) =>
        exception is RedisException or RedisTimeoutException;
}
