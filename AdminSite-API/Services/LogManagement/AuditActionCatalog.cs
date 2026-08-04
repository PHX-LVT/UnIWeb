using Microsoft.AspNetCore.Mvc.Controllers;

namespace FullProject.Services.LogManagement;

public sealed record AuditActionDefinition(
    string DomainCode,
    string ActionCode,
    string TargetTypeCode,
    bool IsCritical = false,
    bool ShouldAudit = true);

public static class AuditActionCatalog
{
    private static readonly IReadOnlyDictionary<string, (string Domain, string Target)> Controllers =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["AdminUsers"] = ("user-management", "user"),
            ["AdminRoles"] = ("role-management", "role"),
            ["Pages"] = ("page-builder", "page"),
            ["ChildPages"] = ("page-builder", "child-page"),
            ["Sections"] = ("page-builder", "section"),
            ["Blocks"] = ("page-builder", "block"),
            ["SectionPresets"] = ("page-builder", "section-preset"),
            ["Content"] = ("content", "content"),
            ["Forms"] = ("forms", "form"),
            ["Assets"] = ("assets", "asset"),
            ["ManagedResources"] = ("assets", "resource"),
            ["ResourceUploads"] = ("assets", "resource-upload"),
            ["Settings"] = ("settings", "settings"),
            ["Theme"] = ("settings", "theme"),
            ["Branding"] = ("settings", "branding"),
            ["GlobalButtons"] = ("settings", "global-button"),
            ["Footer"] = ("settings", "footer"),
            ["Social"] = ("settings", "social-button"),
            ["VisitorMetrics"] = ("website-activity", "visitor-metrics"),
            ["LogManagement"] = ("system", "log-management")
        };

    private static readonly IReadOnlyDictionary<string, string> ExplicitVerbs =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Publish"] = "published",
            ["Reset"] = "reset",
            ["Restore"] = "restored",
            ["Reorder"] = "reordered",
            ["UpdateVisibility"] = "visibility-updated",
            ["SetVisibility"] = "visibility-updated",
            ["UpdateAccess"] = "access-updated",
            ["SetAccess"] = "access-updated",
            ["UpdateStatus"] = "status-updated",
            ["BulkStatus"] = "status-updated",
            ["Upload"] = "uploaded",
            ["UploadBatch"] = "batch-uploaded",
            ["Replace"] = "replaced",
            ["Duplicate"] = "duplicated",
            ["Apply"] = "applied",
            ["ResetPassword"] = "password-reset",
            ["UpdatePassword"] = "password-changed",
            ["Export"] = "exported",
            ["RunRetention"] = "retention-run"
        };

    private static readonly IReadOnlyDictionary<string, AuditActionDefinition> ExplicitActions =
        new Dictionary<string, AuditActionDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["AdminRoles.GetUpdateImpact"] = new("role-management", "role.impact-calculated", "role", ShouldAudit: false),
            ["AdminUsers.RevokeRememberedDevices"] = new("authentication", "remembered-device.revoked", "remembered-device", true),
            ["AdminUsers.RevokeMyRememberedDevices"] = new("authentication", "remembered-device.revoked", "remembered-device", true),

            ["Pages.RestoreRevision"] = new("page-builder", "page.revision-restored", "page", true),
            ["ChildPages.UpdateCard"] = new("page-builder", "child-page.card-updated", "child-page"),
            ["ChildPages.ResetCard"] = new("page-builder", "child-page.card-reset", "child-page", true),
            ["Sections.UpdateStyle"] = new("page-builder", "section.style-updated", "section"),
            ["Blocks.UpdateLayout"] = new("page-builder", "block.layout-updated", "block"),
            ["Blocks.UpdateLayouts"] = new("page-builder", "block.layouts-updated", "block"),
            ["Blocks.UpdateAuthoringLock"] = new("page-builder", "block.authoring-lock-updated", "block"),
            ["Blocks.DeleteGraphs"] = new("page-builder", "block-graph.deleted", "block-graph", true),

            ["Content.CreateType"] = new("content", "content-type.created", "content-type"),
            ["Content.UpdateType"] = new("content", "content-type.updated", "content-type"),
            ["Content.DeleteType"] = new("content", "content-type.deleted", "content-type", true),
            ["Content.SetStatus"] = new("content", "content.status-updated", "content"),
            ["Content.PermanentDelete"] = new("content", "content.permanently-deleted", "content", true),
            ["Content.RestoreRevision"] = new("content", "content.revision-restored", "content", true),

            ["Forms.UpdateSubmission"] = new("forms", "form-submission.updated", "form-submission"),
            ["Forms.BulkStatus"] = new("forms", "form-submission.status-bulk-updated", "form-submission"),
            ["Forms.BulkDelete"] = new("forms", "form-submission.bulk-deleted", "form-submission", true),
            ["Forms.Delete"] = new("forms", "form-submission.deleted", "form-submission", true),
            ["Forms.ReorderDefinitions"] = new("forms", "form-definition.reordered", "form-definition"),
            ["Forms.CreateDefinition"] = new("forms", "form-definition.created", "form-definition"),
            ["Forms.UpdateDefinition"] = new("forms", "form-definition.updated", "form-definition"),
            ["Forms.UpdateInputType"] = new("forms", "form-input-type.updated", "form-input-type"),
            ["Forms.DeleteDefinition"] = new("forms", "form-definition.deleted", "form-definition", true),

            ["ManagedResources.CreateAlbum"] = new("assets", "resource-album.created", "resource-album"),
            ["ManagedResources.UpdateAlbum"] = new("assets", "resource-album.updated", "resource-album"),
            ["ManagedResources.DeleteAlbum"] = new("assets", "resource-album.deleted", "resource-album", true),
            ["ManagedResources.AssignResourcesToAlbum"] = new("assets", "resource-album.resources-assigned", "resource-album"),
            ["ManagedResources.ReplaceUpload"] = new("assets", "resource.replaced", "resource", true),
            ["ResourceUploads.Initiate"] = new("assets", "resource-upload.initiated", "resource-upload", ShouldAudit: false),
            ["ResourceUploads.Parts"] = new("assets", "resource-upload.parts-issued", "resource-upload", ShouldAudit: false),
            ["ResourceUploads.Complete"] = new("assets", "resource.uploaded", "resource"),
            ["ResourceUploads.Abort"] = new("assets", "resource-upload.cancelled", "resource-upload", ShouldAudit: false),

            ["Settings.UpdateLanguages"] = new("settings", "language-settings.updated", "language-settings"),
            ["Settings.UpdateAdminAppearance"] = new("settings", "admin-appearance.updated", "admin-appearance"),
            ["Settings.UpdateResourceLibrarySettings"] = new("settings", "resource-library-settings.updated", "resource-library-settings"),
            ["Settings.CreateTerm"] = new("settings", "glossary-term.created", "glossary-term"),
            ["Settings.UpdateTerm"] = new("settings", "glossary-term.updated", "glossary-term"),
            ["Settings.DeleteTerm"] = new("settings", "glossary-term.deleted", "glossary-term", true),

            ["Footer.Update"] = new("settings", "footer.updated", "footer"),
            ["Footer.CreateGroup"] = new("settings", "footer-group.created", "footer-group"),
            ["Footer.UpdateGroup"] = new("settings", "footer-group.updated", "footer-group"),
            ["Footer.DeleteGroup"] = new("settings", "footer-group.deleted", "footer-group", true),
            ["Footer.SetGroupVisibility"] = new("settings", "footer-group.visibility-updated", "footer-group"),
            ["Footer.CreateLink"] = new("settings", "footer-link.created", "footer-link"),
            ["Footer.UpdateLink"] = new("settings", "footer-link.updated", "footer-link"),
            ["Footer.DeleteLink"] = new("settings", "footer-link.deleted", "footer-link", true),
            ["Footer.SetLinkVisibility"] = new("settings", "footer-link.visibility-updated", "footer-link"),
            ["Footer.ReorderGroups"] = new("settings", "footer-group.reordered", "footer-group"),
            ["Footer.ReorderLinks"] = new("settings", "footer-link.reordered", "footer-link"),
            ["Social.SetButtonVisibility"] = new("settings", "social-button.visibility-updated", "social-button"),
            ["Social.SetGroupVisibility"] = new("settings", "social-button-group.visibility-updated", "social-button-group"),
            ["LogManagement.Export"] = new("system", "log.exported", "log-export", true),
            ["LogManagement.RunRetention"] = new("system", "log.retention-run", "log-retention", true)
        };

    public static AuditActionDefinition Resolve(ControllerActionDescriptor descriptor)
    {
        var controller = descriptor.ControllerName;
        var action = descriptor.ActionName;
        if (ExplicitActions.TryGetValue($"{controller}.{action}", out var explicitAction))
            return explicitAction;
        var mapping = Controllers.TryGetValue(controller, out var value)
            ? value
            : (Domain: "system", Target: ToCode(controller));
        var verb = ResolveVerb(action);
        var code = $"{mapping.Target}.{verb}";
        var critical = verb is "deleted" or "password-reset" or "password-changed" or
            "published" or "reset" or "retention-run";
        return new AuditActionDefinition(mapping.Domain, code, mapping.Target, critical);
    }

    private static string ResolveVerb(string action)
    {
        foreach (var pair in ExplicitVerbs)
        {
            if (action.Contains(pair.Key, StringComparison.OrdinalIgnoreCase))
                return pair.Value;
        }

        if (action.StartsWith("Create", StringComparison.OrdinalIgnoreCase) ||
            action.StartsWith("Add", StringComparison.OrdinalIgnoreCase)) return "created";
        if (action.Contains("Delete", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Remove", StringComparison.OrdinalIgnoreCase)) return "deleted";
        if (action.Contains("Enable", StringComparison.OrdinalIgnoreCase)) return "enabled";
        if (action.Contains("Disable", StringComparison.OrdinalIgnoreCase)) return "disabled";
        if (action.Contains("Submit", StringComparison.OrdinalIgnoreCase)) return "submitted";
        if (action.Contains("Update", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Edit", StringComparison.OrdinalIgnoreCase) ||
            action.StartsWith("Set", StringComparison.OrdinalIgnoreCase)) return "updated";
        return "changed";
    }

    private static string ToCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unknown";
        var chars = new List<char>(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            if (char.IsUpper(current) && i > 0 && chars.Count > 0 && chars[^1] != '-') chars.Add('-');
            chars.Add(char.ToLowerInvariant(current is '_' or ' ' ? '-' : current));
        }
        return new string(chars.ToArray()).Trim('-');
    }
}
