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

        private static PublicTheme ToPublicTheme(ThemeModel? model) => new()
        {
            FontBody = model?.FontBody ?? "Inter",
            FontHeading = model?.FontHeading ?? "Inter",
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
            ButtonSizeScale = model?.ButtonSizeScale ?? "1",
            ButtonTextSize = model?.ButtonTextSize ?? "15px",
            AnimationsEnabled = model?.AnimationsEnabled ?? true,
            AnimationSpeed = model?.AnimationSpeed ?? "normal",
            SpacingScale = model?.SpacingScale ?? "1"
        };
    }
}
