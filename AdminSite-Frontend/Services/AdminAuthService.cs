using AdminSite.Models;
using Blazored.LocalStorage;

namespace AdminSite.Services
{
    public class AdminAuthService
    {
        private readonly IHttpService _http;
        private readonly ILocalStorageService _storage;

        public AdminSession? CurrentUser { get; private set; }
        public bool IsAuthenticated => CurrentUser is not null
                                    && !string.IsNullOrEmpty(CurrentUser.Token);

        public AdminAuthService(IHttpService http, ILocalStorageService storage)
        {
            _http = http;
            _storage = storage;
        }

        // Called on every first render to restore session from localStorage
        public async Task InitializeAsync()
        {
            CurrentUser = await _storage.GetItemAsync<AdminSession>("admin_session");
        }

        public async Task<(bool Success, string? Error)> LoginAsync(
            string email, string password)
        {
            var response = await _http.PostAsync<LoginResponse>(
                "api/auth/login",
                new LoginRequest { Email = email, Password = password });

            if (!response.Success || response.Data is null)
                return (false, response.Message ?? "Login failed.");

            CurrentUser = new AdminSession
            {
                AdminId = response.Data.AdminId,
                Email = response.Data.Email,
                Token = response.Data.Token
            };

            await _storage.SetItemAsync("admin_session", CurrentUser);
            return (true, null);
        }

        public async Task LogoutAsync()
        {
            CurrentUser = null;
            await _storage.RemoveItemAsync("admin_session");
        }
    }
}
