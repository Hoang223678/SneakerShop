using MassTransit;
using Microsoft.EntityFrameworkCore;
using Ordering.Api.Data;
using Ordering.Api.Services;
using SneakerShop.BuildingBlocks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default"),
        sql => sql.EnableRetryOnFailure()));

builder.Services.AddScoped<IOrderService, OrderService>();

// Typed HTTP client for the Catalog service's internal endpoint.
builder.Services.AddHttpClient<ICatalogClient, CatalogClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Catalog:BaseUrl"]
        ?? throw new InvalidOperationException("Services:Catalog:BaseUrl is not configured."));
    client.DefaultRequestHeaders.Add(InternalApiKeyAttribute.HeaderName, builder.Configuration["Internal:ApiKey"] ?? "");
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddJwtAuth(builder.Configuration);
builder.Services.AddMessaging(builder.Configuration, x => x.AddConsumers(typeof(Program).Assembly));
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
