using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Motivation.Infrastructure.HealthChecks;

public class MemoryCacheHealthCheck : IHealthCheck
{
    private readonly IMemoryCache _cache;
    private const string TestKey = "__health_check_probe__";

    public MemoryCacheHealthCheck(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _cache.Set(TestKey, DateTime.UtcNow, TimeSpan.FromSeconds(5));
            var found = _cache.TryGetValue(TestKey, out _);

            return Task.FromResult(found
                ? HealthCheckResult.Healthy("MemoryCache is operational")
                : HealthCheckResult.Degraded("MemoryCache failed to retrieve probe value"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("MemoryCache check failed", ex));
        }
    }
}
