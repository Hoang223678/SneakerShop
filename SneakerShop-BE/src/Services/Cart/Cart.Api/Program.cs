using Cart.Api.Consumers;
using Cart.Api.Services;
using SneakerShop.BuildingBlocks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ICartStore, RedisCartStore>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<BearerForwardingHandler>();
builder.Services.AddScoped<ICartCheckoutService, CartCheckoutService>();

// Typed HTTP client for the Catalog service's internal endpoint.
builder.Services.AddHttpClient<ICatalogClient, CatalogClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Catalog:BaseUrl"]
        ?? throw new InvalidOperationException("Services:Catalog:BaseUrl is not configured."));
    client.DefaultRequestHeaders.Add(InternalApiKeyAttribute.HeaderName, builder.Configuration["Internal:ApiKey"] ?? "");
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddHttpClient<IOrderingClient, OrderingClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Ordering:BaseUrl"]
        ?? throw new InvalidOperationException("Services:Ordering:BaseUrl is not configured."));
    client.Timeout = TimeSpan.FromSeconds(15);
}).AddHttpMessageHandler<BearerForwardingHandler>();
builder.Services.AddHttpClient<IPaymentClient, PaymentClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Payment:BaseUrl"]
        ?? throw new InvalidOperationException("Services:Payment:BaseUrl is not configured."));
    client.Timeout = TimeSpan.FromSeconds(15);
}).AddHttpMessageHandler<BearerForwardingHandler>();

builder.Services.AddJwtAuth(builder.Configuration);
builder.Services.AddMessaging(builder.Configuration, x => x.AddConsumer<CartCleanupConsumer>());
builder.Services.AddApiDefaults();
builder.Services.AddSwaggerWithJwt("Sneaker Shop - Cart API");
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

app.Run();
