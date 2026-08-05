namespace FullProject.Services.Notifications;

public sealed record AdminDomainEventEnvelope(
    string EventId,
    string EventType,
    string DomainCode,
    string ActionCode,
    string? ActorId,
    string? TargetTypeCode,
    string? TargetId,
    DateTimeOffset OccurredAt,
    string TraceId,
    string DeduplicationKey,
    IReadOnlyDictionary<string, string?> SafeMetadata);

public interface IAdminDomainEventPublisher
{
    Task PublishAsync(AdminDomainEventEnvelope envelope, CancellationToken cancellationToken = default);
}

public sealed class NullAdminDomainEventPublisher : IAdminDomainEventPublisher
{
    public Task PublishAsync(AdminDomainEventEnvelope envelope, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
