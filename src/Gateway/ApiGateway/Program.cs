using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Reverse proxy: routes and clusters come from the "ReverseProxy" section of appsettings.json.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Allow the React dev servers (Vite / CRA) to call the API.
var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
              ?? new[] { "http://localhost:5173", "http://localhost:3000" };
builder.Services.AddCors(options => options.AddPolicy("frontend", policy =>
    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

// Simple per-IP rate limit (100 requests / minute). Applied to routes via "RateLimiterPolicy": "per-ip".
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("per-ip", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy("payment-webhook", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseCors("frontend");
app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { service = "Sneaker Shop API Gateway", status = "running" }));
app.MapReverseProxy();

app.Run();
