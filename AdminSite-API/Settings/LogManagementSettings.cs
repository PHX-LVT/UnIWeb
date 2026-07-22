namespace FullProject.Settings;

public sealed class LogManagementSettings
{
    public int AuditActiveDays { get; set; } = 365;
    public int AuditTotalDays { get; set; } = 1095;
    public int LoginActiveDays { get; set; } = 90;
    public int LoginTotalDays { get; set; } = 365;
    public int CriticalSecurityTotalDays { get; set; } = 1095;
    public int ExportMaximumRows { get; set; } = 100_000;
    public int RetentionBatchSize { get; set; } = 2_000;
}
