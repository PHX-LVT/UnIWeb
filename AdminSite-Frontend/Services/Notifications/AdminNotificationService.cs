using Blazored.Toast.Services;
using System.Globalization;

namespace AdminSite.Services.Notifications;

public interface IAdminNotificationService
{
    event Action? InlineFeedbackChanged;

    string? LastTechnicalDetail { get; }

    void Notify(AdminFeedbackMessage message);
    void Notify(string? message, int statusCode);
    void NotifyResponse<T>(
        ApiResponse<T>? response,
        string? successFallback = null,
        string? failureFallback = null,
        AdminFeedbackDisplayMode displayMode = AdminFeedbackDisplayMode.Toast,
        string? inlineTargetId = null);
    void SilentSuccess<T>(ApiResponse<T>? response, string? failureFallback = null);
    void SetInlineStatus(string targetId, AdminFeedbackSeverity severity, string message, string? technicalDetail = null);
    void ClearInlineStatus(string targetId);
    bool TryGetInlineStatus(string targetId, out AdminInlineFeedbackState status);
}

public sealed class AdminNotificationService : IAdminNotificationService
{
    private readonly IToastService _toast;
    private readonly Dictionary<string, AdminInlineFeedbackState> _inlineStatuses = new(StringComparer.Ordinal);

    public AdminNotificationService(IToastService toast)
    {
        _toast = toast;
    }

    public event Action? InlineFeedbackChanged;

    public string? LastTechnicalDetail { get; private set; }

    public void Notify(string? message, int statusCode)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        Notify(new AdminFeedbackMessage
        {
            Severity = AdminFeedbackMessage.SeverityFromStatusCode(statusCode),
            MessageFallback = message,
            DisplayMode = AdminFeedbackDisplayMode.Toast
        });
    }

    public void NotifyResponse<T>(
        ApiResponse<T>? response,
        string? successFallback = null,
        string? failureFallback = null,
        AdminFeedbackDisplayMode displayMode = AdminFeedbackDisplayMode.Toast,
        string? inlineTargetId = null)
    {
        Notify(AdminFeedbackMessage.FromResponse(
            response,
            displayMode,
            inlineTargetId,
            successFallback,
            failureFallback));
    }

    public void SilentSuccess<T>(ApiResponse<T>? response, string? failureFallback = null)
    {
        Notify(AdminFeedbackMessage.FromResponse(
            response,
            AdminFeedbackDisplayMode.Silent,
            successFallback: null,
            failureFallback: failureFallback,
            suppressSuccess: true));
    }

    public void Notify(AdminFeedbackMessage message)
    {
        var text = ResolveMessage(message);
        if (string.IsNullOrWhiteSpace(text)) return;

        if (!string.IsNullOrWhiteSpace(message.TechnicalDetail))
            LastTechnicalDetail = message.TechnicalDetail;

        var isSuccess = message.Severity is AdminFeedbackSeverity.Success or AdminFeedbackSeverity.Info;
        if (message.SuppressSuccess && isSuccess)
            return;

        if (message.DisplayMode == AdminFeedbackDisplayMode.Silent && isSuccess)
            return;

        if (message.DisplayMode == AdminFeedbackDisplayMode.Inline &&
            !string.IsNullOrWhiteSpace(message.InlineTargetId))
        {
            SetInlineStatus(message.InlineTargetId, message.Severity, text, message.TechnicalDetail);
            return;
        }

        ShowToast(message.Severity, text);
    }

    public void SetInlineStatus(string targetId, AdminFeedbackSeverity severity, string message, string? technicalDetail = null)
    {
        if (string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(message)) return;

        _inlineStatuses[targetId] = new AdminInlineFeedbackState(
            targetId,
            severity,
            message,
            DateTimeOffset.UtcNow,
            technicalDetail);
        InlineFeedbackChanged?.Invoke();
    }

    public void ClearInlineStatus(string targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId)) return;
        if (_inlineStatuses.Remove(targetId))
            InlineFeedbackChanged?.Invoke();
    }

    public bool TryGetInlineStatus(string targetId, out AdminInlineFeedbackState status) =>
        _inlineStatuses.TryGetValue(targetId, out status!);

    private string? ResolveMessage(AdminFeedbackMessage message)
    {
        var resolved = ResolveKey(message.MessageKey, message.MessageArgs);
        if (!string.IsNullOrWhiteSpace(resolved))
            return resolved;

        return message.MessageFallback;
    }

    private string? ResolveKey(string? key, IReadOnlyList<object?>? args)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;

        var text = AdminUiLocalizer.T(key, AdminUiLocalizer.FallbackLanguage);
        if (string.Equals(text, key, StringComparison.Ordinal))
            return null;

        if (args is null || args.Count == 0)
            return text;

        try
        {
            return string.Format(CultureInfo.CurrentCulture, text, args.ToArray());
        }
        catch (FormatException)
        {
            LastTechnicalDetail = $"Notification key '{key}' has incompatible format arguments.";
            return text;
        }
    }

    private void ShowToast(AdminFeedbackSeverity severity, string message)
    {
        switch (severity)
        {
            case AdminFeedbackSeverity.Success:
                _toast.ShowSuccess(message);
                break;
            case AdminFeedbackSeverity.Info:
                _toast.ShowInfo(message);
                break;
            case AdminFeedbackSeverity.Warning:
                _toast.ShowWarning(message);
                break;
            case AdminFeedbackSeverity.Error:
            default:
                _toast.ShowError(message);
                break;
        }
    }
}
