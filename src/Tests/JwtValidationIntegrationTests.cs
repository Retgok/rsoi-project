using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SharedAuth;

namespace Tests;

public class JwtValidationIntegrationTests
{
    [Fact]
    public void Validates_locally_signed_access_token()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Issuer"] = "http://localhost:8090",
                ["Auth:Audience"] = "flight-booking"
            })
            .Build();

        var tokenService = new IdentityProvider.TokenService(configuration);
        var localJwks = new IdentityProvider.LocalJwksProvider(tokenService);
        var user = new IdentityProvider.UserAccount
        {
            Username = "Test Max",
            Role = "User",
            Email = "test@example.com"
        };

        var token = tokenService.CreateAccessToken(user, "openid profile", TimeSpan.FromHours(1));
        ValidateToken(token, localJwks.GetSigningKeys());
    }

    [Fact]
    public void Validates_token_from_running_idp_using_jwks_provider()
    {
        var token = ObtainLiveToken();
        var jwks = new JwksProvider("http://127.0.0.1:8090/.well-known/jwks.json");
        var keys = jwks.GetSigningKeys().ToList();
        Assert.NotEmpty(keys);
        ValidateToken(token, keys);
    }

    private static void ValidateToken(string token, IEnumerable<SecurityKey> keys)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "http://localhost:8090",
            ValidateAudience = true,
            ValidAudience = "flight-booking",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RoleClaimType = "role",
            NameClaimType = "preferred_username",
            IssuerSigningKeyResolver = (_, _, _, _) => keys
        }, out _);

        Assert.Equal("Test Max", principal.GetUsername());
    }

    [Fact]
    public void Admin_token_has_admin_role_for_authorization()
    {
        var token = ObtainLiveToken("admin", "admin123");
        var jwks = new JwksProvider("http://127.0.0.1:8090/.well-known/jwks.json");
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "http://localhost:8090",
            ValidateAudience = true,
            ValidAudience = "flight-booking",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RoleClaimType = "role",
            NameClaimType = "preferred_username",
            IssuerSigningKeyResolver = (_, _, _, _) => jwks.GetSigningKeys()
        }, out _);

        Assert.True(principal.IsInRole("Admin"));
    }

    private static string ObtainLiveToken(string login = "Test Max", string password = "test123")
    {
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        var authUrl =
            "http://127.0.0.1:8090/oauth/authorize?response_type=code&client_id=flight-ui" +
            "&redirect_uri=http%3A%2F%2Flocalhost%3A8080%2Fapi%2Fv1%2Fcallback&scope=openid%20profile" +
            "&username=Test%20Max&password=test123";

        if (login != "Test Max" || password != "test123")
        {
            authUrl =
                "http://127.0.0.1:8090/oauth/authorize?response_type=code&client_id=flight-ui" +
                "&redirect_uri=http%3A%2F%2Flocalhost%3A8080%2Fapi%2Fv1%2Fcallback&scope=openid%20profile" +
                $"&username={Uri.EscapeDataString(login)}&password={Uri.EscapeDataString(password)}";
        }

        var authResponse = http.GetAsync(authUrl).GetAwaiter().GetResult();
        var location = authResponse.Headers.Location?.ToString()
                       ?? throw new InvalidOperationException($"No redirect: {authResponse.StatusCode}");
        var code = System.Web.HttpUtility.ParseQueryString(new Uri(location).Query).Get("code")
                   ?? throw new InvalidOperationException("No code in redirect");

        var tokenResponse = http.PostAsync(
            "http://127.0.0.1:8090/oauth/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = "http://localhost:8080/api/v1/callback",
                ["client_id"] = "flight-ui",
                ["client_secret"] = "flight-ui-secret"
            })).GetAwaiter().GetResult();

        tokenResponse.EnsureSuccessStatusCode();
        var json = tokenResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        return System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("access_token").GetString()!;
    }
}
