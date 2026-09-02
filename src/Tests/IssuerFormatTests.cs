using System.IdentityModel.Tokens.Jwt;
using IdentityProvider;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Tests;

public class IssuerFormatTests
{
    [Theory]
    [InlineData("http://identity-provider:8090")]
    [InlineData("http://flight.local")]
    [InlineData("http://localhost:8090")]
    public void Issuer_is_accepted_by_jwt_validator(string issuer)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Issuer"] = issuer,
                ["Auth:Audience"] = "flight-booking"
            })
            .Build();

        var tokenService = new TokenService(configuration);
        var user = new UserAccount { Username = "admin", Role = "Admin" };
        var token = tokenService.CreateAccessToken(user, "openid", TimeSpan.FromHours(1));

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var ex = Record.Exception(() => handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = "flight-booking",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeyResolver = (_, _, _, _) => tokenService.GetJwks().GetSigningKeys()
        }, out _));

        Assert.Null(ex);
    }
}
