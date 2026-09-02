using ApiGatewayService;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Tests;

public class AuthGatewayControllerTests
{
    [Fact]
    public void Authorize_RedirectsToIdentityProvider()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:PublicIdentityProviderUrl"] = "http://localhost:8090",
                ["Auth:ClientId"] = "flight-ui",
                ["Auth:CallbackUrl"] = "http://localhost:8080/api/v1/callback"
            })
            .Build();

        var controller = new AuthGatewayController(config, Mock.Of<IHttpClientFactory>());
        var result = controller.Authorize(null, null);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Contains("/oauth/authorize", redirect.Url);
        Assert.Contains("client_id=flight-ui", redirect.Url);
        Assert.Contains("response_type=code", redirect.Url);
    }

    [Fact]
    public async Task Callback_ReturnsBadRequest_WhenCodeMissing()
    {
        var config = new ConfigurationBuilder().Build();
        var controller = new AuthGatewayController(config, Mock.Of<IHttpClientFactory>());

        var result = await controller.Callback(null, null, null);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Callback_ReturnsBadRequest_WhenErrorProvided()
    {
        var config = new ConfigurationBuilder().Build();
        var controller = new AuthGatewayController(config, Mock.Of<IHttpClientFactory>());

        var result = await controller.Callback(null, null, "access_denied");
        Assert.IsType<BadRequestObjectResult>(result);
    }
}
