using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedAuth;

namespace ApiGatewayService;

[ApiController]
[Route("api/v1")]
public class AuthGatewayController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public AuthGatewayController(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("authorize")]
    [AllowAnonymous]
    public IActionResult Authorize(
        [FromQuery] string? redirect_uri,
        [FromQuery] string? state)
    {
        // Browser redirect must use a public URL (localhost), not the docker DNS name.
        var idpPublicUrl = _configuration["Auth:PublicIdentityProviderUrl"]
                           ?? _configuration["Services:IdentityProvider"]
                           ?? "http://localhost:8090";
        var clientId = _configuration["Auth:ClientId"] ?? "flight-ui";
        var callback = redirect_uri
                       ?? _configuration["Auth:CallbackUrl"]
                       ?? $"{Request.Scheme}://{Request.Host}/api/v1/callback";
        var scope = "openid profile email";

        var url = $"{idpPublicUrl.TrimEnd('/')}/oauth/authorize" +
                  $"?response_type=code&client_id={Uri.EscapeDataString(clientId)}" +
                  $"&redirect_uri={Uri.EscapeDataString(callback)}" +
                  $"&scope={Uri.EscapeDataString(scope)}";

        if (!string.IsNullOrWhiteSpace(state))
            url += $"&state={Uri.EscapeDataString(state)}";

        return Redirect(url);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] OAuthLoginRequest request)
    {
        if (request == null)
            return BadRequest(new { message = "Request body is required" });

        var idpUrl = _configuration["Services:IdentityProvider"] ?? "http://identity_provider:8090";
        var client = _httpClientFactory.CreateClient();
        var response = await client.PostAsJsonAsync($"{idpUrl.TrimEnd('/')}/oauth/login", request);

        var payload = await response.Content.ReadAsStringAsync();
        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            Content = payload,
            ContentType = "application/json"
        };
    }

    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error)
    {
        if (!string.IsNullOrWhiteSpace(error))
            return BadRequest(new { message = error });

        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { message = "Authorization code is missing" });

        var idpUrl = _configuration["Services:IdentityProvider"] ?? "http://identity_provider:8090";
        var clientId = _configuration["Auth:ClientId"] ?? "flight-ui";
        var clientSecret = _configuration["Auth:ClientSecret"] ?? "flight-ui-secret";
        var callback = _configuration["Auth:CallbackUrl"]
                       ?? $"{Request.Scheme}://{Request.Host}/api/v1/callback";

        var client = _httpClientFactory.CreateClient();
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = callback,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        };

        var response = await client.PostAsync(
            $"{idpUrl.TrimEnd('/')}/oauth/token",
            new FormUrlEncodedContent(form));

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, new { message = "Token exchange failed" });

        var payload = await response.Content.ReadAsStringAsync();
        var uiRedirect = _configuration["Auth:UiRedirectUrl"] ?? "http://localhost:3000/callback";
        return Redirect($"{uiRedirect}?token={Uri.EscapeDataString(payload)}&state={Uri.EscapeDataString(state ?? "")}");
    }
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
