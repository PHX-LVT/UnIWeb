using Contracts.Api;

namespace AdminSite.Services.Notifications;

public enum AdminFeedbackSeverity
{
    Success,
    Info,
    Warning,
    Error
}

public enum AdminFeedbackDisplayMode
{
    Toast,
    Inline,
    Silent
}

public sealed class AdminFeedbackMessage
{
    public AdminFeedbackSeverity Severity { get; init; } = AdminFeedbackSeverity.Info;
    public string? MessageKey { get; init; }
    public IReadOnlyList<object?>? MessageArgs { get; init; }
    public string? MessageFallback { get; init; }
    public AdminFeedbackDisplayMode DisplayMode { get; init; } = AdminFeedbackDisplayMode.Toast;
    public string? InlineTargetId { get; init; }
    public bool SuppressSuccess { get; init; }
    public string? TechnicalDetail { get; init; }
    public string? OperationId { get; init; }
    public string? ErrorCode { get; init; }
    public IReadOnlyList<ApiFieldError>? FieldErrors { get; init; }

    public static AdminFeedbackMessage FromResponse<T>(
        ApiResponse<T>? response,
        AdminFeedbackDisplayMode displayMode = AdminFeedbackDisplayMode.Toast,
        string? inlineTargetId = null,
        string? successFallback = null,
        string? failureFallback = null,
        bool suppressSuccess = false,
        string? technicalDetail = null)
    {
        if (response is null)
        {
            return new AdminFeedbackMessage
            {
                Severity = AdminFeedbackSeverity.Error,
                MessageKey = "NotificationNoResponse",
                MessageFallback = failureFallback ?? "No response from server.",
                DisplayMode = displayMode,
                InlineTargetId = inlineTargetId,
                TechnicalDetail = technicalDetail
            };
        }

        var success = response.Success;
        var args = response.NotificationArgs?.Select(value => (object?)value).ToList();

        return new AdminFeedbackMessage
        {
            Severity = success
                ? AdminFeedbackSeverity.Success
                : response.StatusCode is >= 200 and < 300
                    ? AdminFeedbackSeverity.Error
                    : SeverityFromStatusCode(response.StatusCode),
            MessageKey = response.NotificationKey,
            MessageArgs = args,
            MessageFallback = success
                ? successFallback ?? response.Message
                : failureFallback ?? response.Message,
            DisplayMode = displayMode,
            InlineTargetId = inlineTargetId,
            SuppressSuccess = suppressSuccess,
            TechnicalDetail = technicalDetail,
            OperationId = response.TraceId,
            ErrorCode = response.ErrorCode,
            FieldErrors = response.FieldErrors
        };
    }

    public static AdminFeedbackSeverity SeverityFromStatusCode(int statusCode) => statusCode switch
    {
        >= 200 and < 300 => AdminFeedbackSeverity.Success,
        400 or 404 or 409 or 410 or 413 or 422 or 429 => AdminFeedbackSeverity.Warning,
        401 or 403 => AdminFeedbackSeverity.Warning,
        _ => AdminFeedbackSeverity.Error
    };
}
