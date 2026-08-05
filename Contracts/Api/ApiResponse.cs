
namespace Contracts.Api;

public interface IApiResponse
{
    bool Success { get; set; }
    string? Message { get; set; }
    string? NotificationKey { get; set; }
    List<string>? NotificationArgs { get; set; }
    List<string>? Errors { get; set; }
    int StatusCode { get; set; }
    int? RetryAfterSeconds { get; set; }
    string? ErrorCode { get; set; }
    List<ApiFieldError>? FieldErrors { get; set; }
    string? TraceId { get; set; }
    string? DomainCode { get; set; }
    string? ActionCode { get; set; }
}

public sealed class ApiFieldError
{
    public string FieldKey { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = "validation-failed";
    public string? MessageKey { get; set; }
    public List<string>? MessageArgs { get; set; }
    public string? Message { get; set; }
}

public class ApiResponse<T> : IApiResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? NotificationKey { get; set; }
    public List<string>? NotificationArgs { get; set; }
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }
    public int StatusCode { get; set; }
    public int? RetryAfterSeconds { get; set; }
    public string? ErrorCode { get; set; }
    public List<ApiFieldError>? FieldErrors { get; set; }
    public string? TraceId { get; set; }
    public string? DomainCode { get; set; }
    public string? ActionCode { get; set; }

    public static ApiResponse<T> Fail(
        string message,
        int statusCode,
        List<string>? errors = null,
        string? notificationKey = null,
        List<string>? notificationArgs = null,
        string? errorCode = null,
        List<ApiFieldError>? fieldErrors = null,
        string? traceId = null) => new()
        {
            Success = false,
            Message = message,
            StatusCode = statusCode,
            Errors = errors,
            NotificationKey = notificationKey,
            NotificationArgs = notificationArgs,
            ErrorCode = errorCode,
            FieldErrors = fieldErrors,
            TraceId = traceId
        };
}

public class ApiResponse : ApiResponse<object> { }
