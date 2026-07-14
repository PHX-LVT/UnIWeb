using FullProject.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FullProject.Services;

public sealed class AdminRoleService
{
    public const string ProtectedAdminRoleName = "AdminAdmin";

    private readonly IMongoCollection<AdminRoleDefinition> _roles;
    private readonly IMongoCollection<AdminUser> _users;
    private readonly IMongoCollection<BsonDocument> _userDocuments;
    private readonly IMongoCollection<AdminSessionRecord> _sessions;
    private readonly IMongoCollection<AdminAuditLog> _auditLogs;

    public AdminRoleService(IMongoDatabase database)
    {
        _roles = database.GetCollection<AdminRoleDefinition>("admin_roles");
        _users = database.GetCollection<AdminUser>("admin_users");
        _userDocuments = database.GetCollection<BsonDocument>("admin_users");
        _sessions = database.GetCollection<AdminSessionRecord>("admin_sessions");
        _auditLogs = database.GetCollection<AdminAuditLog>("admin_audit_logs");
    }

    public async Task EnsureInitializedAsync()
    {
        await NormalizeLegacyRoleStorageAsync();

        var roleCount = await _roles.CountDocumentsAsync(_ => true);
        if (roleCount == 0)
        {
            var now = DateTime.UtcNow;
            var roles = new[]
            {
                new AdminRoleDefinition
                {
                    Name = ProtectedAdminRoleName,
                    NormalizedName = NormalizeName(ProtectedAdminRoleName),
                    Description = "Protected full-access administrator role.",
                    Permissions = AdminPermissionKeys.All.ToList(),
                    IsProtected = true,
                    IsSystem = true,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new AdminRoleDefinition
                {
                    Name = "Manager",
                    NormalizedName = NormalizeName("Manager"),
                    Description = "Content publishing and form-management role.",
                    Permissions =
                    [
                        AdminPermissionKeys.ViewContent,
                        AdminPermissionKeys.ApproveContent,
                        AdminPermissionKeys.ViewFormDefinitions,
                        AdminPermissionKeys.EditFormDefinitions,
                        AdminPermissionKeys.ViewFormSubmissions,
                        AdminPermissionKeys.ManageFormSubmissions,
                        AdminPermissionKeys.ExportFormSubmissions
                    ],
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new AdminRoleDefinition
                {
                    Name = "Writer",
                    NormalizedName = NormalizeName("Writer"),
                    Description = "Content drafting and form-management role.",
                    Permissions =
                    [
                        AdminPermissionKeys.ViewContent,
                        AdminPermissionKeys.CreateEditContent,
                        AdminPermissionKeys.ViewFormDefinitions,
                        AdminPermissionKeys.EditFormDefinitions,
                        AdminPermissionKeys.ViewFormSubmissions,
                        AdminPermissionKeys.ManageFormSubmissions,
                        AdminPermissionKeys.ExportFormSubmissions
                    ],
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new AdminRoleDefinition
                {
                    Name = "Viewer",
                    NormalizedName = NormalizeName("Viewer"),
                    Description = "Read-only administrative role.",
                    Permissions = [AdminPermissionKeys.ViewContent],
                    CreatedAt = now,
                    UpdatedAt = now
                }
            };

            await _roles.InsertManyAsync(roles);
        }

        await MigrateLegacyContentPermissionsAsync();

        var adminRole = await GetProtectedAdminRoleAsync();
        if (adminRole is null)
        {
            adminRole = new AdminRoleDefinition
            {
                Name = ProtectedAdminRoleName,
                NormalizedName = NormalizeName(ProtectedAdminRoleName),
                Description = "Protected full-access administrator role.",
                Permissions = AdminPermissionKeys.All.ToList(),
                IsProtected = true,
                IsSystem = true
            };
            await _roles.InsertOneAsync(adminRole);
        }
        else if (!adminRole.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase)
                     .SetEquals(AdminPermissionKeys.All) || !adminRole.IsProtected || !adminRole.IsSystem)
        {
            await _roles.UpdateOneAsync(r => r.Id == adminRole.Id,
                Builders<AdminRoleDefinition>.Update
                    .Set(r => r.Permissions, AdminPermissionKeys.All.ToList())
                    .Set(r => r.IsProtected, true)
                    .Set(r => r.IsSystem, true)
                    .Set(r => r.UpdatedAt, DateTime.UtcNow));
        }

        await LinkLegacyUsersToRolesAsync();
    }

    private async Task MigrateLegacyContentPermissionsAsync()
    {
        var legacyKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AdminPermissionKeys.ManageContent,
            AdminPermissionKeys.PublishContent,
            AdminPermissionKeys.DeleteContent
        };
        var affectedUserIds = new HashSet<string>(StringComparer.Ordinal);

        var roles = await _roles.Find(_ => true).ToListAsync();
        foreach (var role in roles)
        {
            if (!role.Permissions.Any(legacyKeys.Contains)) continue;

            var migrated = MigrateLegacyContentPermissionSet(role.Permissions, role.Name);
            await _roles.UpdateOneAsync(r => r.Id == role.Id,
                Builders<AdminRoleDefinition>.Update
                    .Set(r => r.Permissions, migrated)
                    .Set(r => r.UpdatedAt, DateTime.UtcNow));

            var roleUsers = await _users.Find(user => user.RoleId == role.Id)
                .Project(user => user.Id)
                .ToListAsync();
            foreach (var userId in roleUsers) affectedUserIds.Add(userId);
        }

        var users = await _users.Find(_ => true).ToListAsync();
        foreach (var user in users)
        {
            var sourceExtras = user.ExtraPermissions.Count > 0
                ? user.ExtraPermissions
                : user.Permissions;
            if (!sourceExtras.Any(legacyKeys.Contains)) continue;

            var migratedExtras = MigrateLegacyContentPermissionSet(sourceExtras, null);
            await _users.UpdateOneAsync(current => current.Id == user.Id,
                Builders<AdminUser>.Update.Combine(
                    Builders<AdminUser>.Update.Set(current => current.ExtraPermissions, migratedExtras),
                    Builders<AdminUser>.Update.Set(current => current.Permissions, migratedExtras),
                    Builders<AdminUser>.Update.Set(current => current.UpdatedAt, DateTime.UtcNow)));
            affectedUserIds.Add(user.Id);
        }

        if (affectedUserIds.Count == 0) return;

        var ids = affectedUserIds.ToList();
        await _users.UpdateManyAsync(user => ids.Contains(user.Id),
            Builders<AdminUser>.Update.Inc(user => user.TokenVersion, 1));
        await RevokeSessionsForUsersAsync(ids, "system-content-permission-migration", AdminSessionRevokeReason.RoleChanged);
    }

    private static List<string> MigrateLegacyContentPermissionSet(
        IEnumerable<string>? permissions,
        string? roleName)
    {
        var source = (permissions ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var migrated = source
            .Where(permission =>
                !string.Equals(permission, AdminPermissionKeys.ManageContent, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(permission, AdminPermissionKeys.PublishContent, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(permission, AdminPermissionKeys.DeleteContent, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (string.Equals(roleName, "Manager", StringComparison.OrdinalIgnoreCase))
        {
            migrated.Add(AdminPermissionKeys.ApproveContent);
        }
        else if (string.Equals(roleName, "Writer", StringComparison.OrdinalIgnoreCase))
        {
            migrated.Add(AdminPermissionKeys.CreateEditContent);
        }
        else
        {
            if (source.Contains(AdminPermissionKeys.ManageContent))
                migrated.Add(AdminPermissionKeys.CreateEditContent);
            if (source.Contains(AdminPermissionKeys.PublishContent))
                migrated.Add(AdminPermissionKeys.ApproveContent);
        }

        if (source.Overlaps(
            [
                AdminPermissionKeys.ManageContent,
                AdminPermissionKeys.PublishContent,
                AdminPermissionKeys.DeleteContent
            ]))
            migrated.Add(AdminPermissionKeys.ViewContent);

        return NormalizePermissions(migrated);
    }

    private async Task NormalizeLegacyRoleStorageAsync()
    {
        // The enum-based model stored the field as "Role". The dynamic-role
        // model stores the compatibility name as "role". Copy the old value
        // explicitly before typed AdminUser documents are read; otherwise a
        // missing field must never be interpreted as AdminAdmin.
        var legacyFilter = new BsonDocument("$and", new BsonArray
        {
            new BsonDocument("role", new BsonDocument("$exists", false)),
            new BsonDocument("Role", new BsonDocument("$exists", true))
        });
        var legacyUsers = await _userDocuments.Find(legacyFilter)
            .Project(new BsonDocument { { "_id", 1 }, { "Role", 1 } })
            .ToListAsync();

        var writes = new List<WriteModel<BsonDocument>>();
        foreach (var document in legacyUsers)
        {
            var roleName = ReadLegacyRoleName(document.GetValue("Role", BsonNull.Value));
            if (string.IsNullOrWhiteSpace(roleName)) continue;

            writes.Add(new UpdateOneModel<BsonDocument>(
                new BsonDocument("_id", document["_id"]),
                new BsonDocument
                {
                    { "$set", new BsonDocument("role", roleName) },
                    { "$unset", new BsonDocument("Role", 1) }
                }));
        }

        if (writes.Count > 0)
            await _userDocuments.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = false });
    }

    private async Task LinkLegacyUsersToRolesAsync()
    {
        // RoleId is represented as a MongoDB ObjectId. BSON filters avoid
        // serializing legacy blank strings through the ObjectId serializer.
        // Link every known legacy role, not only AdminAdmin, so ordinary users
        // participate correctly in role counts, deletion and reassignment.
        var roles = await _roles.Find(_ => true).ToListAsync();
        foreach (var role in roles)
        {
            var unassignedRoleFilter = new BsonDocument("$and", new BsonArray
            {
                new BsonDocument("role", role.Name),
                new BsonDocument("$or", new BsonArray
                {
                    new BsonDocument("RoleId", new BsonDocument("$exists", false)),
                    new BsonDocument("RoleId", BsonNull.Value),
                    new BsonDocument("RoleId", new BsonDocument("$type", "string"))
                })
            });
            var migratedUserDocuments = await _userDocuments.Find(unassignedRoleFilter)
                .Project(new BsonDocument("_id", 1))
                .ToListAsync();
            var migratedUserIds = migratedUserDocuments
                .Where(document => document["_id"].IsObjectId)
                .Select(document => document["_id"].AsObjectId.ToString())
                .ToList();

            var update = new BsonDocument("$set", new BsonDocument
            {
                { "RoleId", ObjectId.Parse(role.Id) },
                { "UpdatedAt", DateTime.UtcNow }
            });
            if (!role.IsProtected)
                update.Add("$inc", new BsonDocument("TokenVersion", 1));

            await _userDocuments.UpdateManyAsync(
                unassignedRoleFilter,
                update);

            // A token issued while the legacy field was being misread may carry
            // incorrect privileges. Revoke repaired ordinary-role sessions at
            // once; the legitimate protected administrator need not be displaced.
            if (!role.IsProtected && migratedUserIds.Count > 0)
            {
                await RevokeSessionsForUsersAsync(
                    migratedUserIds,
                    "system-role-migration",
                    AdminSessionRevokeReason.RoleChanged);
            }
        }

        var knownRoleIds = roles
            .Select(role => ObjectId.Parse(role.Id))
            .ToHashSet();
        var userRoleDocuments = await _userDocuments.Find(FilterDefinition<BsonDocument>.Empty)
            .Project(new BsonDocument { { "_id", 1 }, { "RoleId", 1 } })
            .ToListAsync();
        var unresolvedCount = userRoleDocuments.Count(document =>
            !document.TryGetValue("RoleId", out var roleId) ||
            !roleId.IsObjectId ||
            !knownRoleIds.Contains(roleId.AsObjectId));
        if (unresolvedCount > 0)
        {
            throw new InvalidOperationException(
                $"{unresolvedCount} admin account(s) could not be linked to a valid role.");
        }
    }

    private static string? ReadLegacyRoleName(BsonValue value)
    {
        if (value.IsString) return value.AsString.Trim();
        if (!value.IsInt32) return null;

        return value.AsInt32 switch
        {
            0 => ProtectedAdminRoleName,
            1 => "Manager",
            2 => "Writer",
            3 => "Viewer",
            _ => null
        };
    }

    public async Task<List<AdminRoleDefinition>> GetRolesAsync() =>
        await _roles.Find(_ => true).SortBy(r => r.Name).ToListAsync();

    public async Task<AdminRoleDefinition?> GetByIdAsync(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out _)) return null;
        return await _roles.Find(r => r.Id == id).FirstOrDefaultAsync();
    }

    public async Task<AdminRoleDefinition?> GetByNameAsync(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var normalized = NormalizeName(name);
        return await _roles.Find(r => r.NormalizedName == normalized).FirstOrDefaultAsync();
    }

    public Task<AdminRoleDefinition?> GetRoleForUserAsync(AdminUser user) =>
        GetByIdAsync(user.RoleId);

    public async Task<AdminRoleDefinition?> GetProtectedAdminRoleAsync() =>
        await _roles.Find(r => r.IsProtected && r.NormalizedName == NormalizeName(ProtectedAdminRoleName))
            .FirstOrDefaultAsync();

    public async Task<bool> IsAdminAdminAsync(AdminUser user)
    {
        var role = await GetRoleForUserAsync(user);
        return role?.IsProtected == true &&
               string.Equals(role.Name, ProtectedAdminRoleName, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<List<string>> GetEffectivePermissionsAsync(AdminUser user)
    {
        var role = await GetRoleForUserAsync(user);
        if (role?.IsProtected == true)
            return AdminPermissionKeys.All.ToList();

        var rolePermissions = NormalizePermissions(role?.Permissions);
        var extras = NormalizePermissions(user.ExtraPermissions.Count > 0
            ? user.ExtraPermissions
            : user.Permissions);

        return rolePermissions.Concat(extras)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public List<string> NormalizeExtraPermissions(IEnumerable<string>? permissions, AdminRoleDefinition role) =>
        NormalizePermissions(permissions)
            .Except(NormalizePermissions(role.Permissions), StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static List<string> NormalizePermissions(IEnumerable<string>? permissions)
        => AdminPermissionKeys.ExpandDependencies(permissions);

    public async Task<long> CountUsersAsync(string roleId) =>
        await _users.CountDocumentsAsync(u => u.RoleId == roleId);

    public async Task<(AdminRoleDefinition? Role, List<string> Errors)> CreateAsync(
        AdminRoleCreateRequest request,
        AdminUser actor,
        string ipAddress,
        string userAgent)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name)) return (null, ["Role name is required."]);
        if (name.Length > 80) return (null, ["Role name cannot exceed 80 characters."]);

        var normalized = NormalizeName(name);
        if (await _roles.Find(r => r.NormalizedName == normalized).AnyAsync())
            return (null, ["Role name already exists."]);

        var role = new AdminRoleDefinition
        {
            Name = name,
            NormalizedName = normalized,
            Description = (request.Description ?? string.Empty).Trim(),
            Permissions = NormalizePermissions(request.Permissions),
            CreatedById = actor.Id,
            UpdatedById = actor.Id
        };
        await _roles.InsertOneAsync(role);
        await LogAsync("role-created", actor, role.Id, role.Name, $"Created role {role.Name}.", ipAddress, userAgent);
        return (role, []);
    }

    public async Task<(AdminRoleDefinition? Role, List<string> Errors, long AffectedUsers)> UpdateAsync(
        string id,
        AdminRoleUpdateRequest request,
        AdminUser actor,
        string ipAddress,
        string userAgent)
    {
        var role = await GetByIdAsync(id);
        if (role is null) return (null, ["Role not found."], 0);
        if (role.IsProtected) return (null, ["The AdminAdmin role cannot be modified."], 0);
        if (role.IsDeleting) return (null, ["This role is currently being deleted."], 0);

        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name)) return (null, ["Role name is required."], 0);
        if (name.Length > 80) return (null, ["Role name cannot exceed 80 characters."], 0);

        var normalized = NormalizeName(name);
        if (await _roles.Find(r => r.Id != id && r.NormalizedName == normalized).AnyAsync())
            return (null, ["Role name already exists."], 0);

        var permissions = NormalizePermissions(request.Permissions);
        var affectedUsers = await CountUsersAsync(id);
        if (affectedUsers > 0)
            await RevokeRoleUsersAsync(id, actor.Id, AdminSessionRevokeReason.RoleChanged);

        await _roles.UpdateOneAsync(r => r.Id == id,
            Builders<AdminRoleDefinition>.Update
                .Set(r => r.Name, name)
                .Set(r => r.NormalizedName, normalized)
                .Set(r => r.Description, (request.Description ?? string.Empty).Trim())
                .Set(r => r.Permissions, permissions)
                .Set(r => r.UpdatedById, actor.Id)
                .Set(r => r.UpdatedAt, DateTime.UtcNow));

        if (affectedUsers > 0)
        {
            await _users.UpdateManyAsync(u => u.RoleId == id,
                Builders<AdminUser>.Update.Combine(
                    Builders<AdminUser>.Update.Set(u => u.LegacyRole, name),
                    Builders<AdminUser>.Update.PullAll(u => u.ExtraPermissions, permissions)));
        }

        await LogAsync("role-updated", actor, role.Id, role.Name,
            $"Updated role {role.Name}; affected {affectedUsers} user(s).", ipAddress, userAgent);
        return (await GetByIdAsync(id), [], affectedUsers);
    }

    public async Task<(bool Deleted, List<string> Errors, long ReassignedUsers)> DeleteAsync(
        string id,
        string? replacementRoleId,
        AdminUser actor,
        string ipAddress,
        string userAgent)
    {
        var role = await GetByIdAsync(id);
        if (role is null) return (false, ["Role not found."], 0);
        if (role.IsProtected) return (false, ["The AdminAdmin role cannot be deleted."], 0);

        var affectedUsers = await CountUsersAsync(id);
        AdminRoleDefinition? replacement = null;
        if (affectedUsers > 0)
        {
            if (string.IsNullOrWhiteSpace(replacementRoleId))
                return (false, ["A replacement role is required for assigned users."], affectedUsers);
            if (string.Equals(id, replacementRoleId, StringComparison.Ordinal))
                return (false, ["Replacement role must be different from the deleted role."], affectedUsers);

            replacement = await GetByIdAsync(replacementRoleId);
            if (replacement is null) return (false, ["Replacement role not found."], affectedUsers);
            if (replacement.IsDeleting) return (false, ["Replacement role is currently being deleted."], affectedUsers);

            await _roles.UpdateOneAsync(r => r.Id == id,
                Builders<AdminRoleDefinition>.Update
                    .Set(r => r.IsDeleting, true)
                    .Set(r => r.UpdatedById, actor.Id)
                    .Set(r => r.UpdatedAt, DateTime.UtcNow));

            var reassignedUserIds = await _users.Find(u => u.RoleId == id).Project(u => u.Id).ToListAsync();

            await _users.UpdateManyAsync(u => u.RoleId == id,
                Builders<AdminUser>.Update.Combine(
                    Builders<AdminUser>.Update.Set(u => u.RoleId, replacement.Id),
                    Builders<AdminUser>.Update.Set(u => u.LegacyRole, replacement.Name),
                    Builders<AdminUser>.Update.PullAll(u => u.ExtraPermissions, replacement.Permissions),
                    Builders<AdminUser>.Update.Inc(u => u.TokenVersion, 1),
                    Builders<AdminUser>.Update.Set(u => u.UpdatedById, actor.Id),
                    Builders<AdminUser>.Update.Set(u => u.UpdatedAt, DateTime.UtcNow)));
            await RevokeSessionsForUsersAsync(
                reassignedUserIds,
                actor.Id,
                AdminSessionRevokeReason.RoleChanged);
        }
        else
        {
            await _roles.UpdateOneAsync(r => r.Id == id,
                Builders<AdminRoleDefinition>.Update
                    .Set(r => r.IsDeleting, true)
                    .Set(r => r.UpdatedById, actor.Id)
                    .Set(r => r.UpdatedAt, DateTime.UtcNow));
        }

        if (await CountUsersAsync(id) > 0)
        {
            await _roles.UpdateOneAsync(r => r.Id == id,
                Builders<AdminRoleDefinition>.Update.Set(r => r.IsDeleting, false));
            return (false, ["Role reassignment did not complete; the role was not deleted."], affectedUsers);
        }

        await _roles.DeleteOneAsync(r => r.Id == id);
        await LogAsync("role-deleted", actor, role.Id, role.Name,
            replacement is null
                ? $"Deleted unused role {role.Name}."
                : $"Deleted role {role.Name} and reassigned {affectedUsers} user(s) to {replacement.Name}.",
            ipAddress,
            userAgent);
        return (true, [], affectedUsers);
    }

    private async Task RevokeRoleUsersAsync(string roleId, string actorId, AdminSessionRevokeReason reason)
    {
        var userIds = await _users.Find(u => u.RoleId == roleId).Project(u => u.Id).ToListAsync();
        if (userIds.Count == 0) return;
        await _users.UpdateManyAsync(u => userIds.Contains(u.Id), Builders<AdminUser>.Update.Inc(u => u.TokenVersion, 1));
        await RevokeSessionsForUsersAsync(userIds, actorId, reason);
    }

    private async Task RevokeSessionsForUsersAsync(List<string> userIds, string actorId, AdminSessionRevokeReason reason)
    {
        if (userIds.Count == 0) return;
        await _sessions.UpdateManyAsync(
            s => userIds.Contains(s.AdminId) && !s.IsRevoked,
            Builders<AdminSessionRecord>.Update
                .Set(s => s.IsRevoked, true)
                .Set(s => s.RevokedAt, DateTime.UtcNow)
                .Set(s => s.RevokedById, actorId)
                .Set(s => s.RevokeReason, reason));
    }

    private async Task LogAsync(
        string action,
        AdminUser actor,
        string targetId,
        string targetName,
        string message,
        string ipAddress,
        string userAgent) =>
        await _auditLogs.InsertOneAsync(new AdminAuditLog
        {
            Area = AdminAuditArea.UserManagement,
            Action = action,
            ActorId = actor.Id,
            ActorEmail = actor.Email,
            TargetId = targetId,
            TargetEmail = targetName,
            Message = message,
            IpAddress = ipAddress,
            UserAgent = userAgent
        });

    private static string NormalizeName(string value) => value.Trim().ToUpperInvariant();
}
