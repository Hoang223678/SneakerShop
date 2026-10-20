using Inventory.Api.Caching;
using Inventory.Api.Data;
using Inventory.Api.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using SneakerShop.BuildingBlocks;
using SneakerShop.BuildingBlocks.Caching;
using SneakerShop.BuildingBlocks.Consumers;
using SneakerShop.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<BearerForwardingHandler>();
builder.Services.AddHttpClient<ICatalogVariantClient, CatalogVariantClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Catalog:BaseUrl"]
        ?? throw new InvalidOperationException("Services:Catalog:BaseUrl is not configured."));
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<BearerForwardingHandler>();

builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default"),
        sql => sql.EnableRetryOnFailure()));
builder.Services.AddSingleton<ReservationExpiryService>();
builder.Services.AddHostedService(services => services.GetRequiredService<ReservationExpiryService>());
builder.Services.AddAppCache(builder.Configuration, "inventory", InventoryCacheKeys.AvailabilityDuration);

builder.Services.AddJwtAuth(builder.Configuration);
builder.Services.AddMessaging(builder.Configuration, x =>
{
    x.AddConsumers(typeof(Program).Assembly);
    x.AddFaultAudit<OrderCreated>("inventory");
    x.AddFaultAudit<OrderCancelled>("inventory");
    x.AddFaultAudit<OrderShipped>("inventory");
    x.AddFaultAudit<OrderPaid>("inventory");
    x.AddFaultAudit<StockReserved>("inventory");
    x.AddFaultAudit<StockReservationFailed>("inventory");
    x.AddFaultAudit<SneakerVariantCreated>("inventory");
    x.AddFaultAudit<SneakerVariantUpdated>("inventory");
    x.AddFaultAudit<SneakerVariantDeleted>("inventory");
    x.AddEntityFrameworkOutbox<InventoryDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();
    });
    x.AddConfigureEndpointsCallback((context, _, endpoint) =>
        endpoint.UseEntityFrameworkOutbox<InventoryDbContext>(context));
});
builder.Services.AddApiDefaults();
builder.Services.AddSwaggerWithJwt("Sneaker Shop - Inventory API");
builder.Services.AddControllers().AddApiJson();
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
}

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseCors();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

await StartupRetry.RunAsync(() => DbInitializer.InitializeAsync(app.Services), app.Logger);

app.Run();
