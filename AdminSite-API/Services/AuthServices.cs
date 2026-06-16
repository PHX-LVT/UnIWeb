
using FullProject.DTOs;
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
        private readonly IMongoCollection<AdminUser> _users;
        private readonly JwtSettings _jwt;

        public AuthService(IMongoDatabase db, IOptions<JwtSettings> jwt)
        {
            _users = db.GetCollection<AdminUser>("admin_users");
            _jwt = jwt.Value;
        }

        public async Task<LoginResponseDto?> LoginAsync(string email, string password)
        {
            var user = await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
            if (user is null) return null;
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)) return null;

            var token = GenerateJwt(user);
            return new LoginResponseDto
            {
                Token = token,
                AdminId = user.Id,
                Email = user.Email
            };
        }

        public async Task<AdminUser?> GetByIdAsync(string adminId) =>
            await _users.Find(u => u.Id == adminId).FirstOrDefaultAsync();

        public async Task SeedAdminAsync(string email, string password)
        {
            var exists = await _users.Find(u => u.Email == email).AnyAsync();
            if (exists) return;

            await _users.InsertOneAsync(new AdminUser
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                CreatedAt = DateTime.UtcNow
            });
        }
        public bool VerifyPassword(string plainPassword, string hash) =>
            BCrypt.Net.BCrypt.Verify(plainPassword, hash);
        public string HashPassword(string password) =>
            BCrypt.Net.BCrypt.HashPassword(password);
        private string GenerateJwt(AdminUser user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var claims = new[]
            {
                new Claim("adminId", user.Id),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _jwt.Issuer,
                audience: _jwt.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(_jwt.ExpiryHour),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        public async Task UpdatePasswordAsync(string id, string passwordHash)
        {
            var update = Builders<AdminUser>.Update.Set(a => a.PasswordHash, passwordHash);
            await _users.UpdateOneAsync(a => a.Id == id, update);
        }


    }
}
