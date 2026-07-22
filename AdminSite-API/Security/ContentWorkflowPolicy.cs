using Contracts.Auth;
using FullProject.Models;
using System.Security.Claims;

namespace FullProject.Security;

public sealed class ContentWorkflowPolicy
{
    public bool CanViewModule(ClaimsPrincipal user) =>
        AdminAuthorization.IsAdminAdmin(user) ||
        AdminAuthorization.HasPermission(user, AdminPermissionKeys.ViewContent);

    public bool CanCreate(ClaimsPrincipal user) =>
        AdminAuthorization.IsAdminAdmin(user) ||
        AdminAuthorization.HasPermission(user, AdminPermissionKeys.CreateEditContent);

    public bool CanApprove(ClaimsPrincipal user) =>
        AdminAuthorization.IsAdminAdmin(user) ||
        AdminAuthorization.HasPermission(user, AdminPermissionKeys.ApproveContent);

    public IEnumerable<ContentItem> ApplyVisibility(
        ClaimsPrincipal user,
        string actorId,
        IEnumerable<ContentItem> items,
        string? scope)
    {
        if (!CanViewModule(user)) return [];

        var normalizedScope = (scope ?? "all").Trim().ToLowerInvariant();
        var isAdmin = AdminAuthorization.IsAdminAdmin(user);
        var canAuthor = AdminAuthorization.HasPermission(user, AdminPermissionKeys.CreateEditContent);
        var canApprove = AdminAuthorization.HasPermission(user, AdminPermissionKeys.ApproveContent);

        return normalizedScope switch
        {
            "my" => items.Where(item =>
                IsOwner(item, actorId) &&
                IsMyContentStatus(item)),

            "submitted" => items.Where(item =>
                item.Status == ContentStatus.Submitted &&
                (isAdmin || canApprove || (canAuthor && IsOwner(item, actorId)))),

            "published" => isAdmin || canAuthor || canApprove
                ? items.Where(item =>
                    item.Status == ContentStatus.Published &&
                    IsOwner(item, actorId))
                : [],

            "deleted" => isAdmin
                ? items.Where(item => item.Status == ContentStatus.Deleted)
                : [],

            _ => isAdmin || canApprove
                ? items.Where(item =>
                    item.Status != ContentStatus.Deleted)
                : items.Where(item => item.Status == ContentStatus.Published)
        };
    }

    public bool CanRead(ClaimsPrincipal user, string actorId, ContentItem item)
    {
        if (!CanViewModule(user)) return false;
        if (AdminAuthorization.IsAdminAdmin(user)) return true;
        if (item.Status == ContentStatus.Deleted) return false;
        if (item.Status == ContentStatus.Published) return true;
        if (AdminAuthorization.HasPermission(user, AdminPermissionKeys.ApproveContent)) return true;

        return AdminAuthorization.HasPermission(user, AdminPermissionKeys.CreateEditContent) &&
               IsOwner(item, actorId);
    }

    public bool CanEdit(ClaimsPrincipal user, string actorId, ContentItem item)
    {
        if (item.Status == ContentStatus.Deleted) return false;
        if (AdminAuthorization.IsAdminAdmin(user)) return true;

        if (AdminAuthorization.HasPermission(user, AdminPermissionKeys.ApproveContent) &&
            (item.Status is ContentStatus.Submitted or ContentStatus.Published))
            return true;

        return AdminAuthorization.HasPermission(user, AdminPermissionKeys.CreateEditContent) &&
               IsOwner(item, actorId) &&
               item.Status == ContentStatus.Draft;
    }

    public bool CanSubmit(ClaimsPrincipal user, string actorId, ContentItem item) =>
        item.Status == ContentStatus.Draft &&
        item.ReviewStatus != ContentReviewStatus.Rejected &&
        (AdminAuthorization.IsAdminAdmin(user) ||
         (AdminAuthorization.HasPermission(user, AdminPermissionKeys.CreateEditContent) &&
          IsOwner(item, actorId)));

    public bool CanWithdraw(ClaimsPrincipal user, string actorId, ContentItem item) =>
        item.Status == ContentStatus.Submitted &&
        (AdminAuthorization.IsAdminAdmin(user) ||
         (AdminAuthorization.HasPermission(user, AdminPermissionKeys.CreateEditContent) &&
          IsOwner(item, actorId)));

    public bool CanReject(ClaimsPrincipal user, ContentItem item) =>
        item.Status == ContentStatus.Submitted &&
        (AdminAuthorization.IsAdminAdmin(user) ||
         AdminAuthorization.HasPermission(user, AdminPermissionKeys.ApproveContent));

    public bool CanPublish(ClaimsPrincipal user, ContentItem item) =>
        (item.Status == ContentStatus.Submitted &&
         (AdminAuthorization.IsAdminAdmin(user) ||
          AdminAuthorization.HasPermission(user, AdminPermissionKeys.ApproveContent))) ||
        (item.Status == ContentStatus.Draft &&
         AdminAuthorization.IsAdminAdmin(user));

    public bool CanReturnPublishedToPending(ClaimsPrincipal user, ContentItem item) =>
        item.Status == ContentStatus.Published &&
        (AdminAuthorization.IsAdminAdmin(user) ||
         AdminAuthorization.HasPermission(user, AdminPermissionKeys.ApproveContent));

    public bool CanForceReturnToDraft(ClaimsPrincipal user, ContentItem item) =>
        AdminAuthorization.IsAdminAdmin(user) &&
        item.Status != ContentStatus.Deleted;

    public bool CanDelete(ClaimsPrincipal user, string actorId, ContentItem item)
    {
        if (item.Status is ContentStatus.Published or ContentStatus.Deleted)
            return false;

        if (AdminAuthorization.IsAdminAdmin(user)) return true;

        return AdminAuthorization.HasPermission(user, AdminPermissionKeys.CreateEditContent) &&
               IsOwner(item, actorId) &&
               item.Status == ContentStatus.Draft;
    }

    public bool CanRestore(ClaimsPrincipal user, ContentItem item) =>
        AdminAuthorization.IsAdminAdmin(user) &&
        item.Status == ContentStatus.Deleted;

    public bool CanPermanentlyDelete(ClaimsPrincipal user) =>
        AdminAuthorization.IsAdminAdmin(user);

    public bool CanPreview(ClaimsPrincipal user, string actorId, ContentItem item) =>
        item.Status == ContentStatus.Published &&
        CanRead(user, actorId, item);

    public bool CanViewHistory(ClaimsPrincipal user, string actorId, ContentItem item) =>
        CanViewModule(user) &&
        (AdminAuthorization.IsAdminAdmin(user) ||
         AdminAuthorization.HasPermission(user, AdminPermissionKeys.ApproveContent) ||
         (AdminAuthorization.HasPermission(user, AdminPermissionKeys.CreateEditContent) &&
          IsOwner(item, actorId)));

    public static bool IsOwner(ContentItem item, string actorId) =>
        !string.IsNullOrWhiteSpace(actorId) &&
        string.Equals(item.AuthorId, actorId, StringComparison.OrdinalIgnoreCase);

    private static bool IsMyContentStatus(ContentItem item) =>
        item.Status is ContentStatus.Draft or
            ContentStatus.Submitted or
            ContentStatus.Published;

}
