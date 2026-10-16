using Identity.Api.Data;
using Identity.Api.Services;
using Microsoft.EntityFrameworkCore;
using SneakerShop.BuildingBlocks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default"),
        sql => sql.EnableRetryOnFailure()));

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddSingleton<ILoginAttemptLimiter, RedisLoginAttemptLimiter>();

builder.Services.AddJwtAuth(builder.Configuration);
builder.Services.AddApiDefaults();
builder.Services.AddSwaggerWithJwt("Sneaker Shop - Identity API");
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

await StartupRetry.RunAsync(
    () => DbInitializer.InitializeAsync(app.Services, app.Configuration),
    app.Logger);

app.Run();
