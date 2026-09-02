using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedAuth;

namespace ApiGatewayService;

[ApiController]
[Route("api/v1/users")]
[Authorize(Policy = "AdminOnly")]
[Produces("application/json")]
public class UsersGatewayController : ControllerBase
{
    private readonly IdentityClient _client;

    public UsersGatewayController(IdentityClient client)
    {
        _client = client;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var response = await _client.GetUsersAsync();
        return await ForwardAsync(response);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Username and password are required" });

        var response = await _client.CreateUserAsync(request);
        return await ForwardAsync(response);
    }

    private static async Task<IActionResult> ForwardAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return new UnauthorizedResult();

        if (response.StatusCode == HttpStatusCode.Forbidden)
            return new ForbidResult();

        if (string.IsNullOrWhiteSpace(body))
            return new StatusCodeResult((int)response.StatusCode);

        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            Content = body,
            ContentType = "application/json"
        };
    }
}
