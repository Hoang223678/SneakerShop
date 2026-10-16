using System.Text;
using System.Text.Json.Serialization;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SneakerShop.BuildingBlocks.Caching;

namespace SneakerShop.BuildingBlocks;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Validates JWTs issued by the Identity service. All services share the same key/issuer/audience,
    /// and reject tokens revoked by logout (checked in Redis).
    /// </summary>
    public static IServiceCollection AddJwtAuth(this IServiceCollection services, IConfiguration config)
    {
        var key = config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        services.AddRedis(config);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false; // keep claim names as issued: sub, email, role...
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = config["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = config["Jwt:Audience"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
                options.Events = new JwtBearerEvents { OnTokenValidated = RejectRevokedTokenAsync };
            });

        services.AddAuthorization();
        return services;
    }

    /// <summary>Fails open while Redis is unreachable: tokens are short-lived, and blocking every request would be worse.</summary>
    private static async Task RejectRevokedTokenAsync(TokenValidatedContext context)
    {
        var jti = context.Principal?.FindFirst("jti")?.Value;
        if (jti is null) return;

        var services = context.HttpContext.RequestServices;
        try
        {
            if (await services.GetRequiredService<ITokenRevocationStore>().IsRevokedAsync(jti))
                context.Fail("The access token has been revoked.");
        }
        catch (Exception exception) when (exception.IsRedisFailure())
        {
            services.GetRequiredService<ILoggerFactory>().CreateLogger("SneakerShop.Auth")
                .LogWarning(exception, "Skipped the token revocation check because Redis is unavailable.");
        }
    }

    /// <summary>Global exception handler (ProblemDetails) and health checks.</summary>
    public static IServiceCollection AddApiDefaults(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        services.AddHealthChecks();
        return services;
    }

    /// <summary>Serialize enums as strings ("Pending" instead of 0).</summary>
    public static IMvcBuilder AddApiJson(this IMvcBuilder builder) =>
        builder.AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services, string title)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = title, Version = "v1" });
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the access token only (without the word Bearer)."
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });
        });
        return services;
    }

    /// <summary>MassTransit over RabbitMQ with retry. Pass consumers via <paramref name="configure"/>.</summary>
    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration config,
        Action<IBusRegistrationConfigurator>? configure = null)
    {
        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();
            configure?.Invoke(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(config["RabbitMq:Host"] ?? "localhost", "/", h =>
                {
                    h.Username(config["RabbitMq:Username"] ?? "guest");
                    h.Password(config["RabbitMq:Password"] ?? "guest");
                });

                cfg.UseMessageRetry(r => r.Exponential(5,
                    TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2)));

                cfg.ConfigureEndpoints(context);
            });
        });
        return services;
    }
}
