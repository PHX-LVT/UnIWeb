using Contracts.Global;

namespace UserSite.Services
{
    public class ThemeService
    {
        private readonly PublicApiService _api;
        private PublicTheme? _theme;

        public ThemeService(PublicApiService api)
        {
            _api = api;
        }

        public async Task<PublicTheme> GetAsync()
        {
            _theme ??= await _api.GetThemeAsync();
            return _theme;
        }

        public string ToCssVariables(PublicTheme t) => ThemeCssBuilder.Build(t);
    }
}
