using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FullProject.Services.Health;

public sealed class LogPersistenceHealthState : IHealthCheck
{
    private readonly object _gate = new();
    private string? _lastFailure;
    private DateTime? _failedAtUtc;

    public void MarkSucceeded()
    {
        lock (_gate)
        {
            _lastFailure = null;
            _failedAtUtc = null;
        }
    }

    public void MarkFailed(Exception exception)
    {
        lock (_gate)
        {
            _lastFailure = "persistence-failed";
            _failedAtUtc = DateTime.UtcNow;
        }
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_lastFailure is null)
                return Task.FromResult(HealthCheckResult.Healthy("Security log persistence is operational."));

            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Security log persistence failed.",
                data: new Dictionary<string, object>
                {
                    ["failedAtUtc"] = _failedAtUtc?.ToString("O") ?? string.Empty,
                    ["error"] = _lastFailure
                }));
        }
    }
}
