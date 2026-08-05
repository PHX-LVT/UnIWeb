using Contracts.Auth;
using FullProject.Services.LogManagement;
using FullProject.Services.Notifications;
using FullProject.Utils;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace FullProject.Middleware;

public sealed class AdminMutationAuditMiddleware
{
    private static readonly HashSet<string> MutationMethods =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };
    private static readonly string[] TargetRouteKeys =
        ["submissionId", "blockId", "sectionId", "childId", "pageId", "revisionId", "presetId", "linkId", "groupId", "buttonId", "termId", "type", "id"];

    private readonly RequestDelegate _next;
    private readonly ILogger<AdminMutationAuditMiddleware> _logger;

    public AdminMutationAuditMiddleware(RequestDelegate next, ILogger<AdminMutationAuditMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IAuditTrailWriter writer,
        IAdminDomainEventPublisher domainEvents)
    {
        var isAdminMutation = context.Request.Path.StartsWithSegments("/api/admin") &&
                              MutationMethods.Contains(context.Request.Method);
        if (!isAdminMutation)
        {
            await _next(context);
            return;
        }

        var descriptor = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        Exception? downstreamException = null;
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            downstreamException = ex;
            throw;
        }
        finally
        {
            if (descriptor is not null)
            {
                await PersistAuditAsync(context, writer, descriptor, downstreamException);
                await PublishDomainEventAsync(context, domainEvents, descriptor, downstreamException);
            }
        }
    }

    private async Task PersistAuditAsync(
        HttpContext context,
        IAuditTrailWriter writer,
        ControllerActionDescriptor descriptor,
        Exception? downstreamException)
    {
        var outcome = downstreamException is null
            ? ResolveOutcome(context.Response.StatusCode)
            : AdminAuditOutcome.Failed;
        // Rich domain writers mark requests they already recorded. If their
        // write failed, the marker is absent and this generic fallback runs.
        if (outcome == AdminAuditOutcome.Succeeded &&
            context.Items.ContainsKey("AuditTrail.ExplicitWritten"))
            return;

        var definition = AuditActionCatalog.Resolve(descriptor);
        if (!definition.ShouldAudit) return;
        var targetId = TargetRouteKeys
            .Select(key => context.Request.RouteValues.TryGetValue(key, out var value) ? value?.ToString() : null)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        try
        {
            await writer.WriteAsync(new AuditWriteRequest
            {
                DomainCode = definition.DomainCode,
                ActionCode = definition.ActionCode,
                OutcomeCode = context.Items.TryGetValue(ApiOutcomePolicy.OperationErrorItem, out var errorCode)
                    ? errorCode?.ToString() ?? outcome.ToString().ToLowerInvariant()
                    : outcome.ToString().ToLowerInvariant(),
                TargetTypeCode = definition.TargetTypeCode,
                TargetId = targetId,
                Outcome = outcome,
                Severity = definition.IsCritical || outcome is AdminAuditOutcome.Denied or AdminAuditOutcome.Failed
                    ? AdminAuditSeverity.Warning
                    : AdminAuditSeverity.Information,
                ChangeCount = outcome == AdminAuditOutcome.Succeeded ? 1 : 0,
                MessageKey = $"audit.{definition.ActionCode}.{outcome.ToString().ToLowerInvariant()}",
                ResultMessage = downstreamException is null
                    ? $"{definition.ActionCode} {outcome.ToString().ToLowerInvariant()}."
                    : $"{definition.ActionCode} failed with an unhandled server error.",
                CorrelationId = context.TraceIdentifier,
                BatchId = definition.ActionCode.Contains("bulk", StringComparison.OrdinalIgnoreCase) ||
                          definition.ActionCode.Contains("batch", StringComparison.OrdinalIgnoreCase)
                    ? context.TraceIdentifier
                    : null,
                RequestPath = context.Request.Path,
                RequestMethod = context.Request.Method,
                RetentionClass = definition.IsCritical ? "critical" : "standard"
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // A completed mutation cannot be rolled back safely from middleware.
            // Surface the storage failure operationally without corrupting the
            // already-produced response.
            _logger.LogError(ex,
                "Failed to persist audit event {DomainCode}/{ActionCode}; correlation {CorrelationId}.",
                definition.DomainCode,
                definition.ActionCode,
                context.TraceIdentifier);
        }
    }

    private async Task PublishDomainEventAsync(
        HttpContext context,
        IAdminDomainEventPublisher publisher,
        ControllerActionDescriptor descriptor,
        Exception? downstreamException)
    {
        if (downstreamException is not null || context.Response.StatusCode is < 200 or >= 300)
            return;

        var definition = AuditActionCatalog.Resolve(descriptor);
        if (!definition.ShouldAudit) return;
        var targetId = TargetRouteKeys
            .Select(key => context.Request.RouteValues.TryGetValue(key, out var value) ? value?.ToString() : null)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var eventId = Guid.NewGuid().ToString("N");
        try
        {
            await publisher.PublishAsync(new AdminDomainEventEnvelope(
                eventId,
                definition.ActionCode,
                definition.DomainCode,
                definition.ActionCode,
                context.User.FindFirst("adminId")?.Value,
                definition.TargetTypeCode,
                targetId,
                DateTimeOffset.UtcNow,
                context.TraceIdentifier,
                $"{definition.ActionCode}:{targetId ?? "none"}:{context.TraceIdentifier}",
                new Dictionary<string, string?>()), CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to publish domain event {DomainCode}/{ActionCode}; correlation {CorrelationId}.",
                definition.DomainCode,
                definition.ActionCode,
                context.TraceIdentifier);
        }
    }

    private static AdminAuditOutcome ResolveOutcome(int statusCode) => statusCode switch
    {
        StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden => AdminAuditOutcome.Denied,
        >= 200 and < 300 => AdminAuditOutcome.Succeeded,
        StatusCodes.Status409Conflict => AdminAuditOutcome.Partial,
        _ => AdminAuditOutcome.Failed
    };
}
