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
    private readonly IConfiguration _configuration;

    public OAuthController(
        IdentityDb db,
        TokenService tokenService,
        IEventPublisher events,
        IConfiguration configuration)
    {
        _db = db;
        _tokenService = tokenService;
        _events = events;
        _configuration = configuration;
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

        var client = await ValidateAuthorizeRequest(client_id, redirect_uri);
        if (client == null)
            return BadRequest(new { error = "invalid_client" });

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return Redirect(BuildUiLoginUrl(response_type, client_id, redirect_uri, scope, state));

        var redirectUrl = await TryCreateAuthorizationRedirectAsync(
            client, response_type, client_id, redirect_uri, scope, state, username, password);
        if (redirectUrl == null)
            return Unauthorized(new { error = "invalid_credentials" });

        return Redirect(redirectUrl);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] OAuthLoginRequest request)
    {
        if (request == null)
            return BadRequest(new { error = "invalid_request" });

        var client = await ValidateAuthorizeRequest(request.client_id, request.redirect_uri);
        if (client == null)
            return BadRequest(new { error = "invalid_client" });

        if (!string.Equals(request.response_type, "code", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "unsupported_response_type" });

        if (string.IsNullOrWhiteSpace(request.username) || string.IsNullOrWhiteSpace(request.password))
            return BadRequest(new { error = "invalid_request" });

        var redirectUrl = await TryCreateAuthorizationRedirectAsync(
            client,
            request.response_type,
            request.client_id,
            request.redirect_uri,
            request.scope,
            request.state,
            request.username,
            request.password);
        if (redirectUrl == null)
            return Unauthorized(new { error = "invalid_credentials" });

        return Ok(new { redirectUrl });
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

    private async Task<OauthClient?> ValidateAuthorizeRequest(string client_id, string redirect_uri)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.ClientId == client_id);
        if (client == null || !IsRedirectAllowed(client.RedirectUris, redirect_uri))
            return null;

        return client;
    }

    private async Task<string?> TryCreateAuthorizationRedirectAsync(
        OauthClient client,
        string response_type,
        string client_id,
        string redirect_uri,
        string scope,
        string? state,
        string username,
        string password)
    {
        if (!string.Equals(response_type, "code", StringComparison.OrdinalIgnoreCase))
            return null;

        if (!IsRedirectAllowed(client.RedirectUris, redirect_uri))
            return null;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return null;

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

        return redirect;
    }

    private string BuildUiLoginUrl(
        string response_type,
        string client_id,
        string redirect_uri,
        string scope,
        string? state)
    {
        var query = HttpUtility.ParseQueryString("");
        query["response_type"] = response_type;
        query["client_id"] = client_id;
        query["redirect_uri"] = redirect_uri;
        query["scope"] = scope;
        if (!string.IsNullOrWhiteSpace(state))
            query["state"] = state;

        return $"{GetUiLoginUrl()}?{query}";
    }

    private string GetUiLoginUrl()
    {
        var loginUrl = _configuration["Auth:UiLoginUrl"];
        if (!string.IsNullOrWhiteSpace(loginUrl))
            return loginUrl.TrimEnd('/');

        var redirectUrl = _configuration["Auth:UiRedirectUrl"] ?? "http://localhost:3000/callback";
        if (redirectUrl.EndsWith("/callback", StringComparison.OrdinalIgnoreCase))
            return redirectUrl[..^"/callback".Length] + "/login";

        return "http://localhost:3000/login";
    }

    private static bool IsRedirectAllowed(string redirectUris, string redirectUri)
        => redirectUris.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(uri => string.Equals(uri, redirectUri, StringComparison.OrdinalIgnoreCase));
}

public sealed class OAuthLoginRequest
{
    public string response_type { get; set; } = "code";
    public string client_id { get; set; } = "";
    public string redirect_uri { get; set; } = "";
    public string scope { get; set; } = "openid profile email";
    public string? state { get; set; }
    public string username { get; set; } = "";
    public string password { get; set; } = "";
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
