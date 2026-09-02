using ApiGatewayService;
using Microsoft.AspNetCore.Mvc;

namespace Tests;

public class GatewayHealthTests
{
    [Fact]
    public void Health_ReturnsOk()
    {
        var controller = new HealthController();
        var result = controller.Health();
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("OK", ok.Value);
    }
}
