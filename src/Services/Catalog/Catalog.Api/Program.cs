using Catalog.Api.Data;
using Microsoft.EntityFrameworkCore;
using SneakerShop.BuildingBlocks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default"),
        sql => sql.EnableRetryOnFailure()));

builder.Services.AddJwtAuth(builder.Configuration);
builder.Services.AddMessaging(builder.Configuration); // publish-only: no consumers in Catalog
builder.Services.AddApiDefaults();
builder.Services.AddSwaggerWithJwt("Sneaker Shop - Catalog API");
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
