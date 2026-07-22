namespace Contracts.Public;

public enum PublicOperationStatus
{
    Success,
    NotConfigured,
    NotFound,
    ValidationFailed,
    Unauthorized,
    RateLimited,
    Unavailable,
    Timeout,
    InvalidResponse
}

public sealed record PublicOperationResult<T>(
    PublicOperationStatus Status,
    T? Data = default,
    string Message = "",
    string? CorrelationId = null)
{
    public bool IsSuccess => Status == PublicOperationStatus.Success;
    public bool IsAvailable => Status is PublicOperationStatus.Success or PublicOperationStatus.NotConfigured;
    public bool IsRetryable => Status is PublicOperationStatus.RateLimited or PublicOperationStatus.Unavailable or PublicOperationStatus.Timeout;

    public static PublicOperationResult<T> Succeeded(T data) =>
        new(PublicOperationStatus.Success, data);

    public static PublicOperationResult<T> Failed(
        PublicOperationStatus status,
        string message,
        string? correlationId = null) =>
        new(status, default, message, correlationId);
}

public sealed record PublicFormSubmitResult(
    PublicOperationStatus Status,
    string Message = "",
    string? CorrelationId = null)
{
    public bool Success => Status == PublicOperationStatus.Success;
    public bool IsRetryable => Status is PublicOperationStatus.RateLimited or PublicOperationStatus.Unavailable or PublicOperationStatus.Timeout;

    public static PublicFormSubmitResult Accepted(string message = "") =>
        new(PublicOperationStatus.Success, message);

    public static PublicFormSubmitResult Rejected(
        PublicOperationStatus status,
        string message,
        string? correlationId = null) =>
        new(status, message, correlationId);
}
