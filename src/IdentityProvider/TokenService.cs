using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace IdentityProvider;

public sealed class TokenService
{
    private readonly RsaSecurityKey _signingKey;
    private readonly string _issuer;
    private readonly string _audience;

    public TokenService(IConfiguration configuration)
    {
        _issuer = configuration["Auth:Issuer"] ?? "http://localhost:8090";
        _audience = configuration["Auth:Audience"] ?? "flight-booking";

        var pem = configuration["Auth:RsaPrivateKeyPem"];
        if (!string.IsNullOrWhiteSpace(pem))
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            _signingKey = new RsaSecurityKey(rsa) { KeyId = "flight-booking-key" };
            return;
        }

        var generated = RSA.Create(2048);
        _signingKey = new RsaSecurityKey(generated) { KeyId = "flight-booking-key" };
    }

    public JsonWebKeySet GetJwks()
    {
        var rsa = RSA.Create();
        if (_signingKey.Rsa != null)
            rsa.ImportParameters(_signingKey.Rsa.ExportParameters(false));

        var publicKey = new RsaSecurityKey(rsa) { KeyId = _signingKey.KeyId };
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(publicKey);
        jwk.Use = "sig";
        jwk.Alg = SecurityAlgorithms.RsaSha256;
        var set = new JsonWebKeySet();
        set.Keys.Add(jwk);
        return set;
    }

    public string CreateAccessToken(UserAccount user, string scope, TimeSpan lifetime)
    {
        var claims = new List<System.Security.Claims.Claim>
        {
            new("sub", user.Username),
            new("preferred_username", user.Username),
            new("role", user.Role),
            new("scope", scope)
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
            claims.Add(new System.Security.Claims.Claim("email", user.Email));

        if (!string.IsNullOrWhiteSpace(user.FirstName))
            claims.Add(new System.Security.Claims.Claim("given_name", user.FirstName));

        if (!string.IsNullOrWhiteSpace(user.LastName))
            claims.Add(new System.Security.Claims.Claim("family_name", user.LastName));

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256);
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var token = handler.CreateJwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            subject: new System.Security.Claims.ClaimsIdentity(claims),
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: credentials);

        return handler.WriteToken(token);
    }

    public string CreateIdToken(UserAccount user, string scope, TimeSpan lifetime)
    {
        var claims = new List<System.Security.Claims.Claim>
        {
            new("sub", user.Username),
            new("preferred_username", user.Username),
            new(System.Security.Claims.ClaimTypes.Role, user.Role)
        };

        if (scope.Contains("email", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(user.Email))
            claims.Add(new System.Security.Claims.Claim("email", user.Email));

        if (scope.Contains("profile", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(user.FirstName))
                claims.Add(new System.Security.Claims.Claim("given_name", user.FirstName));
            if (!string.IsNullOrWhiteSpace(user.LastName))
                claims.Add(new System.Security.Claims.Claim("family_name", user.LastName));
        }

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256);
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var token = handler.CreateJwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            subject: new System.Security.Claims.ClaimsIdentity(claims),
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: credentials);

        return handler.WriteToken(token);
    }
}
