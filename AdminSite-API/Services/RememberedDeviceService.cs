using Contracts.Auth;
using FullProject.Models;
using FullProject.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Security.Cryptography;
using System.Text;

namespace FullProject.Services;

public enum RememberedDeviceExchangeStatus
{
    Succeeded,
    Disabled,
    Invalid,
    Expired,
    Revoked,
    ReuseDetected
}

public sealed record RememberedDeviceIssue(
    AdminRememberedDeviceRecord Record,
    string Credential);

public sealed record RememberedDeviceExchange(
    RememberedDeviceExchangeStatus Status,
    AdminRememberedDeviceRecord? Record = null,
    string? Credential = null);

public sealed class RememberedDeviceService
{
    private static readonly TimeSpan LegacyRotationOverlap = TimeSpan.FromMinutes(1);
    private readonly IMongoCollection<AdminRememberedDeviceRecord> _devices;
    private readonly RememberedDeviceSettings _settings;

    public RememberedDeviceService(
        IMongoDatabase database,
        IOptions<RememberedDeviceSettings> settings)
    {
        _devices = database.GetCollection<AdminRememberedDeviceRecord>("admin_remembered_devices");
        _settings = settings.Value;
    }

    public bool Enabled => _settings.Enabled;

    public async Task<RememberedDeviceIssue?> CreateAsync(
        AdminUser user,
        string ipAddress,
        string userAgent,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled) return null;

        var now = DateTime.UtcNow;
        var activeFilter = Builders<AdminRememberedDeviceRecord>.Filter.And(
            Builders<AdminRememberedDeviceRecord>.Filter.Eq(device => device.AdminId, user.Id),
            Builders<AdminRememberedDeviceRecord>.Filter.Eq(device => device.IsRevoked, false),
            Builders<AdminRememberedDeviceRecord>.Filter.Gt(device => device.ExpiresAt, now));
        var activeCount = await _devices.CountDocumentsAsync(activeFilter, cancellationToken: cancellationToken);
        var maximum = Math.Clamp(_settings.MaximumDevicesPerAccount, 1, 20);
        var revokeCount = Math.Max(0, activeCount - maximum + 1);
        if (revokeCount > 0)
        {
            var oldestIds = await _devices.Find(activeFilter)
                .SortBy(device => device.LastUsedAt)
                .Limit((int)Math.Min(revokeCount, maximum))
                .Project(device => device.Id)
                .ToListAsync(cancellationToken);
            if (oldestIds.Count > 0)
            {
                await _devices.UpdateManyAsync(
                    device => oldestIds.Contains(device.Id) && !device.IsRevoked,
                    RevokeUpdate(user.Id, AdminRememberedDeviceRevokeReason.DeviceLimit, now),
                    cancellationToken: cancellationToken);
            }
        }

        var secret = GenerateSecret();
        var record = new AdminRememberedDeviceRecord
        {
            Id = ObjectId.GenerateNewId().ToString(),
            AdminId = user.Id,
            Email = user.Email,
            SecretHash = HashSecret(secret),
            TokenVersion = user.TokenVersion,
            CreatedAt = now,
            LastUsedAt = now,
            ExpiresAt = now.AddDays(Math.Clamp(_settings.LifetimeDays, 1, 365)),
            LastUsedIp = ipAddress,
            UserAgent = Limit(userAgent, 500),
            BrowserName = ParseBrowser(userAgent),
            OperatingSystem = ParseOperatingSystem(userAgent)
        };

        await _devices.InsertOneAsync(record, cancellationToken: cancellationToken);
        return new RememberedDeviceIssue(record, FormatCredential(record.Id, secret));
    }

    public async Task<RememberedDeviceExchange> ExchangeAsync(
        string credential,
        string ipAddress,
        string userAgent,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
            return new RememberedDeviceExchange(RememberedDeviceExchangeStatus.Disabled);
        if (!TryParseCredential(credential, out var deviceId, out var secret))
            return new RememberedDeviceExchange(RememberedDeviceExchangeStatus.Invalid);

        var record = await _devices.Find(device => device.Id == deviceId)
            .FirstOrDefaultAsync(cancellationToken);
        if (record is null)
            return new RememberedDeviceExchange(RememberedDeviceExchangeStatus.Invalid);
        if (record.IsRevoked)
            return new RememberedDeviceExchange(RememberedDeviceExchangeStatus.Revoked, record);

        var now = DateTime.UtcNow;
        if (record.ExpiresAt <= now)
        {
            await _devices.UpdateOneAsync(
                device => device.Id == record.Id && !device.IsRevoked,
                RevokeUpdate(record.AdminId, AdminRememberedDeviceRevokeReason.Expired, now),
                cancellationToken: cancellationToken);
            return new RememberedDeviceExchange(RememberedDeviceExchangeStatus.Expired, record);
        }

        var presentedHash = HashSecret(secret);
        var matchesCurrent = HashesEqual(presentedHash, record.SecretHash);
        var matchesRecentPrevious =
            HashesEqual(presentedHash, record.PreviousSecretHash) &&
            now - record.LastUsedAt <= LegacyRotationOverlap;
        if (!matchesCurrent && !matchesRecentPrevious)
            return new RememberedDeviceExchange(RememberedDeviceExchangeStatus.Invalid, record);

        var filter = Builders<AdminRememberedDeviceRecord>.Filter.And(
            Builders<AdminRememberedDeviceRecord>.Filter.Eq(device => device.Id, record.Id),
            Builders<AdminRememberedDeviceRecord>.Filter.Eq(device => device.IsRevoked, false),
            Builders<AdminRememberedDeviceRecord>.Filter.Gt(device => device.ExpiresAt, now));
        filter &= matchesCurrent
            ? Builders<AdminRememberedDeviceRecord>.Filter.Eq(device => device.SecretHash, presentedHash)
            : Builders<AdminRememberedDeviceRecord>.Filter.Or(
                Builders<AdminRememberedDeviceRecord>.Filter.Eq(device => device.PreviousSecretHash, presentedHash),
                Builders<AdminRememberedDeviceRecord>.Filter.Eq(device => device.SecretHash, presentedHash));
        var update = Builders<AdminRememberedDeviceRecord>.Update
            .Set(device => device.SecretHash, presentedHash)
            .Unset(device => device.PreviousSecretHash)
            .Set(device => device.LastUsedAt, now)
            .Set(device => device.LastUsedIp, ipAddress)
            .Set(device => device.UserAgent, Limit(userAgent, 500))
            .Set(device => device.BrowserName, ParseBrowser(userAgent))
            .Set(device => device.OperatingSystem, ParseOperatingSystem(userAgent));
        var refreshed = await _devices.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<AdminRememberedDeviceRecord>
            {
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken);

        if (refreshed is null)
        {
            var latest = await _devices.Find(device => device.Id == record.Id)
                .FirstOrDefaultAsync(cancellationToken);
            return new RememberedDeviceExchange(RememberedDeviceExchangeStatus.Invalid, latest ?? record);
        }

        return new RememberedDeviceExchange(
            RememberedDeviceExchangeStatus.Succeeded,
            refreshed,
            FormatCredential(refreshed.Id, secret));
    }

    public async Task<bool> RevokeCredentialAsync(
        string? credential,
        string actorId,
        AdminRememberedDeviceRevokeReason reason,
        string? expectedAdminId = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseCredential(credential, out var deviceId, out var secret)) return false;
        var record = await _devices.Find(device => device.Id == deviceId)
            .FirstOrDefaultAsync(cancellationToken);
        if (record is null || record.IsRevoked) return false;
        if (!string.IsNullOrWhiteSpace(expectedAdminId) &&
            !string.Equals(record.AdminId, expectedAdminId, StringComparison.Ordinal))
            return false;

        var presentedHash = HashSecret(secret);
        if (!HashesEqual(presentedHash, record.SecretHash) &&
            !HashesEqual(presentedHash, record.PreviousSecretHash))
            return false;

        var result = await _devices.UpdateOneAsync(
            device => device.Id == record.Id && !device.IsRevoked,
            RevokeUpdate(actorId, reason, DateTime.UtcNow),
            cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task RevokeAllAsync(
        string adminId,
        string actorId,
        AdminRememberedDeviceRevokeReason reason,
        CancellationToken cancellationToken = default) =>
        await _devices.UpdateManyAsync(
            device => device.AdminId == adminId && !device.IsRevoked,
            RevokeUpdate(actorId, reason, DateTime.UtcNow),
            cancellationToken: cancellationToken);

    public async Task RevokeAllForUsersAsync(
        IReadOnlyCollection<string> adminIds,
        string actorId,
        AdminRememberedDeviceRevokeReason reason,
        CancellationToken cancellationToken = default)
    {
        if (adminIds.Count == 0) return;
        await _devices.UpdateManyAsync(
            device => adminIds.Contains(device.AdminId) && !device.IsRevoked,
            RevokeUpdate(actorId, reason, DateTime.UtcNow),
            cancellationToken: cancellationToken);
    }

    public async Task<(List<AdminRememberedDeviceRecord> Items, long TotalCount, int Page, int PageSize)> GetPageAsync(
        int page,
        int pageSize,
        string? adminId = null,
        bool includeRevoked = true,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<AdminRememberedDeviceRecord>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(adminId))
            filter &= Builders<AdminRememberedDeviceRecord>.Filter.Eq(device => device.AdminId, adminId);
        if (!includeRevoked)
            filter &= Builders<AdminRememberedDeviceRecord>.Filter.Eq(device => device.IsRevoked, false);

        pageSize = Math.Clamp(pageSize, 10, 100);
        var totalCount = await _devices.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        var items = await _devices.Find(filter)
            .SortByDescending(device => device.LastUsedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount, page, pageSize);
    }

    public async Task<long> RevokeByIdsAsync(
        IEnumerable<string>? ids,
        string actorId,
        AdminRememberedDeviceRevokeReason reason,
        string? expectedAdminId = null,
        CancellationToken cancellationToken = default)
    {
        var selectedIds = (ids ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (selectedIds.Count == 0) return 0;
        var filter = Builders<AdminRememberedDeviceRecord>.Filter.And(
            Builders<AdminRememberedDeviceRecord>.Filter.In(device => device.Id, selectedIds),
            Builders<AdminRememberedDeviceRecord>.Filter.Eq(device => device.IsRevoked, false));
        if (!string.IsNullOrWhiteSpace(expectedAdminId))
            filter &= Builders<AdminRememberedDeviceRecord>.Filter.Eq(device => device.AdminId, expectedAdminId);
        var result = await _devices.UpdateManyAsync(
            filter,
            RevokeUpdate(actorId, reason, DateTime.UtcNow),
            cancellationToken: cancellationToken);
        return result.ModifiedCount;
    }

    private static UpdateDefinition<AdminRememberedDeviceRecord> RevokeUpdate(
        string actorId,
        AdminRememberedDeviceRevokeReason reason,
        DateTime now) =>
        Builders<AdminRememberedDeviceRecord>.Update
            .Set(device => device.IsRevoked, true)
            .Set(device => device.RevokedAt, now)
            .Set(device => device.RevokedById, actorId)
            .Set(device => device.RevokeReason, reason);

    private string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(Math.Clamp(_settings.TokenBytes, 32, 64));
        return Base64UrlEncode(bytes);
    }

    private static string HashSecret(string secret) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private static bool HashesEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(left),
                Convert.FromBase64String(right));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string FormatCredential(string deviceId, string secret) => $"{deviceId}.{secret}";

    private static bool TryParseCredential(string? credential, out string deviceId, out string secret)
    {
        deviceId = string.Empty;
        secret = string.Empty;
        if (string.IsNullOrWhiteSpace(credential) || credential.Length > 512) return false;
        var separator = credential.IndexOf('.');
        if (separator <= 0 || separator == credential.Length - 1 ||
            credential.IndexOf('.', separator + 1) >= 0)
            return false;
        deviceId = credential[..separator];
        secret = credential[(separator + 1)..];
        return ObjectId.TryParse(deviceId, out _) && secret.Length >= 40;
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Limit(string? value, int maximum) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Length <= maximum ? value : value[..maximum];

    private static string ParseBrowser(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return "Unknown";
        if (userAgent.Contains("Edg/", StringComparison.OrdinalIgnoreCase)) return "Edge";
        if (userAgent.Contains("Chrome/", StringComparison.OrdinalIgnoreCase)) return "Chrome";
        if (userAgent.Contains("Firefox/", StringComparison.OrdinalIgnoreCase)) return "Firefox";
        if (userAgent.Contains("Safari/", StringComparison.OrdinalIgnoreCase)) return "Safari";
        return "Unknown";
    }

    private static string ParseOperatingSystem(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return "Unknown";
        if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase)) return "Windows";
        if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase)) return "Android";
        if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase)) return "iOS";
        if (userAgent.Contains("Mac OS", StringComparison.OrdinalIgnoreCase)) return "macOS";
        if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase)) return "Linux";
        return "Unknown";
    }
}
