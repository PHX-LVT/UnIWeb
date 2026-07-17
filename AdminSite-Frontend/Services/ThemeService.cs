using AdminSite.Models;
using Contracts.Global;

namespace AdminSite.Services
{
    public class ThemeService
    {
        private readonly IHttpService _http;
        public ThemeService(IHttpService http) => _http = http;

        public Task<ApiResponse<ThemeModel>> GetAsync() =>
            _http.GetAsync<ThemeModel>("api/admin/global/theme");

        public Task<ApiResponse<ThemeModel>> UpdateAsync(ThemeModel model) =>
            _http.PutAsync<ThemeModel>("api/admin/global/theme", model);

        public string ToCssVariables(ThemeModel? model) => ThemeCssBuilder.Build(ToPublicTheme(model));

        public string ToScopedCssVariables(ThemeModel? model, string selector) =>
            ThemeCssBuilder.BuildScoped(ToPublicTheme(model), selector);

        private static PublicTheme ToPublicTheme(ThemeModel? model) => new()
        {
            FontBody = ThemeFontCatalog.NormalizeNameOrDefault(model?.FontBody),
            FontHeading = ThemeFontCatalog.NormalizeNameOrDefault(model?.FontHeading),
            TextSizeBase = model?.TextSizeBase ?? "16px",
            TextSizeEyebrow = model?.TextSizeEyebrow ?? "13px",
            TextSizeHeading = model?.TextSizeHeading ?? "40px",
            TextSizeSubheading = model?.TextSizeSubheading ?? "17px",
            TextSizeBody = model?.TextSizeBody ?? "16px",
            TextSizeSmall = model?.TextSizeSmall ?? "13px",
            TextSizeItemTitle = model?.TextSizeItemTitle ?? "20px",
            ColorPrimary = model?.ColorPrimary ?? "#001a33",
            ColorAccent = model?.ColorAccent ?? "#e5c076",
            ColorBackground = model?.ColorBackground ?? "#ffffff",
            ColorText = model?.ColorText ?? "#111827",
            BorderRadius = model?.BorderRadius ?? "10px",
            ButtonStyle = model?.ButtonStyle ?? "filled",
            ButtonColorRole = model?.ButtonColorRole ?? "accent",
            ButtonRadius = model?.ButtonRadius ?? "6px",
            ButtonSizeScale = model?.ButtonSizeScale ?? "1",
            ButtonTextSize = model?.ButtonTextSize ?? "15px",
            AnimationsEnabled = model?.AnimationsEnabled ?? true,
            AnimationSpeed = model?.AnimationSpeed ?? "normal",
            SpacingScale = model?.SpacingScale ?? "1"
        };
    }
}
