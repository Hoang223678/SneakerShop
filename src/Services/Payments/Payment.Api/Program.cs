using System.Threading.RateLimiting;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Payment.Api.Consumers;
using Payment.Api.Data;
using Payment.Api.Providers;
using Payment.Api.Services;
using SneakerShop.BuildingBlocks;
using SneakerShop.BuildingBlocks.Consumers;
using SneakerShop.Contracts;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
builder.Services.AddDbContextFactory<PaymentDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
    {
        sql.EnableRetryOnFailure();
        sql.MigrationsHistoryTable("__EFMigrationsHistory_Payment");
    }));
builder.Services.Configure<VnPayOptions>(builder.Configuration.GetSection("Payments:VnPay"));
builder.Services.AddHttpClient<VnPayProvider>();
builder.Services.AddScoped<IPaymentProvider>(sp => sp.GetRequiredService<VnPayProvider>());
builder.Services.AddSingleton<IPaymentProvider, FakePaymentProvider>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddHostedService<PaymentReconciliationService>();
builder.Services.AddHttpClient<IOrderClient, OrderClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Ordering:BaseUrl"]
        ?? throw new InvalidOperationException("Services:Ordering:BaseUrl is not configured."));
    client.DefaultRequestHeaders.Add(
        InternalApiKeyAttribute.HeaderName, builder.Configuration["Internal:ApiKey"] ?? "");
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddJwtAuth(builder.Configuration);
builder.Services.AddMessaging(builder.Configuration, x =>
{
    x.AddConsumer<RefundRequestedConsumer>();
    x.AddConsumer<FaultAuditConsumer<RefundRequested>>();
    x.AddConsumer<FaultAuditConsumer<PaymentSucceeded>>();
    x.AddConsumer<FaultAuditConsumer<PaymentFailed>>();
    x.AddEntityFrameworkOutbox<PaymentDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();
    });
    x.AddConfigureEndpointsCallback((context, _, endpoint) =>
        endpoint.UseEntityFrameworkOutbox<PaymentDbContext>(context));
});
builder.Services.AddApiDefaults();
builder.Services.AddSwaggerWithJwt("Sneaker Shop - Payment API");
builder.Services.AddControllers().AddApiJson();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("payment-webhook", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

await StartupRetry.RunAsync(() => DbInitializer.InitializeAsync(app.Services), app.Logger);
app.Run();
