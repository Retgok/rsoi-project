using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ApiGatewayService;

public sealed class AuthForwardingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthForwardingHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var authHeader = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authHeader))
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);

        return base.SendAsync(request, cancellationToken);
    }
}

public static class HttpClientAuthExtensions
{
    public static IHttpClientBuilder AddAuthForwarding(this IHttpClientBuilder builder)
        => builder.AddHttpMessageHandler<AuthForwardingHandler>();

    public static IServiceCollection AddAuthForwarding(this IServiceCollection services)
    {
        services.AddTransient<AuthForwardingHandler>();
        return services;
    }
}
