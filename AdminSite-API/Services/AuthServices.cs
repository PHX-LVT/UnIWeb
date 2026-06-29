using FullProject.Models;
using FullProject.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace FullProject.Services
{
    public class AuthService
    {
        private const int MaxFailedLoginAttempts = 5;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

        private readonly IMongoCollection<AdminUser> _users;
        private readonly IMongoCollection<AdminSessionRecord> _sessions;
        private readonly IMongoCollection<AdminLoginActivityRecord> _loginActivity;
        private readonly IMongoCollection<AdminAuditLog> _auditLogs;
        private readonly JwtSettings _jwt;

        public AuthService(IMongoDatabase db, IOptions<JwtSettings> jwt)
        {
            _users = db.GetCollection<AdminUser>("admin_users");
            _sessions = db.GetCollection<AdminSessionRecord>("admin_sessions");
            _loginActivity = db.GetCollection<AdminLoginActivityRecord>("admin_login_activity");
            _auditLogs = db.GetCollection<AdminAuditLog>("admin_audit_logs");
            _jwt = jwt.Value;
        }

        public async Task<LoginResponseDto?> LoginAsync(string email, string password, string ipAddress, string userAgent)
        {
            var normalizedEmail = NormalizeEmail(email);
            var user = await _users.Find(u => u.Email == normalizedEmail).FirstOrDefaultAsync();
            if (user is null)
            {
                await RecordLoginActivityAsync(null, normalizedEmail, "login-denied", false, "Unknown admin email.", ipAddress, userAgent);
                await LogAsync(AdminAuditArea.Auth, "login-denied", "anonymous", string.Empty, null, normalizedEmail, "Unknown admin email.", ipAddress, userAgent);
                return null;
            }

            NormalizeUserDefaults(user);
            if (!CanLogin(user))
            {
                await RecordLoginActivityAsync(user, user.Email, "login-denied", false, "Account is disabled or locked.", ipAddress, userAgent);
                await LogAsync(AdminAuditArea.Auth, "login-denied", user.Id, user.Email, user.Id, user.Email, "Account is disabled or locked.", ipAddress, userAgent);
                return null;
            }

            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                await RegisterFailedLoginAsync(user, ipAddress, userAgent);
                return null;
            }

            var tokenId = Guid.NewGuid().ToString("N");
            var expiresAt = DateTime.UtcNow.AddHours(_jwt.ExpiryHour);
            var effectivePermissions = GetEffectivePermissions(user).ToList();
            var token = GenerateJwt(user, tokenId, effectivePermissions, expiresAt);
            var session = new AdminSessionRecord
            {
                AdminId = user.Id,
                Email = user.Email,
                TokenId = tokenId,
                TokenVersion = user.TokenVersion,
                LoginAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                BrowserName = ParseBrowser(userAgent),
                OperatingSystem = ParseOperatingSystem(userAgent)
            };

            await _sessions.InsertOneAsync(session);
            await _users.UpdateOneAsync(u => u.Id == user.Id,
                Builders<AdminUser>.Update
                    .Set(u => u.Status, AdminUserStatus.Active)
                    .Set(u => u.FailedLoginAttempts, 0)
                    .Set(u => u.LockedUntil, null)
                    .Set(u => u.LastLoginAt, DateTime.UtcNow)
                    .Set(u => u.LastLoginIp, ipAddress)
                    .Set(u => u.UpdatedAt, DateTime.UtcNow));
            user.Status = AdminUserStatus.Active;
            user.FailedLoginAttempts = 0;
            user.LockedUntil = null;
            await RecordLoginActivityAsync(user, user.Email, "login-success", true, "Admin logged in.", ipAddress, userAgent);
            await LogAsync(AdminAuditArea.Auth, "login-success", user.Id, user.Email, user.Id, user.Email, "Admin logged in.", ipAddress, userAgent);

            return new LoginResponseDto
            {
                Token = token,
                AdminId = user.Id,
                Email = user.Email,
                FullName = DisplayName(user),
                Role = user.Role,
                Status = user.Status,
                Permissions = effectivePermissions
            };
        }

        public async Task<bool> ValidateSessionAsync(string adminId, string tokenId, int tokenVersion)
        {
            if (string.IsNullOrWhiteSpace(adminId) || string.IsNullOrWhiteSpace(tokenId))
                return false;

            var user = await GetByIdAsync(adminId);
            if (user is null) return false;
            NormalizeUserDefaults(user);

            if (user.Status != AdminUserStatus.Active ||
                user.TokenVersion != tokenVersion ||
                (user.LockedUntil is not null && user.LockedUntil > DateTime.UtcNow))
                return false;

            var session = await _sessions.Find(s =>
                    s.AdminId == adminId &&
                    s.TokenId == tokenId &&
                    !s.IsRevoked &&
                    s.ExpiresAt > DateTime.UtcNow)
                .FirstOrDefaultAsync();

            if (session is null) return false;

            if ((DateTime.UtcNow - session.LastActivityAt) > TimeSpan.FromMinutes(2))
            {
                await _sessions.UpdateOneAsync(s => s.Id == session.Id,
                    Builders<AdminSessionRecord>.Update.Set(s => s.LastActivityAt, DateTime.UtcNow));
            }

            return true;
        }

        public async Task LogoutAsync(string adminId, string tokenId, string ipAddress, string userAgent)
        {
            await RevokeSessionByTokenIdAsync(tokenId, adminId, AdminSessionRevokeReason.Logout, ipAddress);
            var user = await GetByIdAsync(adminId);
            await RecordLoginActivityAsync(user, user?.Email ?? string.Empty, "logout", true, "Admin logged out.", ipAddress, userAgent);
            await LogAsync(AdminAuditArea.Auth, "logout", adminId, user?.Email ?? string.Empty, adminId, user?.Email, "Admin logged out.", ipAddress, userAgent);
        }

        public async Task<AdminUser?> GetByIdAsync(string adminId) =>
            await _users.Find(u => u.Id == adminId).FirstOrDefaultAsync();

        public async Task<List<AdminUser>> GetUsersAsync() =>
            await _users.Find(_ => true).SortBy(u => u.Email).ToListAsync();

        public async Task<(AdminUser? User, List<string> Errors)> CreateUserAsync(AdminUserCreateRequest dto, AdminUser actor, string ipAddress, string userAgent)
        {
            var errors = ValidateUserCreate(dto);
            if (errors.Count > 0) return (null, errors);

            var email = NormalizeEmail(dto.Email);
            var exists = await _users.Find(u => u.Email == email).AnyAsync();
            if (exists) return (null, ["Email already exists."]);

            var user = new AdminUser
            {
                Email = email,
                FullName = string.IsNullOrWhiteSpace(dto.FullName) ? email : dto.FullName.Trim(),
                PasswordHash = HashPassword(dto.Password),
                Role = dto.Role,
                Status = dto.Active ? AdminUserStatus.Active : AdminUserStatus.Disabled,
                Permissions = NormalizePermissions(dto.Role, dto.Permissions),
                TokenVersion = 1,
                DisabledAt = dto.Active ? null : DateTime.UtcNow,
                DisabledById = dto.Active ? null : actor.Id,
                CreatedById = actor.Id,
                UpdatedById = actor.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _users.InsertOneAsync(user);
            await LogAsync(AdminAuditArea.UserManagement, "user-created", actor.Id, actor.Email, user.Id, user.Email, $"Created {user.Role} account.", ipAddress, userAgent);
            return (user, []);
        }

        public async Task<(AdminUser? User, List<string> Errors)> UpdateUserAsync(string id, AdminUserUpdateRequest dto, AdminUser actor, string ipAddress, string userAgent)
        {
            var user = await GetByIdAsync(id);
            if (user is null) return (null, ["User not found."]);

            NormalizeUserDefaults(user);
            if (user.Role == AdminRole.AdminAdmin && dto.Role is not null && dto.Role.Value != AdminRole.AdminAdmin)
            {
                var adminCount = await _users.CountDocumentsAsync(u => u.Role == AdminRole.AdminAdmin && u.Status == AdminUserStatus.Active);
                if (adminCount <= 1) return (null, ["At least one active AdminAdmin account is required."]);
            }

            var updates = new List<UpdateDefinition<AdminUser>>
            {
                Builders<AdminUser>.Update.Set(u => u.UpdatedAt, DateTime.UtcNow),
                Builders<AdminUser>.Update.Set(u => u.UpdatedById, actor.Id)
            };

            if (dto.FullName is not null)
                updates.Add(Builders<AdminUser>.Update.Set(u => u.FullName, string.IsNullOrWhiteSpace(dto.FullName) ? user.Email : dto.FullName.Trim()));

            var roleChanged = dto.Role is not null && dto.Role.Value != user.Role;
            if (dto.Role is not null)
                updates.Add(Builders<AdminUser>.Update.Set(u => u.Role, dto.Role.Value));

            if (dto.Permissions is not null)
                updates.Add(Builders<AdminUser>.Update.Set(u => u.Permissions, NormalizePermissions(dto.Role ?? user.Role, dto.Permissions)));

            var statusChanged = dto.Active is not null &&
                                ((dto.Active.Value && user.Status != AdminUserStatus.Active) ||
                                 (!dto.Active.Value && user.Status != AdminUserStatus.Disabled));
            if (dto.Active is not null)
            {
                if (!dto.Active.Value && (dto.Role ?? user.Role) == AdminRole.AdminAdmin)
                {
                    var adminCount = await _users.CountDocumentsAsync(u => u.Role == AdminRole.AdminAdmin && u.Status == AdminUserStatus.Active && u.Id != id);
                    if (adminCount == 0) return (null, ["At least one active AdminAdmin account is required."]);
                }

                updates.Add(Builders<AdminUser>.Update.Set(u => u.Status, dto.Active.Value ? AdminUserStatus.Active : AdminUserStatus.Disabled));
                updates.Add(Builders<AdminUser>.Update.Set(u => u.DisabledAt, dto.Active.Value ? null : DateTime.UtcNow));
                updates.Add(Builders<AdminUser>.Update.Set(u => u.DisabledById, dto.Active.Value ? null : actor.Id));
                if (dto.Active.Value)
                {
                    updates.Add(Builders<AdminUser>.Update.Set(u => u.LockedUntil, null));
                    updates.Add(Builders<AdminUser>.Update.Set(u => u.FailedLoginAttempts, 0));
                }
            }

            if (roleChanged || dto.Permissions is not null || statusChanged)
                updates.Add(Builders<AdminUser>.Update.Inc(u => u.TokenVersion, 1));

            await _users.UpdateOneAsync(u => u.Id == id, Builders<AdminUser>.Update.Combine(updates));

            if (roleChanged || dto.Permissions is not null || (statusChanged && dto.Active == false))
                await RevokeAllUserSessionsAsync(id, actor.Id, AdminSessionRevokeReason.RoleChanged, ipAddress);

            await LogAsync(AdminAuditArea.UserManagement, "user-updated", actor.Id, actor.Email, user.Id, user.Email, "Updated account settings.", ipAddress, userAgent);
            return (await GetByIdAsync(id), []);
        }

        public async Task<(AdminUser? User, List<string> Errors)> SetUserEnabledAsync(string id, bool enabled, AdminUser actor, string ipAddress, string userAgent)
        {
            var user = await GetByIdAsync(id);
            if (user is null) return (null, ["User not found."]);
            NormalizeUserDefaults(user);

            if (!enabled && user.Role == AdminRole.AdminAdmin)
            {
                var adminCount = await _users.CountDocumentsAsync(u => u.Role == AdminRole.AdminAdmin && u.Status == AdminUserStatus.Active && u.Id != id);
                if (adminCount == 0) return (null, ["At least one active AdminAdmin account is required."]);
            }

            var nextStatus = enabled ? AdminUserStatus.Active : AdminUserStatus.Disabled;
            var update = Builders<AdminUser>.Update
                .Set(u => u.Status, nextStatus)
                .Set(u => u.DisabledAt, enabled ? null : DateTime.UtcNow)
                .Set(u => u.DisabledById, enabled ? null : actor.Id)
                .Set(u => u.UpdatedById, actor.Id)
                .Set(u => u.UpdatedAt, DateTime.UtcNow)
                .Inc(u => u.TokenVersion, enabled ? 0 : 1);

            await _users.UpdateOneAsync(u => u.Id == id, update);

            if (!enabled)
                await RevokeAllUserSessionsAsync(id, actor.Id, AdminSessionRevokeReason.UserDisabled, ipAddress);

            await LogAsync(AdminAuditArea.UserManagement, enabled ? "user-enabled" : "user-disabled", actor.Id, actor.Email, user.Id, user.Email, enabled ? "Enabled account." : "Disabled account.", ipAddress, userAgent);
            return (await GetByIdAsync(id), []);
        }

        public async Task<(AdminUser? User, List<string> Errors)> ResetPasswordAsync(string id, string newPassword, AdminUser actor, string ipAddress, string userAgent)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
                return (null, ["New password must be at least 8 characters."]);

            var user = await GetByIdAsync(id);
            if (user is null) return (null, ["User not found."]);

            await _users.UpdateOneAsync(u => u.Id == id,
                Builders<AdminUser>.Update
                    .Set(u => u.PasswordHash, HashPassword(newPassword))
                    .Inc(u => u.TokenVersion, 1)
                    .Set(u => u.UpdatedById, actor.Id)
                    .Set(u => u.UpdatedAt, DateTime.UtcNow));

            await RevokeAllUserSessionsAsync(id, actor.Id, AdminSessionRevokeReason.PasswordChanged, ipAddress);
            await LogAsync(AdminAuditArea.UserManagement, "password-reset", actor.Id, actor.Email, user.Id, user.Email, "Reset account password.", ipAddress, userAgent);
            return (await GetByIdAsync(id), []);
        }

        public async Task<(AdminUser? User, List<string> Errors)> DeleteUserAsync(string id, AdminUser actor, string ipAddress, string userAgent)
        {
            var user = await GetByIdAsync(id);
            if (user is null) return (null, ["User not found."]);
            NormalizeUserDefaults(user);

            if (user.Id == actor.Id)
                return (null, ["You cannot delete your own account."]);

            if (user.Role == AdminRole.AdminAdmin)
            {
                var adminCount = await _users.CountDocumentsAsync(u => u.Role == AdminRole.AdminAdmin && u.Status == AdminUserStatus.Active && u.Id != id);
                if (adminCount == 0) return (null, ["At least one active AdminAdmin account is required."]);
            }

            await RevokeAllUserSessionsAsync(id, actor.Id, AdminSessionRevokeReason.AccountDeleted, ipAddress);
            await _users.DeleteOneAsync(u => u.Id == id);
            await LogAsync(AdminAuditArea.UserManagement, "user-deleted", actor.Id, actor.Email, user.Id, user.Email, "Deleted admin account and revoked existing sessions.", ipAddress, userAgent);
            return (user, []);
        }

        public async Task UpdateOwnPasswordAsync(AdminUser user, string passwordHash, string ipAddress, string userAgent)
        {
            await _users.UpdateOneAsync(a => a.Id == user.Id,
                Builders<AdminUser>.Update
                    .Set(a => a.PasswordHash, passwordHash)
                    .Inc(a => a.TokenVersion, 1)
                    .Set(a => a.UpdatedById, user.Id)
                    .Set(a => a.UpdatedAt, DateTime.UtcNow));

            await RevokeAllUserSessionsAsync(user.Id, user.Id, AdminSessionRevokeReason.PasswordChanged, ipAddress);
            await LogAsync(AdminAuditArea.Auth, "password-changed", user.Id, user.Email, user.Id, user.Email, "Changed own password.", ipAddress, userAgent);
        }

        public async Task<List<AdminSessionRecord>> GetSessionsAsync(string? adminId = null, bool includeRevoked = true)
        {
            var filter = Builders<AdminSessionRecord>.Filter.Empty;
            if (!string.IsNullOrWhiteSpace(adminId))
                filter &= Builders<AdminSessionRecord>.Filter.Eq(s => s.AdminId, adminId);
            if (!includeRevoked)
                filter &= Builders<AdminSessionRecord>.Filter.Eq(s => s.IsRevoked, false);

            return await _sessions.Find(filter)
                .SortByDescending(s => s.LoginAt)
                .Limit(300)
                .ToListAsync();
        }

        public async Task<(List<AdminSessionRecord> Items, long TotalCount, int Page, int PageSize)> GetSessionsPageAsync(
            int page,
            int pageSize,
            string? adminId = null,
            bool includeRevoked = true)
        {
            var filter = Builders<AdminSessionRecord>.Filter.Empty;
            if (!string.IsNullOrWhiteSpace(adminId))
                filter &= Builders<AdminSessionRecord>.Filter.Eq(s => s.AdminId, adminId);
            if (!includeRevoked)
                filter &= Builders<AdminSessionRecord>.Filter.Eq(s => s.IsRevoked, false);

            pageSize = Math.Clamp(pageSize, 10, 100);
            var totalCount = await _sessions.CountDocumentsAsync(filter);
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);

            var items = await _sessions.Find(filter)
                .SortByDescending(s => s.LoginAt)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            return (items, totalCount, page, pageSize);
        }

        public async Task<List<AdminAuditLog>> GetAuditLogsAsync(string? targetId = null)
        {
            var filter = Builders<AdminAuditLog>.Filter.Empty;
            if (!string.IsNullOrWhiteSpace(targetId))
                filter &= Builders<AdminAuditLog>.Filter.Eq(l => l.TargetId, targetId);

            return await _auditLogs.Find(filter)
                .SortByDescending(l => l.CreatedAt)
                .Limit(500)
                .ToListAsync();
        }

        public async Task<(List<AdminAuditLog> Items, long TotalCount, int Page, int PageSize)> GetAuditLogsPageAsync(
            int page,
            int pageSize,
            string? targetId = null)
        {
            var filter = Builders<AdminAuditLog>.Filter.Empty;
            if (!string.IsNullOrWhiteSpace(targetId))
                filter &= Builders<AdminAuditLog>.Filter.Eq(l => l.TargetId, targetId);

            pageSize = Math.Clamp(pageSize, 10, 100);
            var totalCount = await _auditLogs.CountDocumentsAsync(filter);
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);

            var items = await _auditLogs.Find(filter)
                .SortByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            return (items, totalCount, page, pageSize);
        }

        public async Task<(List<AdminLoginActivityRecord> Items, long TotalCount, int Page, int PageSize)> GetLoginActivityPageAsync(
            int page,
            int pageSize,
            string? adminId = null)
        {
            var filter = Builders<AdminLoginActivityRecord>.Filter.Empty;
            if (!string.IsNullOrWhiteSpace(adminId))
                filter &= Builders<AdminLoginActivityRecord>.Filter.Eq(l => l.AdminId, adminId);

            pageSize = Math.Clamp(pageSize, 10, 100);
            var totalCount = await _loginActivity.CountDocumentsAsync(filter);
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);

            var items = await _loginActivity.Find(filter)
                .SortByDescending(l => l.OccurredAt)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            return (items, totalCount, page, pageSize);
        }

        public async Task<List<AdminLoginActivityRecord>> GetLoginActivityAsync(string? adminId = null)
        {
            var filter = Builders<AdminLoginActivityRecord>.Filter.Empty;
            if (!string.IsNullOrWhiteSpace(adminId))
                filter &= Builders<AdminLoginActivityRecord>.Filter.Eq(l => l.AdminId, adminId);

            return await _loginActivity.Find(filter)
                .SortByDescending(l => l.OccurredAt)
                .Limit(300)
                .ToListAsync();
        }

        public async Task<long> DeleteSessionsAsync(IEnumerable<string> ids, AdminUser actor, string ipAddress, string userAgent)
        {
            var selectedIds = NormalizeIds(ids);
            if (selectedIds.Count == 0) return 0;

            var now = DateTime.UtcNow;
            var filter = Builders<AdminSessionRecord>.Filter.And(
                Builders<AdminSessionRecord>.Filter.In(s => s.Id, selectedIds),
                Builders<AdminSessionRecord>.Filter.Or(
                    Builders<AdminSessionRecord>.Filter.Eq(s => s.IsRevoked, true),
                    Builders<AdminSessionRecord>.Filter.Lte(s => s.ExpiresAt, now)));

            var result = await _sessions.DeleteManyAsync(filter);
            await LogAsync(AdminAuditArea.UserManagement, "sessions-deleted", actor.Id, actor.Email, null, null, $"Deleted {result.DeletedCount} inactive session record(s).", ipAddress, userAgent);
            return result.DeletedCount;
        }

        public async Task<long> DeleteLoginActivityAsync(IEnumerable<string> ids, AdminUser actor, string ipAddress, string userAgent)
        {
            var selectedIds = NormalizeIds(ids);
            if (selectedIds.Count == 0) return 0;

            var result = await _loginActivity.DeleteManyAsync(Builders<AdminLoginActivityRecord>.Filter.In(l => l.Id, selectedIds));
            await LogAsync(AdminAuditArea.UserManagement, "login-activity-deleted", actor.Id, actor.Email, null, null, $"Deleted {result.DeletedCount} login activity log(s).", ipAddress, userAgent);
            return result.DeletedCount;
        }

        public async Task<long> DeleteAuditLogsAsync(IEnumerable<string> ids, AdminUser actor, string ipAddress, string userAgent)
        {
            var selectedIds = NormalizeIds(ids);
            if (selectedIds.Count == 0) return 0;

            var result = await _auditLogs.DeleteManyAsync(Builders<AdminAuditLog>.Filter.In(l => l.Id, selectedIds));
            await LogAsync(AdminAuditArea.UserManagement, "audit-logs-deleted", actor.Id, actor.Email, null, null, $"Deleted {result.DeletedCount} audit log(s).", ipAddress, userAgent);
            return result.DeletedCount;
        }

        public async Task SeedAdminAsync(string email, string password)
        {
            var normalizedEmail = NormalizeEmail(email);
            var exists = await _users.Find(u => u.Email == normalizedEmail).AnyAsync();
            if (exists) return;

            await _users.InsertOneAsync(new AdminUser
            {
                Email = normalizedEmail,
                FullName = "Admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = AdminRole.AdminAdmin,
                Status = AdminUserStatus.Active,
                Permissions = AdminPermissionKeys.All.ToList(),
                TokenVersion = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        public bool VerifyPassword(string plainPassword, string hash) =>
            BCrypt.Net.BCrypt.Verify(plainPassword, hash);

        public string HashPassword(string password) =>
            BCrypt.Net.BCrypt.HashPassword(password);

        public static List<string> GetEffectivePermissions(AdminUser user)
        {
            if (user.Role == AdminRole.AdminAdmin)
                return AdminPermissionKeys.All.ToList();

            var defaults = user.Role switch
            {
                AdminRole.Manager => new[]
                {
                    AdminPermissionKeys.ManageContent,
                    AdminPermissionKeys.PublishContent,
                    AdminPermissionKeys.DeleteContent,
                    AdminPermissionKeys.ViewFormDefinitions,
                    AdminPermissionKeys.EditFormDefinitions,
                    AdminPermissionKeys.ViewFormSubmissions,
                    AdminPermissionKeys.ManageFormSubmissions,
                    AdminPermissionKeys.ExportFormSubmissions
                },
                AdminRole.Writer => new[]
                {
                    AdminPermissionKeys.ManageContent,
                    AdminPermissionKeys.ViewFormDefinitions,
                    AdminPermissionKeys.EditFormDefinitions,
                    AdminPermissionKeys.ViewFormSubmissions,
                    AdminPermissionKeys.ManageFormSubmissions,
                    AdminPermissionKeys.ExportFormSubmissions
                },
                _ => Array.Empty<string>()
            };

            return defaults
                .Concat(NormalizePermissions(user.Role, user.Permissions))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static void NormalizeUserDefaults(AdminUser user)
        {
            user.Email = NormalizeEmail(user.Email);
            if (string.IsNullOrWhiteSpace(user.FullName))
                user.FullName = user.Email;
            if (user.TokenVersion <= 0)
                user.TokenVersion = 1;
            user.Permissions ??= new();
        }

        private string GenerateJwt(AdminUser user, string tokenId, List<string> permissions, DateTime expiresAt)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var claims = new List<Claim>
            {
                new("adminId", user.Id),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Name, user.Email),
                new(ClaimTypes.Role, user.Role.ToString()),
                new("tokenVersion", user.TokenVersion.ToString()),
                new(JwtRegisteredClaimNames.Jti, tokenId)
            };

            claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));

            var token = new JwtSecurityToken(
                issuer: _jwt.Issuer,
                audience: _jwt.Audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private async Task RegisterFailedLoginAsync(AdminUser user, string ipAddress, string userAgent)
        {
            var failedAttempts = user.FailedLoginAttempts + 1;
            var update = Builders<AdminUser>.Update
                .Set(u => u.FailedLoginAttempts, failedAttempts)
                .Set(u => u.UpdatedAt, DateTime.UtcNow);

            if (failedAttempts >= MaxFailedLoginAttempts)
            {
                update = update
                    .Set(u => u.Status, AdminUserStatus.Locked)
                    .Set(u => u.LockedUntil, DateTime.UtcNow.Add(LockoutDuration));
            }

            await _users.UpdateOneAsync(u => u.Id == user.Id, update);
            await RecordLoginActivityAsync(user, user.Email, "login-denied", false, "Invalid password.", ipAddress, userAgent);
            await LogAsync(AdminAuditArea.Auth, "login-denied", user.Id, user.Email, user.Id, user.Email, "Invalid password.", ipAddress, userAgent);
        }

        private static bool CanLogin(AdminUser user)
        {
            if (user.Status == AdminUserStatus.Disabled)
                return false;
            if (user.Status == AdminUserStatus.Locked && user.LockedUntil is not null && user.LockedUntil > DateTime.UtcNow)
                return false;
            return true;
        }

        private async Task RevokeSessionByTokenIdAsync(string tokenId, string actorId, AdminSessionRevokeReason reason, string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(tokenId)) return;

            await _sessions.UpdateManyAsync(s => s.TokenId == tokenId && !s.IsRevoked,
                Builders<AdminSessionRecord>.Update
                    .Set(s => s.IsRevoked, true)
                    .Set(s => s.RevokedAt, DateTime.UtcNow)
                    .Set(s => s.RevokedById, actorId)
                    .Set(s => s.RevokeReason, reason));
        }

        private async Task RevokeAllUserSessionsAsync(string adminId, string actorId, AdminSessionRevokeReason reason, string ipAddress)
        {
            await _sessions.UpdateManyAsync(s => s.AdminId == adminId && !s.IsRevoked,
                Builders<AdminSessionRecord>.Update
                    .Set(s => s.IsRevoked, true)
                    .Set(s => s.RevokedAt, DateTime.UtcNow)
                    .Set(s => s.RevokedById, actorId)
                    .Set(s => s.RevokeReason, reason));
        }

        private async Task LogAsync(AdminAuditArea area, string action, string actorId, string actorEmail, string? targetId, string? targetEmail, string message, string ipAddress, string userAgent)
        {
            await _auditLogs.InsertOneAsync(new AdminAuditLog
            {
                Area = area,
                Action = action,
                ActorId = actorId,
                ActorEmail = actorEmail,
                TargetId = targetId,
                TargetEmail = targetEmail,
                Message = message,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                CreatedAt = DateTime.UtcNow
            });
        }

        private async Task RecordLoginActivityAsync(AdminUser? user, string email, string eventType, bool success, string message, string ipAddress, string userAgent)
        {
            await _loginActivity.InsertOneAsync(new AdminLoginActivityRecord
            {
                AdminId = user?.Id,
                Email = NormalizeEmail(email),
                EventType = eventType,
                Success = success,
                Message = message,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                BrowserName = ParseBrowser(userAgent),
                OperatingSystem = ParseOperatingSystem(userAgent),
                OccurredAt = DateTime.UtcNow
            });
        }

        private static List<string> ValidateUserCreate(AdminUserCreateRequest dto)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(dto.Email) || !dto.Email.Contains('@')) errors.Add("Valid email is required.");
            if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 8) errors.Add("Password must be at least 8 characters.");
            return errors;
        }

        private static List<string> NormalizePermissions(AdminRole role, IEnumerable<string>? permissions)
        {
            var allowed = AllowedPermissionsForRole(role);
            return (permissions ?? [])
                .Where(permission => allowed.Contains(permission, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static IReadOnlyCollection<string> AllowedPermissionsForRole(AdminRole role) => role switch
        {
            AdminRole.AdminAdmin => AdminPermissionKeys.All,
            AdminRole.Manager => new[]
            {
                AdminPermissionKeys.ManageContent,
                AdminPermissionKeys.PublishContent,
                AdminPermissionKeys.DeleteContent,
                AdminPermissionKeys.ViewFormDefinitions,
                AdminPermissionKeys.EditFormDefinitions,
                AdminPermissionKeys.ViewFormSubmissions,
                AdminPermissionKeys.ManageFormSubmissions,
                AdminPermissionKeys.ExportFormSubmissions
            },
            AdminRole.Writer => new[]
            {
                AdminPermissionKeys.ManageContent,
                AdminPermissionKeys.ViewFormDefinitions,
                AdminPermissionKeys.EditFormDefinitions,
                AdminPermissionKeys.ViewFormSubmissions,
                AdminPermissionKeys.ManageFormSubmissions,
                AdminPermissionKeys.ExportFormSubmissions
            },
            _ => Array.Empty<string>()
        };

        private static List<string> NormalizeIds(IEnumerable<string>? ids) =>
            (ids ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        private static string NormalizeEmail(string email) =>
            (email ?? string.Empty).Trim().ToLowerInvariant();

        private static string DisplayName(AdminUser user) =>
            string.IsNullOrWhiteSpace(user.FullName) ? user.Email : user.FullName;

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
            if (userAgent.Contains("Mac OS", StringComparison.OrdinalIgnoreCase)) return "macOS";
            if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase)) return "Android";
            if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase)) return "iOS";
            if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase)) return "Linux";
            return "Unknown";
        }
    }
}
