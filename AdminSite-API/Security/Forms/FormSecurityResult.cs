namespace FullProject.Security.Forms;

internal sealed class FormSecurityResult
{
    public bool Accepted { get; init; }
    public int StatusCode { get; init; } = StatusCodes.Status400BadRequest;
    public string Message { get; init; } = string.Empty;
    public Dictionary<string, string> Data { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string IpAddress { get; init; } = string.Empty;
    public string UserAgent { get; init; } = string.Empty;
    public string Fingerprint { get; init; } = string.Empty;
}
