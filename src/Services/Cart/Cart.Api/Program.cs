using Cart.Api.Consumers;
using Cart.Api.Services;
using SneakerShop.BuildingBlocks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ICartStore, RedisCartStore>();

// Typed HTTP client for the Catalog service's internal endpoint.
builder.Services.AddHttpClient<ICatalogClient, CatalogClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Catalog:BaseUrl"]
        ?? throw new InvalidOperationException("Services:Catalog:BaseUrl is not configured."));
    client.DefaultRequestHeaders.Add(InternalApiKeyAttribute.HeaderName, builder.Configuration["Internal:ApiKey"] ?? "");
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddJwtAuth(builder.Configuration);
builder.Services.AddMessaging(builder.Configuration, x => x.AddConsumer<CartCleanupConsumer>());
builder.Services.AddApiDefaults();
builder.Services.AddSwaggerWithJwt("Sneaker Shop - Cart API");
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

app.Run();
