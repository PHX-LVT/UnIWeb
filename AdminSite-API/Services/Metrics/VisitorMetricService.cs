using FullProject.Data;
using Contracts.Admin;
using FullProject.Models;
using MongoDB.Driver;

namespace FullProject.Services.Metrics;

public sealed class VisitorMetricService
{
    public const string PageView = "page-view";
    public const string ContentPageView = "content-page-view";
    public const string Download = "download";
    public const string FormSubmission = "form-submission";

    private readonly MongoDbContext _context;

    public VisitorMetricService(MongoDbContext context)
    {
        _context = context;
    }

    public async Task IncrementAsync(string metricType, string targetType, string targetKey, string? label = null)
    {
        metricType = Clean(metricType, 80);
        targetType = Clean(targetType, 80);
        targetKey = Clean(targetKey, 1_000);
        label = Clean(label ?? targetKey, 300);

        if (string.IsNullOrWhiteSpace(metricType) ||
            string.IsNullOrWhiteSpace(targetType) ||
            string.IsNullOrWhiteSpace(targetKey))
        {
            return;
        }

        var day = DateTime.UtcNow.Date;
        var filter = Builders<VisitorMetricCounter>.Filter.Eq(m => m.MetricType, metricType) &
                     Builders<VisitorMetricCounter>.Filter.Eq(m => m.TargetType, targetType) &
                     Builders<VisitorMetricCounter>.Filter.Eq(m => m.TargetKey, targetKey) &
                     Builders<VisitorMetricCounter>.Filter.Eq(m => m.Day, day);

        var update = Builders<VisitorMetricCounter>.Update
            .SetOnInsert(m => m.MetricType, metricType)
            .SetOnInsert(m => m.TargetType, targetType)
            .SetOnInsert(m => m.TargetKey, targetKey)
            .SetOnInsert(m => m.Day, day)
            .Set(m => m.Label, label)
            .Set(m => m.UpdatedAt, DateTime.UtcNow)
            .Inc(m => m.Count, 1);

        await _context.VisitorMetrics.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
    }

    public async Task<List<VisitorMetricResponseDto>> GetAsync(string? metricType, DateTime? from, DateTime? to)
    {
        var filter = Builders<VisitorMetricCounter>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(metricType))
            filter &= Builders<VisitorMetricCounter>.Filter.Eq(m => m.MetricType, Clean(metricType, 80));
        if (from is not null)
            filter &= Builders<VisitorMetricCounter>.Filter.Gte(m => m.Day, from.Value.Date);
        if (to is not null)
            filter &= Builders<VisitorMetricCounter>.Filter.Lte(m => m.Day, to.Value.Date);

        var items = await _context.VisitorMetrics
            .Find(filter)
            .SortByDescending(m => m.Day)
            .ThenBy(m => m.MetricType)
            .ThenByDescending(m => m.Count)
            .Limit(500)
            .ToListAsync();

        return items.Select(Map).ToList();
    }

    private static VisitorMetricResponseDto Map(VisitorMetricCounter item) => new()
    {
        Id = item.Id,
        MetricType = item.MetricType,
        TargetType = item.TargetType,
        TargetKey = item.TargetKey,
        Label = item.Label,
        Day = item.Day,
        Count = item.Count,
        UpdatedAt = item.UpdatedAt
    };

    private static string Clean(string value, int maxLength)
    {
        var cleaned = (value ?? string.Empty).Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }
}
