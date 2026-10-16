using System.Threading.RateLimiting;
using RedisRateLimiting;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Reverse proxy: routes and clusters come from the "ReverseProxy" section of appsettings.json.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Allow the React dev servers (Vite / CRA) to call the API.
var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
              ?? new[] { "http://localhost:5173", "http://localhost:3000" };
builder.Services.AddCors(options => options.AddPolicy("frontend", policy =>
    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

var redisOptions = ConfigurationOptions.Parse(builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("ConnectionStrings:Redis is not configured."));
redisOptions.AbortOnConnectFail = false;
redisOptions.BacklogPolicy = BacklogPolicy.FailFast;
var redis = ConnectionMultiplexer.Connect(redisOptions);

// Per-IP limits applied to routes via "RateLimiterPolicy". Counters live in Redis so every gateway replica
// shares them; while Redis is unreachable each replica falls back to its own in-memory counter.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("per-ip", httpContext => PerIpLimit(httpContext, "per-ip", permitsPerMinute: 100));
    options.AddPolicy("payment-webhook", httpContext => PerIpLimit(httpContext, "payment-webhook", permitsPerMinute: 60));
});

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseCors("frontend");
app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { service = "Sneaker Shop API Gateway", status = "running" }));
app.MapReverseProxy();

app.Run();

RateLimitPartition<string> PerIpLimit(HttpContext httpContext, string policy, int permitsPerMinute)
{
    var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var window = TimeSpan.FromMinutes(1);

    return redis.IsConnected
        ? RedisRateLimitPartition.GetFixedWindowRateLimiter($"gateway:v1:{policy}:{ip}", _ =>
            new RedisFixedWindowRateLimiterOptions
            {
                ConnectionMultiplexerFactory = () => redis,
                PermitLimit = permitsPerMinute,
                Window = window
            })
        : RateLimitPartition.GetFixedWindowLimiter($"local:{policy}:{ip}", _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitsPerMinute,
                Window = window,
                QueueLimit = 0
            });
}
