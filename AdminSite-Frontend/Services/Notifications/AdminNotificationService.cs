using Blazored.Toast.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using System.Globalization;

namespace AdminSite.Services.Notifications;

public interface IAdminNotificationService
{
    event Action? InlineFeedbackChanged;

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

public sealed class AdminNotificationService : IAdminNotificationService, IDisposable
{
    private readonly IToastService _toast;
    private readonly IAdminLanguageContext _language;
    private readonly NavigationManager _navigation;
    private readonly ILogger<AdminNotificationService> _logger;
    private readonly Dictionary<string, AdminInlineFeedbackState> _inlineStatuses = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _shownOperations = new(StringComparer.Ordinal);

    public AdminNotificationService(
        IToastService toast,
        IAdminLanguageContext language,
        NavigationManager navigation,
        ILogger<AdminNotificationService> logger)
    {
        _toast = toast;
        _language = language;
        _navigation = navigation;
        _logger = logger;
        _navigation.LocationChanged += OnLocationChanged;
    }

    public event Action? InlineFeedbackChanged;

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
            _logger.LogWarning("Notification diagnostic detail: {TechnicalDetail}", message.TechnicalDetail);

        if (IsDuplicate(message.OperationId)) return;

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

        var language = _language.CurrentLanguage;
        var text = AdminUiLocalizer.T(key, language);
        if (string.Equals(text, key, StringComparison.Ordinal))
            return null;

        if (args is null || args.Count == 0)
            return text;

        try
        {
            var resolvedArgs = args
                .Select(value => value is string stringValue
                    ? AdminOutcomeTextResolver.ResolveArgument(stringValue, language)
                    : value)
                .ToArray();
            return string.Format(CultureInfo.CurrentCulture, text, resolvedArgs);
        }
        catch (FormatException)
        {
            _logger.LogWarning("Notification key {NotificationKey} has incompatible format arguments.", key);
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

    private bool IsDuplicate(string? operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId)) return false;
        var now = DateTimeOffset.UtcNow;
        foreach (var expired in _shownOperations.Where(pair => now - pair.Value > TimeSpan.FromMinutes(2)).Select(pair => pair.Key).ToList())
            _shownOperations.Remove(expired);
        if (_shownOperations.ContainsKey(operationId)) return true;
        _shownOperations[operationId] = now;
        return false;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        if (_inlineStatuses.Count == 0) return;
        _inlineStatuses.Clear();
        InlineFeedbackChanged?.Invoke();
    }

    public void Dispose() => _navigation.LocationChanged -= OnLocationChanged;
}
