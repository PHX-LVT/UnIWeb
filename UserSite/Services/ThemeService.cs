using Contracts.Global;

using Contracts.Public;

namespace UserSite.Services
{
    public class ThemeService
    {
        private readonly PublicApiService _api;
        private PublicTheme? _theme;
        public PublicOperationStatus LastLoadStatus { get; private set; } = PublicOperationStatus.Success;
        public string LastLoadMessage { get; private set; } = string.Empty;

        public ThemeService(PublicApiService api)
        {
            _api = api;
        }

        public async Task<PublicTheme> GetAsync(bool forceReload = false)
        {
            if (_theme is not null && !forceReload) return _theme;

            var result = await _api.GetThemeAsync();
            LastLoadStatus = result.Status;
            LastLoadMessage = result.Message;
            _theme = Normalize(result.Data);
            return _theme;
        }

        public string ToCssVariables(PublicTheme t) => ThemeCssBuilder.Build(Normalize(t));

        private static PublicTheme Normalize(PublicTheme? theme)
        {
            theme ??= new PublicTheme();
            theme.FontBody = ThemeFontCatalog.NormalizeNameOrDefault(theme.FontBody);
            theme.FontHeading = ThemeFontCatalog.NormalizeNameOrDefault(theme.FontHeading);
            return theme;
        }
    }
}
