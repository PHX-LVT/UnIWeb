using AdminSite.Models;
using AdminSite.Services;
using Contracts.Admin;
using System.Xml.Linq;//using DevExpress.XtraPrinting.Native.Preview;

namespace AdminSite.Services
{

    public class AdminPageService
    {
        private readonly IHttpService _http;
        private readonly AdminSectionService _sectionService;
        private readonly AdminFormSubmissionService _formService;
        public AdminPageService(IHttpService http, AdminSectionService sectionService, AdminFormSubmissionService formService)
        {
            _http = http;
            _sectionService = sectionService;
            _formService = formService;
        }
        public Task<ApiResponse<List<PageModel>>> GetAllAsync() =>
            _http.GetAsync<List<PageModel>>("api/admin/pages");

        public Task<ApiResponse<PageModel>> GetByIdAsync(string pageId) =>
            _http.GetAsync<PageModel>($"api/admin/pages/{pageId}");

        public Task<ApiResponse<PageModel>> CreateAsync(PageRequest req) =>
            _http.PostAsync<PageModel>("api/admin/pages", req);

        public Task<ApiResponse<PageModel>> UpdateAsync(string pageId, PageRequest req) =>
            _http.PutAsync<PageModel>($"api/admin/pages/{pageId}", req);

        public Task<ApiResponse<object>> DeleteAsync(string pageId) =>
            _http.DeleteAsync<object>($"api/admin/pages/{pageId}");

        public Task<ApiResponse<object>> SetVisibilityAsync(string pageId, bool visible) =>
            _http.PutAsync<object>($"api/admin/pages/{pageId}/visibility",
                new VisibilityRequest { Visible = visible });

        public Task<ApiResponse<object>> SetAccessAsync(string pageId, bool access) =>
            _http.PutAsync<object>($"api/admin/pages/{pageId}/access",
                new VisibilityRequest { Visible = access });

        public Task<ApiResponse<object>> ReorderAsync(List<string> orderedIds) =>
            _http.PutAsync<object>("api/admin/pages/reorder",
                new ReorderRequest { OrderedIds = orderedIds });
        
        public async Task CreateFromTemplateAsync(string template)
        {
            var name = new Dictionary<string, string> { ["en"] = template.ToUpper() };
            var page = await CreateAsync(new PageRequest { Name = name });
            if (page?.Data?.Id is null) return;
            await SeedTemplateAsync(page.Data.Id, template);
        }
        public async Task SeedTemplateAsync(string pageId, string template)
        {
            var quoteFormId = await GetFormDefinitionIdAsync("quote");
            foreach (var section in BuildTemplateSections(template, quoteFormId))
                await _sectionService.CreateAsync(pageId, section);
        }


        private async Task<string?> GetFormDefinitionIdAsync(string key)
        {
            var definitions = await _formService.GetDefinitionsAsync();
            return definitions.Data?
                .FirstOrDefault(definition => definition.Active &&
                    string.Equals(definition.Key, key, StringComparison.OrdinalIgnoreCase))
                ?.Id;
        }
        private static IEnumerable<SectionCreateDto> BuildTemplateSections(string template, string? quoteFormId) => template switch
        {
            "landing" => LandingTemplate(),
            "about" => AboutTemplate(),
            "contact" => ContactTemplate(),
            "services" => ServicesTemplate(quoteFormId),
            "portfolio" => PortfolioTemplate(),
            "faq" => FaqTemplate(),
            _ => Array.Empty<SectionCreateDto>()
        };

        private static IEnumerable<SectionCreateDto> LandingTemplate() => new SectionCreateDto[]
        {
            Hero(
                "Starter page",
                "A clear promise for your new page",
                "Use this hero to introduce the page, explain who it is for, and point visitors toward the next action.",
                "centered",
                "#061b3a",
                TextLight(),
                Button("Start editing", "#first-section")),
            new StatsSectionCreateDto
            {
                SectionTitle = L("Quick proof points"),
                Style = SoftBand(),
                Items = new()
                {
                    new() { Value = 24, Suffix = "/7", Label = L("Availability"), Order = 0 },
                    new() { Value = 98, Suffix = "%", Label = L("On-time workflow"), Order = 1 },
                    new() { Value = 12, Suffix = "+", Label = L("Editable sections"), Order = 2 },
                    new() { Value = 1, Prefix = "<", Suffix = " day", Label = L("Launch-ready draft"), Order = 3 }
                }
            },
            new ListSectionCreateDto
            {
                SectionTitle = L("What this page can teach"),
                Columns = 3,
                Style = WhiteSection("large"),
                Items = new()
                {
                    ListItem("fas fa-heading", "Hero section", "Change eyebrow, heading, subheading, buttons, background, and alignment.", 0),
                    ListItem("fas fa-th-large", "Card grid", "Use list cards to explain features, services, benefits, or steps.", 1),
                    ListItem("fas fa-bullhorn", "CTA section", "Close the page with one focused action and a short supporting sentence.", 2)
                }
            },
            new CarouselSectionCreateDto
            {
                SectionTitle = L("Example rotating highlights"),
                Columns = 3,
                Layout = "cards",
                ShowArrows = true,
                ShowDots = true,
                Style = SoftBand(),
                Items = new()
                {
                    CarouselItem("Plan", "Set the story", "Start with the problem this page solves.", 0),
                    CarouselItem("Build", "Add useful sections", "Use sections as reusable patterns, not one-off decoration.", 1),
                    CarouselItem("Publish", "Review and launch", "Preview, adjust, publish, and compare against UserSite.", 2)
                }
            },
            Cta("Ready to customize this page?", "Open each section in the editor, change the sample text, then publish when the preview feels right.", "Edit sections", "#first-section")
        };

        private static IEnumerable<SectionCreateDto> AboutTemplate() => new SectionCreateDto[]
        {
            Hero(
                "About us",
                "Tell visitors who you are and why your work matters",
                "This starter structure gives non-code admins a complete About page with story, values, metrics, and a closing action.",
                "split-left",
                "#ffffff",
                null,
                Button("Meet the team", "#values", "outline")),
            Html(
                "Our story",
                "<p>Replace this paragraph with the company origin story. Keep it concrete: what problem you saw, what you built, and what customers can trust you to do well.</p><p>This HTML section is useful for richer editorial writing when a normal card or list is too rigid.</p>"),
            new TestimonialSectionCreateDto
            {
                Eyebrow = L("Values"),
                SectionTitle = L("Principles that shape the work"),
                Subheading = L("Use testimonial/value cards for beliefs, advantages, or trust signals."),
                Columns = 3,
                Layout = "cards",
                Style = SoftBand(),
                Items = new()
                {
                    TestimonialItem("fas fa-handshake", "Reliability", "Show what customers can consistently expect from you.", 0),
                    TestimonialItem("fas fa-eye", "Transparency", "Explain how you make progress, pricing, or operations visible.", 1),
                    TestimonialItem("fas fa-seedling", "Long-term thinking", "Describe the standard you want every project to meet.", 2)
                }
            },
            new StatsSectionCreateDto
            {
                SectionTitle = L("Numbers worth highlighting"),
                Columns = 3,
                Style = WhiteSection(),
                Items = new()
                {
                    new() { Value = 10, Suffix = "+", Label = L("Years of experience"), Order = 0 },
                    new() { Value = 500, Suffix = "+", Label = L("Projects delivered"), Order = 1 },
                    new() { Value = 3, Suffix = "x", Label = L("Regional coverage"), Order = 2 }
                }
            },
            Cta("Add a human next step", "Use this CTA to send visitors to Contact, Careers, or a service page.", "Contact us", "/contact")
        };

        private static IEnumerable<SectionCreateDto> ContactTemplate() => new SectionCreateDto[]
        {
            Hero(
                "Contact",
                "Make it easy for visitors to reach the right team",
                "Use this page to provide contact methods, office locations, and clear expectations for response time.",
                "centered",
                "#061b3a",
                TextLight(),
                Button("Send a message", "#contact-details")),
            new ListSectionCreateDto
            {
                SectionTitle = L("Contact details"),
                Columns = 3,
                Style = WhiteSection("large"),
                Items = new()
                {
                    ListItem("fas fa-envelope", "Email", "hello@example.com", 0),
                    ListItem("fas fa-phone", "Phone", "+84 000 000 000", 1),
                    ListItem("fas fa-clock", "Response time", "We usually respond within one business day.", 2)
                }
            },
            new NetworkMapSectionCreateDto
            {
                SectionTitle = L("Example office network"),
                CenterLat = 15.87,
                CenterLng = 108.33,
                DefaultZoom = 5,
                Style = SoftBand("large"),
                Pins = new()
                {
                    new() { Label = "Ho Chi Minh City", Lat = 10.8231, Lng = 106.6297, Order = 0 },
                    new() { Label = "Da Nang", Lat = 16.0544, Lng = 108.2022, Order = 1 },
                    new() { Label = "Ha Noi", Lat = 21.0278, Lng = 105.8342, Order = 2 }
                }
            },
            Cta("Need a custom form?", "This starter uses editable sections first. Add a Form Block later when the exact fields are decided.", "Open form settings", "#")
        };

        private static IEnumerable<SectionCreateDto> ServicesTemplate(string? quoteFormId) => new SectionCreateDto[]
        {
            Hero(
                "Services",
                "Present your service lineup with clear outcomes",
                "Start with the business result, then use cards and process sections to explain how the service works.",
                "split-right",
                "#ffffff",
                null,
                Button("View services", "#services")),
            new ListSectionCreateDto
            {
                SectionTitle = L("Service examples"),
                Columns = 3,
                Style = SoftBand("large"),
                Items = new()
                {
                    ListItem("fas fa-route", "Planning", "Map the workflow, dependencies, and operating constraints.", 0),
                    ListItem("fas fa-cogs", "Execution", "Coordinate teams, assets, content, and delivery milestones.", 1),
                    ListItem("fas fa-chart-line", "Optimization", "Use reports and feedback to improve the next cycle.", 2)
                }
            },
            new TestimonialSectionCreateDto
            {
                Eyebrow = L("Process"),
                SectionTitle = L("A simple delivery flow"),
                HeaderAlignment = "left",
                Columns = 4,
                Style = WhiteSection(),
                Items = new()
                {
                    TestimonialItem("fas fa-search", "Discover", "Understand goals and constraints.", 0),
                    TestimonialItem("fas fa-pencil-ruler", "Design", "Choose the right section pattern.", 1),
                    TestimonialItem("fas fa-layer-group", "Build", "Add content and reusable blocks.", 2),
                    TestimonialItem("fas fa-check", "Publish", "Review preview and launch.", 3)
                }
            },
            Cta("Turn this into a real service page", "Replace each card with a real service, then link the CTA to Contact or Quote.", "Get a quote", "/contact", quoteFormId)
        };

        private static IEnumerable<SectionCreateDto> PortfolioTemplate() => new SectionCreateDto[]
        {
            Hero(
                "Portfolio",
                "Show selected work with context, proof, and next steps",
                "Use this template for projects, customers, case snapshots, or internal showcase pages.",
                "centered",
                "#061b3a",
                TextLight(),
                Button("Explore examples", "#work")),
            new ListSectionCreateDto
            {
                Layout = "cards",
                Columns = 3,
                SectionTitle = L("Portfolio examples"),
                Style = WhiteSection("large"),
                Items = new()
                {
                    new() { Title = L("Project image placeholder"), Description = L("Add a real image URL in the editor."), Order = 0 },
                    new() { Title = L("Result caption"), Description = L("Use card text to explain the result, not just the image."), Order = 1 },
                    new() { Title = L("Visual proof"), Description = L("Use Library sections with Gallery layout for managed media galleries."), Order = 2 }
                }
            },
            new StatsSectionCreateDto
            {
                SectionTitle = L("Portfolio proof points"),
                Columns = 3,
                Style = SoftBand(),
                Items = new()
                {
                    new() { Value = 36, Suffix = "+", Label = L("Completed examples"), Order = 0 },
                    new() { Value = 8, Suffix = "+", Label = L("Industries covered"), Order = 1 },
                    new() { Value = 92, Suffix = "%", Label = L("Reusable structure"), Order = 2 }
                }
            },
            Cta("Want to feature another project?", "Duplicate this structure, update the gallery, and add a stronger case summary.", "Add project", "#")
        };

        private static IEnumerable<SectionCreateDto> FaqTemplate() => new SectionCreateDto[]
        {
            Hero(
                "FAQ",
                "Answer common questions before visitors need to ask",
                "Use short, direct answers. Put the most important or most frequent questions first.",
                "centered",
                "#ffffff",
                null),
            new ListSectionCreateDto
            {
                Layout = "cards",
                Columns = 2,
                SectionTitle = L("Frequently asked questions"),
                ShowIcon = false,
                Style = SoftBand("large"),
                Items = new()
                {
                    ListItem("", "How do I edit this page?", "Open each section from the Canvas overlay, then change its fields in the editor panel.", 0),
                    ListItem("", "Can I reorder sections?", "Yes. Drag sections in the Canvas controls, then publish when the order is correct.", 1),
                    ListItem("", "What should be translated?", "Titles, labels, captions, descriptions, button text, and visible content should be multilingual.", 2),
                    ListItem("", "What should stay technical?", "Slug, href, icon, layout, style, image URL, file name, and action type should stay as structured settings.", 3)
                }
            },
            Cta("Still need help?", "Send visitors to a contact method, guide, or support page.", "Contact support", "/contact")
        };

        private static HeroSectionCreateDto Hero(
            string eyebrow,
            string heading,
            string subheading,
            string layout,
            string backgroundColor,
            SectionStyleDto? style,
            params SectionButtonDto[] buttons) => new()
            {
                Layout = layout,
                ContentAlignment = layout == "centered" ? "center" : "left",
                HeadingSize = "large",
                Eyebrow = L(eyebrow),
                Heading = L(heading),
                Subheading = L(subheading),
                Buttons = buttons.ToList(),
                Style = style ?? new SectionStyleDto
                {
                    BackgroundType = "color",
                    BackgroundColor = backgroundColor,
                    Padding = "large",
                    ContentWidth = "normal",
                    TextColor = "dark"
                }
            };

        private static HtmlSectionCreateDto Html(string title, string bodyHtml) => new()
        {
            Content = L($"""
                <div class="sc-html-prose">
                    <p class="sc-eyebrow">{title}</p>
                    <h2>{title}</h2>
                    {bodyHtml}
                </div>
                """),
            Style = WhiteSection("large")
        };

        private static CtaSectionCreateDto Cta(string heading, string subtext, string buttonLabel, string href, string? formDefinitionId = null) => new()
        {
            Layout = "center",
            Heading = L(heading),
            Subtext = L(subtext),
            Buttons = new() { Button(buttonLabel, href, formDefinitionId: formDefinitionId) },
            Style = new SectionStyleDto
            {
                BackgroundType = "color",
                BackgroundColor = "#061b3a",
                TextColor = "light",
                Padding = "large",
                ContentWidth = "normal"
            }
        };

        private static SectionButtonDto Button(string label, string href, string style = "filled", int order = 0, string? formDefinitionId = null) => new()
        {
            Label = L(label),
            Action = string.IsNullOrWhiteSpace(formDefinitionId) ? "linkToPage" : "openForm",
            Href = string.IsNullOrWhiteSpace(formDefinitionId) ? href : null,
            FormDefinitionId = formDefinitionId,
            Style = style,
            Visible = true,
            Order = order
        };

        private static ListItemDto ListItem(string icon, string title, string description, int order) => new()
        {
            Icon = icon,
            Title = L(title),
            Description = L(description),
            Order = order,
            Visible = true
        };

        private static TestimonialItemDto TestimonialItem(string icon, string title, string description, int order) => new()
        {
            Icon = icon,
            Title = L(title),
            Description = L(description),
            Order = order,
            Visible = true
        };

        private static CarouselItemDto CarouselItem(string tag, string title, string description, int order) => new()
        {
            Tag = L(tag),
            Title = L(title),
            Description = L(description),
            Order = order,
            Visible = true
        };

        private static SectionStyleDto WhiteSection(string padding = "medium") => new()
        {
            BackgroundType = "color",
            BackgroundColor = "#ffffff",
            TextColor = "dark",
            Padding = padding,
            ContentWidth = "normal"
        };

        private static SectionStyleDto SoftBand(string padding = "medium") => new()
        {
            BackgroundType = "color",
            BackgroundColor = "#f5f7fb",
            TextColor = "dark",
            Padding = padding,
            ContentWidth = "normal"
        };

        private static SectionStyleDto TextLight() => new()
        {
            BackgroundType = "color",
            BackgroundColor = "#061b3a",
            TextColor = "light",
            Padding = "large",
            ContentWidth = "normal"
        };

        private static Dictionary<string, string> L(string value) => new() { ["en"] = value };
        public Task<ApiResponse<object>> PublishAsync(string pageId) =>
            _http.PostAsync<object>($"api/admin/pages/{pageId}/publish", new { });

        public Task<ApiResponse<object>> ResetAsync(string pageId) =>
            _http.PostAsync<object>($"api/admin/pages/{pageId}/reset", new { });

        // Child pages
        public Task<ApiResponse<List<PageModel>>> GetChildrenAsync(string pageId) =>
            _http.GetAsync<List<PageModel>>($"api/admin/pages/{pageId}/children");

        public Task<ApiResponse<PageModel>> CreateChildAsync(string pageId, PageRequest req) =>
            _http.PostAsync<PageModel>($"api/admin/pages/{pageId}/children", req);

        public Task<ApiResponse<object>> DeleteChildAsync(string pageId, string childId) =>
            _http.DeleteAsync<object>($"api/admin/pages/{pageId}/children/{childId}");

        public Task<ApiResponse<object>> SetChildVisibilityAsync(string pageId, string childId, bool visible) =>
            _http.PutAsync<object>($"api/admin/pages/{pageId}/children/{childId}/visibility",
                new VisibilityRequest { Visible = visible });

        public Task<ApiResponse<object>> SetChildAccessAsync(string pageId, string childId, bool access) =>
            _http.PutAsync<object>($"api/admin/pages/{pageId}/children/{childId}/access",
                new VisibilityRequest { Visible = access });

        public Task<ApiResponse<object>> UpdateChildCardAsync(string pageId, string childId, PageCardRequest req) =>
            _http.PutAsync<object>($"api/admin/pages/{pageId}/children/{childId}/card", req);

        public Task<ApiResponse<object>> ResetChildCardAsync(string pageId, string childId) =>
            _http.PutAsync<object>($"api/admin/pages/{pageId}/children/{childId}/card/reset", new { });

        public Task<ApiResponse<object>> ReorderChildrenAsync(string pageId, List<string> orderedIds) =>
            _http.PutAsync<object>($"api/admin/pages/{pageId}/children/reorder",
                new ReorderRequest { OrderedIds = orderedIds });

    }
}

