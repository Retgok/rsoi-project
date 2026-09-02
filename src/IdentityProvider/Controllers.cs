using System.Web;
using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SharedAuth;
using SharedEvents;

namespace IdentityProvider;

[ApiController]
[Route(".well-known")]
[AllowAnonymous]
public class DiscoveryController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public DiscoveryController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet("openid-configuration")]
    public IActionResult GetConfiguration()
    {
        var issuer = GetIssuer();
        return Ok(new
        {
            issuer,
            authorization_endpoint = $"{issuer}/oauth/authorize",
            token_endpoint = $"{issuer}/oauth/token",
            jwks_uri = $"{issuer}/.well-known/jwks.json",
            response_types_supported = new[] { "code" },
            subject_types_supported = new[] { "public" },
            id_token_signing_alg_values_supported = new[] { "RS256" },
            scopes_supported = new[] { "openid", "profile", "email" },
            token_endpoint_auth_methods_supported = new[] { "client_secret_post" },
            grant_types_supported = new[] { "authorization_code" }
        });
    }

    [HttpGet("jwks.json")]
    public IActionResult GetJwks([FromServices] IJwksProvider jwksProvider)
    {
        var set = new JsonWebKeySet();
        foreach (var key in jwksProvider.GetSigningKeys())
        {
            var jwk = JsonWebKeyConverter.ConvertFromSecurityKey(key);
            jwk.Use = "sig";
            jwk.Alg = SecurityAlgorithms.RsaSha256;
            set.Keys.Add(jwk);
        }

        return Ok(set);
    }

    private string GetIssuer()
        => _configuration["Auth:Issuer"] ?? $"{Request.Scheme}://{Request.Host}";
}

[ApiController]
[Route("oauth")]
[AllowAnonymous]
public class OAuthController : ControllerBase
{
    private readonly IdentityDb _db;
    private readonly TokenService _tokenService;
    private readonly IEventPublisher _events;

    public OAuthController(IdentityDb db, TokenService tokenService, IEventPublisher events)
    {
        _db = db;
        _tokenService = tokenService;
        _events = events;
    }

    [HttpGet("authorize")]
    public async Task<IActionResult> Authorize(
        [FromQuery] string response_type,
        [FromQuery] string client_id,
        [FromQuery] string redirect_uri,
        [FromQuery] string scope,
        [FromQuery] string? state,
        [FromQuery] string? username,
        [FromQuery] string? password)
    {
        if (!string.Equals(response_type, "code", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "unsupported_response_type" });

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.ClientId == client_id);
        if (client == null || !IsRedirectAllowed(client.RedirectUris, redirect_uri))
            return BadRequest(new { error = "invalid_client" });

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            var query = HttpUtility.ParseQueryString("");
            query["response_type"] = response_type;
            query["client_id"] = client_id;
            query["redirect_uri"] = redirect_uri;
            query["scope"] = scope;
            if (!string.IsNullOrWhiteSpace(state))
                query["state"] = state;

            var stateField = string.IsNullOrWhiteSpace(state)
                ? ""
                : $"""<input type="hidden" name="state" value="{state}" />""";

            var html = $$"""
                <!DOCTYPE html>
                <html lang="ru">
                <head><meta charset="utf-8"><title>Login</title>
                <style>
                  body { font-family: sans-serif; max-width: 420px; margin: 40px auto; }
                  input { width: 100%; margin: 8px 0; padding: 8px; }
                  button { padding: 8px 16px; }
                </style></head>
                <body>
                  <h2>Flight Booking Login</h2>
                  <form method="get" action="/oauth/authorize">
                    <input type="hidden" name="response_type" value="{{response_type}}" />
                    <input type="hidden" name="client_id" value="{{client_id}}" />
                    <input type="hidden" name="redirect_uri" value="{{redirect_uri}}" />
                    <input type="hidden" name="scope" value="{{scope}}" />
                    {{stateField}}
                    <label>Username<input name="username" required /></label>
                    <label>Password<input name="password" type="password" required /></label>
                    <button type="submit">Sign in</button>
                  </form>
                </body></html>
                """;
            return Content(html, "text/html");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return Unauthorized(new { error = "invalid_credentials" });

        var code = Guid.NewGuid().ToString("N");
        _db.AuthCodes.Add(new AuthCode
        {
            Code = code,
            ClientId = client_id,
            Username = user.Username,
            RedirectUri = redirect_uri,
            Scope = string.IsNullOrWhiteSpace(scope) ? "openid profile email" : scope,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        });
        await _db.SaveChangesAsync();

        _events.Publish(new ServiceEvent("identity-provider", "user_login", user.Username, null, DateTime.UtcNow));

        var redirect = $"{redirect_uri}?code={code}";
        if (!string.IsNullOrWhiteSpace(state))
            redirect += $"&state={HttpUtility.UrlEncode(state)}";

        return Redirect(redirect);
    }

    [HttpPost("token")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Token([FromForm] TokenRequest request)
    {
        if (!string.Equals(request.grant_type, "authorization_code", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "unsupported_grant_type" });

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.ClientId == request.client_id);
        if (client == null || client.ClientSecret != request.client_secret)
            return Unauthorized(new { error = "invalid_client" });

        var authCode = await _db.AuthCodes.FirstOrDefaultAsync(c => c.Code == request.code);
        if (authCode == null || authCode.ExpiresAt < DateTime.UtcNow)
            return BadRequest(new { error = "invalid_grant" });

        if (!string.Equals(authCode.ClientId, request.client_id, StringComparison.Ordinal)
            || !string.Equals(authCode.RedirectUri, request.redirect_uri, StringComparison.Ordinal))
            return BadRequest(new { error = "invalid_grant" });

        var user = await _db.Users.FirstAsync(u => u.Username == authCode.Username);
        _db.AuthCodes.Remove(authCode);
        await _db.SaveChangesAsync();

        var scope = authCode.Scope;
        var accessToken = _tokenService.CreateAccessToken(user, scope, TimeSpan.FromHours(1));
        var idToken = scope.Contains("openid", StringComparison.OrdinalIgnoreCase)
            ? _tokenService.CreateIdToken(user, scope, TimeSpan.FromHours(1))
            : null;

        return Ok(new
        {
            access_token = accessToken,
            token_type = "Bearer",
            expires_in = 3600,
            scope,
            id_token = idToken
        });
    }

    private static bool IsRedirectAllowed(string redirectUris, string redirectUri)
        => redirectUris.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(uri => string.Equals(uri, redirectUri, StringComparison.OrdinalIgnoreCase));
}

public sealed class TokenRequest
{
    [FromForm(Name = "grant_type")]
    public string grant_type { get; set; } = "";

    [FromForm(Name = "code")]
    public string code { get; set; } = "";

    [FromForm(Name = "redirect_uri")]
    public string redirect_uri { get; set; } = "";

    [FromForm(Name = "client_id")]
    public string client_id { get; set; } = "";

    [FromForm(Name = "client_secret")]
    public string client_secret { get; set; } = "";
}

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IdentityDb _db;
    private readonly IEventPublisher _events;

    public UsersController(IdentityDb db, IEventPublisher events)
    {
        _db = db;
        _events = events;
    }

    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _db.Users
            .OrderBy(u => u.Username)
            .Select(u => new
            {
                u.Username,
                u.Email,
                u.FirstName,
                u.LastName,
                u.Role
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Username and password are required" });

        if (await _db.Users.AnyAsync(u => u.Username == request.Username))
            return Conflict(new { message = "User already exists" });

        var user = new UserAccount
        {
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Role = "User"
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _events.Publish(new ServiceEvent(
            "identity-provider",
            "user_created",
            User.GetUsername(),
            request.Username,
            DateTime.UtcNow));

        return Created($"/api/v1/users/{user.Username}", new
        {
            user.Username,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Role
        });
    }
}

public sealed class CreateUserRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}

[ApiController]
[Route("manage")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health() => Ok("OK");
}
