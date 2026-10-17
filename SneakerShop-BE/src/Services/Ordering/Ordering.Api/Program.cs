using MassTransit;
using Microsoft.EntityFrameworkCore;
using Ordering.Api.Data;
using Ordering.Api.Sagas;
using Ordering.Api.Services;
using SneakerShop.BuildingBlocks;
using SneakerShop.BuildingBlocks.Consumers;
using SneakerShop.Contracts;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
builder.Services.AddDbContextFactory<OrderDbContext>(options =>
    options.UseSqlServer(connectionString,
        sql =>
        {
            sql.EnableRetryOnFailure();
            sql.MigrationsHistoryTable("__EFMigrationsHistory_Ordering");
        }));
builder.Services.AddDbContextFactory<OrderSagaDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
    {
        sql.EnableRetryOnFailure();
        sql.MigrationsHistoryTable("__EFMigrationsHistory_OrderSaga");
    }));
builder.Services.AddHostedService<OrderSagaTimeoutService>();

builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddSingleton<RedisFlashSaleService>();
builder.Services.AddSingleton<IFlashSaleGate>(sp => sp.GetRequiredService<RedisFlashSaleService>());
builder.Services.AddSingleton<IFlashSaleAdmin>(sp => sp.GetRequiredService<RedisFlashSaleService>());

// Typed HTTP client for the Catalog service's internal endpoint.
builder.Services.AddHttpClient<ICatalogClient, CatalogClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Catalog:BaseUrl"]
        ?? throw new InvalidOperationException("Services:Catalog:BaseUrl is not configured."));
    client.DefaultRequestHeaders.Add(InternalApiKeyAttribute.HeaderName, builder.Configuration["Internal:ApiKey"] ?? "");
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddHttpClient<ICartClient, CartClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Cart:BaseUrl"]
        ?? throw new InvalidOperationException("Services:Cart:BaseUrl is not configured."));
    client.DefaultRequestHeaders.Add(InternalApiKeyAttribute.HeaderName, builder.Configuration["Internal:ApiKey"] ?? "");
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddJwtAuth(builder.Configuration);
builder.Services.AddMessaging(builder.Configuration, x =>
{
    x.AddFaultAudit<OrderCreated>("ordering");
    x.AddFaultAudit<StockReserved>("ordering");
    x.AddFaultAudit<StockReservationFailed>("ordering");
    x.AddFaultAudit<OrderReservationTimedOut>("ordering");
    x.AddFaultAudit<OrderReservationExpired>("ordering");
    x.AddFaultAudit<PaymentSucceeded>("ordering");
    x.AddFaultAudit<PaymentFailed>("ordering");
    x.AddFaultAudit<PaymentRefunded>("ordering");
    x.AddFaultAudit<OrderCancelled>("ordering");
    x.AddFaultAudit<OrderShipped>("ordering");
    x.AddEntityFrameworkOutbox<OrderDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();
    });
    x.AddConfigureEndpointsCallback((context, _, endpoint) =>
        endpoint.UseEntityFrameworkOutbox<OrderDbContext>(context));
    x.AddSagaStateMachine<OrderStateMachine, OrderState>()
        .EntityFrameworkRepository(r =>
        {
            r.ConcurrencyMode = ConcurrencyMode.Optimistic;
            r.AddDbContext<DbContext, OrderSagaDbContext>((_, options) =>
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.EnableRetryOnFailure();
                    sql.MigrationsHistoryTable("__EFMigrationsHistory_OrderSaga");
                }));
        });
});
builder.Services.AddApiDefaults();
builder.Services.AddSwaggerWithJwt("Sneaker Shop - Ordering API");
builder.Services.AddControllers().AddApiJson();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

await StartupRetry.RunAsync(() => DbInitializer.InitializeAsync(app.Services), app.Logger);

app.Run();
