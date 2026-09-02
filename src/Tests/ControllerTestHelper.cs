using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SharedEvents;

namespace TestsSupport;

public static class ControllerTestHelper
{
    public static void SetUser(ControllerBase controller, string username, string role = "User")
    {
        var claims = new List<Claim>
        {
            new("preferred_username", username),
            new(ClaimTypes.Role, role)
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };
    }

    public static Mock<IEventPublisher> CreateEventPublisherMock()
    {
        var mock = new Mock<IEventPublisher>();
        return mock;
    }
}
