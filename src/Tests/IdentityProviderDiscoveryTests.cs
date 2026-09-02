using IdentityProvider;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Tests;

public class IdentityProviderDiscoveryTests
{
    [Fact]
    public void GetConfiguration_ReturnsOpenIdMetadata()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Issuer"] = "http://identity-provider:8090"
            })
            .Build();

        var controller = new DiscoveryController(config);
        var result = controller.GetConfiguration();
        var ok = Assert.IsType<OkObjectResult>(result);

        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("authorization_endpoint", json);
        Assert.Contains("jwks_uri", json);
        Assert.Contains("openid", json);
    }

    [Fact]
    public void GetJwks_ReturnsKeys()
    {
        var config = new ConfigurationBuilder().Build();
        var tokenService = new TokenService(config);
        var jwksProvider = new LocalJwksProvider(tokenService);
        var controller = new DiscoveryController(config);

        var result = controller.GetJwks(jwksProvider);
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public void Health_ReturnsOk()
    {
        var controller = new HealthController();
        var result = controller.Health();
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("OK", ok.Value);
    }
}
