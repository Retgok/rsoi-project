using System.Security.Claims;
using SharedAuth;

namespace Tests;

public class AuthExtensionsTests
{
    [Fact]
    public void GetUsername_ReturnsPreferredUsername_WhenAuthenticated()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("preferred_username", "ivan"),
            new Claim(ClaimTypes.Role, "User")
        ], "Bearer"));

        Assert.Equal("ivan", user.GetUsername());
    }

    [Fact]
    public void GetUsername_ReturnsNull_WhenNotAuthenticated()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        Assert.Null(user.GetUsername());
    }

    [Fact]
    public void GetUsername_ReturnsNull_ForNullPrincipal()
    {
        ClaimsPrincipal? user = null;
        Assert.Null(user.GetUsername());
    }

    [Fact]
    public void IsAdmin_ReturnsTrue_ForAdminRole()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.Role, "Admin")
        ], "Bearer"));

        Assert.True(user.IsAdmin());
    }

    [Fact]
    public void IsAdmin_ReturnsFalse_ForUserRole()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.Role, "User")
        ], "Bearer"));

        Assert.False(user.IsAdmin());
    }
}
