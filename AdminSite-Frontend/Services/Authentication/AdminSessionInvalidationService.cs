using System.Collections.Concurrent;

namespace AdminSite.Services.Authentication;

public enum AdminSessionInvalidationScope
{
    Account,
    Token
}

public sealed record AdminSessionInvalidation(
    AdminSessionInvalidationScope Scope,
    string AdminId,
    string? TokenId,
    string Reason)
{
    public bool Matches(string adminId, string tokenId) =>
        string.Equals(AdminId, adminId, StringComparison.OrdinalIgnoreCase) &&
        (Scope == AdminSessionInvalidationScope.Account ||
         string.Equals(TokenId, tokenId, StringComparison.Ordinal));
}

public sealed class AdminSessionInvalidationService
{
    private readonly ConcurrentDictionary<Guid, Action<AdminSessionInvalidation>> _subscribers = new();
    private readonly ILogger<AdminSessionInvalidationService> _logger;

    public AdminSessionInvalidationService(ILogger<AdminSessionInvalidationService> logger)
    {
        _logger = logger;
    }

    public IDisposable Subscribe(Action<AdminSessionInvalidation> subscriber)
    {
        var id = Guid.NewGuid();
        _subscribers[id] = subscriber;
        return new Subscription(_subscribers, id);
    }

    public void InvalidateAccount(string adminId, string reason)
    {
        if (string.IsNullOrWhiteSpace(adminId)) return;
        Publish(new AdminSessionInvalidation(
            AdminSessionInvalidationScope.Account,
            adminId,
            null,
            NormalizeReason(reason)));
    }

    public void InvalidateToken(string adminId, string tokenId, string reason)
    {
        if (string.IsNullOrWhiteSpace(adminId) || string.IsNullOrWhiteSpace(tokenId)) return;
        Publish(new AdminSessionInvalidation(
            AdminSessionInvalidationScope.Token,
            adminId,
            tokenId,
            NormalizeReason(reason)));
    }

    private void Publish(AdminSessionInvalidation invalidation)
    {
        foreach (var subscriber in _subscribers.Values)
        {
            try
            {
                subscriber(invalidation);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "An inactive AdminSite circuit ignored a session invalidation event.");
            }
        }
    }

    public static string NormalizeReason(string? reason) => reason switch
    {
        "signed-out" => "signed-out",
        "session-expired" => "session-expired",
        "password-changed" => "password-changed",
        "password-reset" => "password-reset",
        "account-disabled" => "account-disabled",
        "account-deleted" => "account-deleted",
        "account-updated" => "account-updated",
        _ => "session-expired"
    };

    private sealed class Subscription : IDisposable
    {
        private readonly ConcurrentDictionary<Guid, Action<AdminSessionInvalidation>> _subscribers;
        private readonly Guid _id;
        private int _disposed;

        public Subscription(
            ConcurrentDictionary<Guid, Action<AdminSessionInvalidation>> subscribers,
            Guid id)
        {
            _subscribers = subscribers;
            _id = id;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                _subscribers.TryRemove(_id, out _);
        }
    }
}
