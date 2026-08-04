
namespace Contracts.Api;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? NotificationKey { get; set; }
    public List<string>? NotificationArgs { get; set; }
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }
    public int StatusCode { get; set; }
    public int? RetryAfterSeconds { get; set; }

    public static ApiResponse<T> Fail(
        string message,
        int statusCode,
        List<string>? errors = null,
        string? notificationKey = null,
        List<string>? notificationArgs = null) => new()
        {
            Success = false,
            Message = message,
            StatusCode = statusCode,
            Errors = errors,
            NotificationKey = notificationKey,
            NotificationArgs = notificationArgs
        };
}

public class ApiResponse : ApiResponse<object> { }
