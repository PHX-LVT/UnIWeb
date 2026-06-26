using FullProject.Services;
using Microsoft.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;

namespace FullProject.Middleware
{
    public sealed class AdminSessionValidationMiddleware
    {
        private static readonly string[] ExcludedPrefixes =
        [
            "/swagger",
            "/api/auth/login",
            "/api/public",
            "/health"
        ];

        private readonly RequestDelegate _next;

        public AdminSessionValidationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, AuthService authService)
        {
            var path = context.Request.Path.Value ?? string.Empty;
            if (ExcludedPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                await _next(context);
                return;
            }

            if (!context.Request.Headers.TryGetValue(HeaderNames.Authorization, out var authValues))
            {
                await _next(context);
                return;
            }

            var authHeader = authValues.ToString();
            if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            var adminId = context.User.FindFirst("adminId")?.Value;
            var tokenId = context.User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value ??
                          context.User.FindFirst("jti")?.Value;
            var tokenVersionValue = context.User.FindFirst("tokenVersion")?.Value;

            if (!int.TryParse(tokenVersionValue, out var tokenVersion) ||
                !await authService.ValidateSessionAsync(adminId ?? string.Empty, tokenId ?? string.Empty, tokenVersion))
            {
                context.Response.Headers["X-Admin-Session-Invalid"] = "true";
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await _next(context);
        }
    }
}
