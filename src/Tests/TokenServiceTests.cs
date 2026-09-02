using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using IdentityProvider;
using Microsoft.Extensions.Configuration;

namespace Tests;

public class TokenServiceTests
{
    private static TokenService CreateService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Issuer"] = "http://test-idp",
                ["Auth:Audience"] = "flight-booking"
            })
            .Build();
        return new TokenService(config);
    }

    [Fact]
    public void GetJwks_ReturnsSigningKey()
    {
        var service = CreateService();
        var jwks = service.GetJwks();
        Assert.NotEmpty(jwks.Keys);
        Assert.Equal("sig", jwks.Keys[0].Use);
    }

    [Fact]
    public void CreateAccessToken_ContainsUserClaims()
    {
        var service = CreateService();
        var user = new UserAccount
        {
            Username = "admin",
            Role = "Admin",
            Email = "admin@test.com",
            FirstName = "Admin",
            LastName = "User"
        };

        var token = service.CreateAccessToken(user, "openid profile email", TimeSpan.FromHours(1));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("admin", jwt.Claims.First(c => c.Type == "preferred_username").Value);
        Assert.Contains(jwt.Claims, c => c.Value == "Admin" && (c.Type == ClaimTypes.Role || c.Type == "role"));
        Assert.Contains(jwt.Claims, c => c.Type == "email");
    }

    [Fact]
    public void CreateIdToken_IncludesProfileClaims_WhenScopeContainsProfile()
    {
        var service = CreateService();
        var user = new UserAccount
        {
            Username = "ivan",
            Role = "User",
            FirstName = "Ivan",
            LastName = "Petrov",
            Email = "ivan@test.com"
        };

        var token = service.CreateIdToken(user, "openid profile email", TimeSpan.FromHours(1));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Contains(jwt.Claims, c => c.Type == "given_name" && c.Value == "Ivan");
        Assert.Contains(jwt.Claims, c => c.Type == "family_name" && c.Value == "Petrov");
    }
}
