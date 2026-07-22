using System.Collections.Concurrent;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FullProject.Services.Health;

public sealed class StartupMaintenanceHealthState : IHealthCheck
{
    private readonly ConcurrentDictionary<string, string> _failures =
        new(StringComparer.OrdinalIgnoreCase);

    public void MarkSucceeded(string operation) => _failures.TryRemove(operation, out _);

    public void MarkDegraded(string operation, Exception exception)
    {
        var message = string.IsNullOrWhiteSpace(exception.Message)
            ? exception.GetType().Name
            : exception.Message;
        _failures[operation] = message.Length <= 300 ? message : message[..300];
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_failures.IsEmpty)
            return Task.FromResult(HealthCheckResult.Healthy("Optional startup maintenance completed."));

        var data = _failures.ToDictionary(
            item => item.Key,
            item => (object)item.Value,
            StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(HealthCheckResult.Degraded(
            "One or more optional startup maintenance operations failed.",
            data: data));
    }
}
