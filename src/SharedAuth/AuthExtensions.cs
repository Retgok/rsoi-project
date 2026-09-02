using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace SharedAuth;

public static class AuthExtensions
{
    public static IServiceCollection AddServiceJwtAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        bool preloadJwks = true)
    {
        JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

        var issuer = configuration["Auth:Issuer"]
                     ?? "http://localhost:8090";
        var jwksUrl = configuration["Auth:JwksUrl"]
                      ?? $"{issuer.TrimEnd('/')}/.well-known/jwks.json";

        services.AddSingleton<IJwksProvider>(_ => new JwksProvider(jwksUrl));
        if (preloadJwks)
        {
            services.AddHostedService<JwksPreloadHostedService>();
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IJwksProvider>((options, jwksProvider) =>
            {
                options.RequireHttpsMetadata = false;
                options.MapInboundClaims = false;
                options.TokenHandlers.Clear();
                options.TokenHandlers.Add(new JwtSecurityTokenHandler());

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = "flight-booking",
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = "preferred_username",
                    RoleClaimType = "role",
                    IssuerSigningKeyResolver = (_, _, _, _) => jwksProvider.GetSigningKeys()
                };

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = 401;
                        context.Response.ContentType = "application/json";
                        return context.Response.WriteAsync("{\"message\":\"Unauthorized\"}");
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy =>
                policy.RequireAssertion(context =>
                    context.User.Claims.Any(claim =>
                        claim.Value == "Admin"
                        && (claim.Type == "role"
                            || claim.Type == ClaimTypes.Role))));
        });

        services.AddHttpContextAccessor();
        return services;
    }

    public static IApplicationBuilder UseServiceJwtAuth(this IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    public static string? GetUsername(this ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return null;

        return user.FindFirstValue("preferred_username")
               ?? user.FindFirstValue(ClaimTypes.Name)
               ?? user.FindFirstValue("sub");
    }

    public static bool IsAdmin(this ClaimsPrincipal user)
        => user.IsInRole("Admin");
}
