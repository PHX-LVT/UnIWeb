using Contracts.Api;

namespace FullProject.Utils;

public static class ApiOutcomePolicy
{
    public const string OperationActionItem = "OperationOutcome.ActionCode";
    public const string OperationDomainItem = "OperationOutcome.DomainCode";
    public const string OperationErrorItem = "OperationOutcome.ErrorCode";

    public static string ResolveErrorCode(int statusCode, string? message)
    {
        var value = message?.Trim() ?? string.Empty;
        if (Contains(value, "source page") && Contains(value, "not found", "no longer exists"))
            return "source-page-not-found";
        if (Contains(value, "not found", "missing", "no longer exists")) return "not-found";
        if (Contains(value, "permission", "forbid", "not authorized")) return "permission-denied";
        if (Contains(value, "expired")) return "expired";
        if (Contains(value, "too many", "rate limit")) return "rate-limited";
        if (Contains(value, "in use", "used by", "referenced")) return "in-use";
        if (Contains(value, "locked")) return "locked";
        if (Contains(value, "already exists", "conflict", "changed before")) return "conflict";
        if (Contains(value, "too large", "smaller", "size limit", "exceed")) return "size-exceeded";
        if (Contains(value, "unsupported", "does not support", "not allowed", "must be")) return "unsupported-value";
        if (Contains(value, "required", "invalid", "choose", "select", "cannot", "must")) return "validation-failed";
        if (Contains(value, "timeout", "timed out")) return "request-timeout";
        if (Contains(value, "storage", "r2", "service unavailable", "temporarily unavailable")) return "service-unavailable";

        return statusCode switch
        {
            StatusCodes.Status401Unauthorized => "authentication-required",
            StatusCodes.Status403Forbidden => "permission-denied",
            StatusCodes.Status404NotFound => "not-found",
            StatusCodes.Status409Conflict => "conflict",
            StatusCodes.Status410Gone => "expired",
            StatusCodes.Status413PayloadTooLarge => "size-exceeded",
            StatusCodes.Status422UnprocessableEntity => "validation-failed",
            StatusCodes.Status429TooManyRequests => "rate-limited",
            StatusCodes.Status502BadGateway or
            StatusCodes.Status503ServiceUnavailable or
            StatusCodes.Status504GatewayTimeout => "service-unavailable",
            >= 500 => "unexpected-error",
            _ => "operation-failed"
        };
    }

    public static string SafeFallback(int statusCode, string actionCode) =>
        $"{DisplayAction(actionCode)} failed: {DisplayReason(ResolveErrorCode(statusCode, null))}.";

    public static List<string> FailureNotificationArgs(string actionCode, string errorCode) =>
        [$"@action:{actionCode}", $"@reason:{errorCode}"];

    public static List<string> SuccessNotificationArgs(string actionCode) =>
        [$"@action:{actionCode}"];

    private static bool Contains(string value, params string[] fragments) =>
        fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static string DisplayAction(string actionCode) =>
        string.IsNullOrWhiteSpace(actionCode) ? "Operation" : actionCode.Split('.')[0].Replace('-', ' ');

    private static string DisplayReason(string errorCode) => errorCode.Replace('-', ' ');
}
