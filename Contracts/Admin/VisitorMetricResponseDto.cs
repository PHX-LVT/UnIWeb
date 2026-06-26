namespace Contracts.Admin;

public sealed class VisitorMetricResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string MetricType { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string TargetKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public DateTime Day { get; set; }
    public long Count { get; set; }
    public DateTime UpdatedAt { get; set; }
}