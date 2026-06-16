using Contracts.Api;


namespace FullProject.Utils
{
    // ═══════════════════════════════════════════════════════════
    // RESPONSE WRAPPER
    // Every endpoint returns ApiResponse<T> for consistency.
    // ═══════════════════════════════════════════════════════════

  
    public static class ApiResult
    {
        public static ApiResponse<T> Ok<T>(T data, string? message = null) => new()
        {
            Success = true,
            Data = data,
            StatusCode = 200,
            Message = message
        };

        public static ApiResponse Ok(string? message = null) => new()
        {
            Success = true,
            StatusCode = 200,
            Message = message
        };

        public static ApiResponse<T> Created<T>(T data, string? message = null) => new()
        {
            Success = true,
            Data = data,
            StatusCode = 201,
            Message = message
        };

        public static ApiResponse<T> NotFound<T>(string message = "Not found.") => new()
        {
            Success = false,
            StatusCode = 404,
            Message = message
        };

        public static ApiResponse NotFound(string message = "Not found.") => new()
        {
            Success = false,
            StatusCode = 404,
            Message = message
        };

        public static ApiResponse<T> BadRequest<T>(string message, List<string>? errors = null) => new()
        {
            Success = false,
            StatusCode = 400,
            Message = message,
            Errors = errors
        };

        public static ApiResponse BadRequest(string message, List<string>? errors = null) => new()
        {
            Success = false,
            StatusCode = 400,
            Message = message,
            Errors = errors
        };

        public static ApiResponse<T> Unauthorized<T>(string message = "Unauthorized.") => new()
        {
            Success = false,
            StatusCode = 401,
            Message = message
        };

        public static ApiResponse<T> Unprocessable<T>(List<string> errors) => new()
        {
            Success = false,
            StatusCode = 422,
            Message = "Validation failed.",
            Errors = errors
        };

        public static ApiResponse<T> ServerError<T>(string message = "An unexpected error occurred.") => new()
        {
            Success = false,
            StatusCode = 500,
            Message = message
        };

        public static ApiResponse ServerError(string message = "An unexpected error occurred.") => new()
        {
            Success = false,
            StatusCode = 500,
            Message = message
        };
    }
}
