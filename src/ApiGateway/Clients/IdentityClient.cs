using System.Net.Http.Json;

namespace ApiGatewayService;

public class IdentityClient
{
    private readonly HttpClient _client;

    public IdentityClient(HttpClient client) => _client = client;

    public async Task<HttpResponseMessage> GetUsersAsync()
        => await _client.GetAsync("/api/v1/users");

    public async Task<HttpResponseMessage> CreateUserAsync(CreateUserRequest request)
        => await _client.PostAsJsonAsync("/api/v1/users", request);
}

public sealed class CreateUserRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}
