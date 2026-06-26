using Contracts.Public;
using FullProject.DTOs;

namespace FullProject.Services.PublicService
{
    public class PublicMetadataService
    {
        private readonly PageService _pageService;
        private readonly FooterService _footerService;
        private readonly SocialButtonsService _socialService;
        private readonly GlobalButtonsService _globalButtonsService;
        private readonly ThemeService _themeService;
        private readonly BrandingService _brandingService;
        private readonly SettingsService _settingsService;

        public PublicMetadataService(
            PageService pageService,
            FooterService footerService,
            SocialButtonsService socialService,
            GlobalButtonsService globalButtonsService,
            ThemeService themeService,
            BrandingService brandingService,
            SettingsService settingsService)
        {
            _pageService = pageService;
            _footerService = footerService;
            _socialService = socialService;
            _globalButtonsService = globalButtonsService;
            _themeService = themeService;
            _brandingService = brandingService;
            _settingsService = settingsService;
        }

        public async Task<List<PublicNavItemDto>> GetNavigationAsync()
        {
            var pages = await _pageService.GetPublicRootPagesAsync();
            return pages
                .OrderBy(p => p.Order)
                .Select(p => new PublicNavItemDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Slug = p.Slug,
                    Order = p.Order
                })
                .ToList();
        }

        public async Task<BrandingResponseDto> GetBrandingAsync()
        {
            var branding = await _brandingService.GetAsync();
            return new BrandingResponseDto
            {
                CompanyName = branding.CompanyName,
                LogoUrl = branding.LogoUrl,
                Href = branding.Href
            };
        }

        public async Task<FooterResponseDto> GetFooterAsync()
        {
            var footer = await _footerService.GetAsync();
            return new FooterResponseDto
            {
                CompanyName = footer.CompanyName,
                Groups = footer.Groups
                    .Where(g => g.Visible)
                    .OrderBy(g => g.Order)
                    .Select(g => new FooterGroupResponseDto
                    {
                        Id = g.Id,
                        Label = g.Label,
                        Visible = g.Visible,
                        Order = g.Order,
                        Links = g.Links
                            .Where(l => l.Visible)
                            .OrderBy(l => l.Order)
                            .Select(l => new FooterLinkResponseDto
                            {
                                Id = l.Id,
                                Label = l.Label,
                                Href = l.Href,
                                Visible = l.Visible,
                                Order = l.Order
                            }).ToList()
                    }).ToList()
            };
        }

        public async Task<SocialButtonGroupResponseDto> GetSocialAsync()
        {
            var group = await _socialService.GetGroupAsync();
            return new SocialButtonGroupResponseDto
            {
                GroupVisible = group.GroupVisible,
                Buttons = group.Buttons
                    .Where(b => b.Visible)
                    .OrderBy(b => b.Order)
                    .Select(b => new SocialButtonResponseDto
                    {
                        Id = b.Id,
                        Label = b.Label,
                        Icon = b.Icon,
                        Href = b.Href,
                        Visible = b.Visible,
                        Order = b.Order
                    }).ToList()
            };
        }

        public async Task<List<GlobalButtonResponseDto>> GetGlobalButtonsAsync()
        {
            var buttons = await _globalButtonsService.GetAllAsync();
            return buttons
                .Where(b => b.Visible)
                .OrderBy(b => b.Order)
                .Select(b => new GlobalButtonResponseDto
                {
                    Id = b.Id,
                    LabelText = b.LabelText,
                    Action = b.Action,
                    Href = b.Href,
                    FormDefinitionId = b.FormDefinitionId,
                    Position = b.Position,
                    Visible = b.Visible,
                    Order = b.Order
                })
                .ToList();
        }

        public async Task<ThemeResponseDto> GetThemeAsync()
        {
            var theme = await _themeService.GetAsync();
            return new ThemeResponseDto
            {
                FontBody = theme.FontBody,
                FontHeading = theme.FontHeading,
                TextSizeBase = theme.TextSizeBase,
                TextSizeEyebrow = theme.TextSizeEyebrow,
                TextSizeHeading = theme.TextSizeHeading,
                TextSizeSubheading = theme.TextSizeSubheading,
                TextSizeBody = theme.TextSizeBody,
                TextSizeSmall = theme.TextSizeSmall,
                TextSizeItemTitle = theme.TextSizeItemTitle,
                ColorPrimary = theme.ColorPrimary,
                ColorAccent = theme.ColorAccent,
                ColorBackground = theme.ColorBackground,
                ColorText = theme.ColorText,
                BorderRadius = theme.BorderRadius,
                ButtonSizeScale = theme.ButtonSizeScale,
                ButtonTextSize = theme.ButtonTextSize,
                AnimationsEnabled = theme.AnimationsEnabled,
                AnimationSpeed = theme.AnimationSpeed,
                SpacingScale = theme.SpacingScale
            };
        }

        public async Task<SiteSettingsResponseDto> GetLanguagesAsync()
        {
            var settings = await _settingsService.GetAsync();
            return new SiteSettingsResponseDto
            {
                DefaultLanguage = settings.DefaultLanguage,
                Languages = settings.Languages
                    .Where(l => l.Active && l.UserEnabled)
                    .OrderBy(l => l.Order)
                    .Select(l => new LanguageResponseDto
                    {
                        Slug = l.Slug,
                        Label = l.Label,
                        NativeName = l.NativeName,
                        Active = l.Active,
                        AdminEnabled = l.AdminEnabled,
                        UserEnabled = l.UserEnabled,
                        IsFallback = l.Slug == settings.DefaultLanguage,
                        Protected = l.Slug == settings.DefaultLanguage,
                        Direction = l.Direction,
                        Order = l.Order
                    })
                    .ToList()
            };
        }
    }
}
