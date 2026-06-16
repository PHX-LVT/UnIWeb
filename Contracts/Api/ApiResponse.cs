
namespace Contracts.Api;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }
    public int StatusCode { get; set; }

    public static ApiResponse<T> Fail(
        string message, int statusCode, List<string>? errors = null) => new()
        {
            Success = false,
            Message = message,
            StatusCode = statusCode,
            Errors = errors
        };
}

public class ApiResponse : ApiResponse<object> { }