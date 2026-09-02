using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace SharedEvents;

public static class RequestMetricsExtensions
{
    public static IServiceCollection AddRequestMetrics(this IServiceCollection services, string serviceName)
    {
        services.AddSingleton(new RequestMetricsOptions { ServiceName = serviceName });
        services.AddSingleton<DbMetricsInterceptor>();
        return services;
    }

    public static IApplicationBuilder UseRequestMetrics(this IApplicationBuilder app)
        => app.UseMiddleware<RequestMetricsMiddleware>();
}
