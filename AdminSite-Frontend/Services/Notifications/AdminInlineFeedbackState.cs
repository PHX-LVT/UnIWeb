namespace AdminSite.Services.Notifications;

public sealed record AdminInlineFeedbackState(
    string TargetId,
    AdminFeedbackSeverity Severity,
    string Message,
    DateTimeOffset UpdatedAt,
    string? TechnicalDetail = null);
