using AdminSite.Models;
using AdminSite.Services;
using AdminSite.Services.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdminSite.Pages;

[AllowAnonymous]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class AdminLoginModel : Microsoft.AspNetCore.Mvc.RazorPages.PageModel
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly IReadOnlyList<LoginLanguageOption> DefaultLanguageOptions =
    [
        new("en", "English", "English", "ltr"),
        new("vi", "Vietnamese", "Tiếng Việt", "ltr"),
        new("cn", "Chinese", "中文", "ltr")
    ];

    private static readonly string[] LoginTextKeys =
    [
        "pageTitle",
        "mainTitle",
        "greeting",
        "subtitle",
        "email",
        "password",
        "submit",
        "language",
        "emailRequired",
        "passwordRequired",
        "emailInvalid",
        "invalidCredentials",
        "invalidSession",
        "unableToSignIn",
        "tooManyAttempts",
        "temporaryUnavailable",
        "loginFailed",
        "signedOut",
        "sessionExpired",
        "passwordChanged",
        "passwordReset",
        "accountDisabled",
        "accountDeleted",
        "accountUpdated",
        "accessDenied"
    ];

    private static readonly IReadOnlyDictionary<string, string> LoginTextCatalogKeys =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["pageTitle"] = "AdminLoginPageTitle",
            ["mainTitle"] = "AdminLoginMainTitle",
            ["greeting"] = "AdminLoginGreeting",
            ["subtitle"] = "AdminLoginSubtitle",
            ["email"] = "AdminLoginEmail",
            ["password"] = "AdminLoginPassword",
            ["submit"] = "AdminLoginSubmit",
            ["language"] = "AdminLoginLanguage",
            ["emailRequired"] = "AdminLoginEmailRequired",
            ["passwordRequired"] = "AdminLoginPasswordRequired",
            ["emailInvalid"] = "AdminLoginEmailInvalid",
            ["invalidCredentials"] = "AdminLoginInvalidCredentials",
            ["invalidSession"] = "AdminLoginInvalidSession",
            ["unableToSignIn"] = "AdminLoginUnableToSignIn",
            ["tooManyAttempts"] = "AdminLoginTooManyAttempts",
            ["temporaryUnavailable"] = "AdminLoginTemporaryUnavailable",
            ["loginFailed"] = "AdminLoginFailed",
            ["signedOut"] = "AdminLoginSignedOut",
            ["sessionExpired"] = "AdminLoginSessionExpired",
            ["passwordChanged"] = "AdminLoginPasswordChanged",
            ["passwordReset"] = "AdminLoginPasswordReset",
            ["accountDisabled"] = "AdminLoginAccountDisabled",
            ["accountDeleted"] = "AdminLoginAccountDeleted",
            ["accountUpdated"] = "AdminLoginAccountUpdated",
            ["accessDenied"] = "AdminLoginAccessDenied"
        };

    private readonly AdminApiAuthenticationClient _authenticationClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AdminLoginModel> _logger;

    public AdminLoginModel(
        AdminApiAuthenticationClient authenticationClient,
        IHttpClientFactory httpClientFactory,
        ILogger<AdminLoginModel> logger)
    {
        _authenticationClient = authenticationClient;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [BindProperty]
    public LoginInput Input { get; set; } = new();

    [BindProperty]
    public string ReturnUrl { get; set; } = "/";

    [BindProperty]
    public string LanguageCode { get; set; } = "en";

    public IReadOnlyList<LoginLanguageOption> LanguageOptions { get; private set; } = DefaultLanguageOptions;
    public string FallbackLanguage { get; private set; } = "en";
    public string CurrentLanguage { get; private set; } = "en";
    public IReadOnlyDictionary<string, string> Text { get; private set; } = BuildLoginText("en");
    public string LoginTranslationsJson { get; private set; } = "{}";
    public string LoginAppearanceCss { get; private set; } = AdminAppearancePresets.ToLoginCssVariables("navy-gold");
    public string StatusReason { get; private set; } = string.Empty;
    public string? StatusMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        string? returnUrl = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        if (User.Identity?.IsAuthenticated == true)
            return LocalRedirect(SafeReturnUrl(returnUrl));

        ReturnUrl = SafeReturnUrl(returnUrl);
        StatusReason = NormalizeReason(reason);
        await LoadLoginAppearanceStateAsync(cancellationToken);
        await LoadLoginLanguageStateAsync(null, cancellationToken);
        StatusMessage = StatusReason.Length > 0 ? Text[StatusReason] : null;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        ReturnUrl = SafeReturnUrl(ReturnUrl);
        await LoadLoginAppearanceStateAsync(cancellationToken);
        await LoadLoginLanguageStateAsync(LanguageCode, cancellationToken);
        ValidateInput();

        if (!ModelState.IsValid)
            return Page();

        var result = await _authenticationClient.LoginAsync(
            Input.Email,
            Input.Password,
            cancellationToken);

        if (!result.Success || result.Login is null)
        {
            ModelState.AddModelError(string.Empty, LocalizeAuthenticationError(result.Error));
            return Page();
        }

        if (string.IsNullOrWhiteSpace(result.Login.Email) ||
            string.IsNullOrWhiteSpace(result.Login.RoleId) ||
            string.IsNullOrWhiteSpace(result.Login.RoleName) ||
            result.Login.Status != AdminUserStatus.Active ||
            !AdminAuthConstants.TryReadJwtMetadata(
                result.Login.Token,
                out var expiresUtc,
                out var tokenId,
                out var jwtAdminId) ||
            !string.Equals(jwtAdminId, result.Login.AdminId, StringComparison.Ordinal))
        {
            _logger.LogWarning("Admin login returned a malformed or expired JWT.");
            ModelState.AddModelError(string.Empty, Text["invalidSession"]);
            return Page();
        }

        var principal = AdminAuthConstants.CreatePrincipal(result.Login, tokenId);
        await HttpContext.SignInAsync(
            AdminAuthConstants.Scheme,
            principal,
            new AuthenticationProperties
            {
                AllowRefresh = false,
                ExpiresUtc = expiresUtc,
                IsPersistent = false,
                IssuedUtc = DateTimeOffset.UtcNow,
                RedirectUri = ReturnUrl
            });

        _logger.LogInformation("Admin {AdminId} signed in.", result.Login.AdminId);
        return LocalRedirect(ReturnUrl);
    }

    private async Task LoadLoginAppearanceStateAsync(CancellationToken cancellationToken)
    {
        var preset = "navy-gold";

        try
        {
            var response = await _httpClientFactory
                .CreateClient(AdminAuthConstants.ApiClientName)
                .GetFromJsonAsync<ApiResponse<AdminAppearanceModel>>(
                    "api/public/admin-appearance",
                    JsonOptions,
                    cancellationToken);

            if (response?.Success == true && !string.IsNullOrWhiteSpace(response.Data?.Preset))
                preset = response.Data.Preset;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to load public Admin appearance for the login page.");
        }

        LoginAppearanceCss = AdminAppearancePresets.ToLoginCssVariables(preset);
    }

    private async Task LoadLoginLanguageStateAsync(string? requestedLanguage, CancellationToken cancellationToken)
    {
        var options = DefaultLanguageOptions.ToList();
        var fallbackLanguage = "en";

        try
        {
            var response = await _httpClientFactory
                .CreateClient(AdminAuthConstants.ApiClientName)
                .GetFromJsonAsync<ApiResponse<SiteSettingsModel>>(
                    "api/public/admin-languages",
                    JsonOptions,
                    cancellationToken);

            if (response?.Success == true && response.Data?.Languages.Count > 0)
            {
                options = response.Data.Languages
                    .Where(l => l.Active && l.AdminEnabled && !string.IsNullOrWhiteSpace(l.Slug))
                    .OrderBy(l => l.Order)
                    .Select(l => new LoginLanguageOption(
                        NormalizeLanguageCode(l.Slug),
                        string.IsNullOrWhiteSpace(l.Name) ? l.Slug.Trim().ToUpperInvariant() : l.Name.Trim(),
                        string.IsNullOrWhiteSpace(l.NativeName)
                            ? (string.IsNullOrWhiteSpace(l.Name) ? l.Slug.Trim().ToUpperInvariant() : l.Name.Trim())
                            : l.NativeName.Trim(),
                        NormalizeDirection(l.Direction)))
                    .GroupBy(l => l.Code, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                fallbackLanguage = NormalizeLanguageCode(response.Data.DefaultLanguage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to load public Admin language options for the login page.");
        }

        if (options.Count == 0)
            options = DefaultLanguageOptions.ToList();

        if (!options.Any(l => string.Equals(l.Code, fallbackLanguage, StringComparison.OrdinalIgnoreCase)))
            fallbackLanguage = options.Any(l => l.Code == "en") ? "en" : options[0].Code;

        AdminUiLocalizer.SetFallbackLanguage(fallbackLanguage);
        LanguageOptions = options;
        FallbackLanguage = fallbackLanguage;
        CurrentLanguage = ResolveLanguage(requestedLanguage);
        LanguageCode = CurrentLanguage;
        Text = BuildLoginText(CurrentLanguage);
        LoginTranslationsJson = JsonSerializer.Serialize(BuildTranslations(), JsonOptions);
    }

    private Dictionary<string, Dictionary<string, string>> BuildTranslations()
    {
        var translations = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new Dictionary<string, string>(BuildLoginText("en"), StringComparer.OrdinalIgnoreCase)
        };

        foreach (var option in LanguageOptions)
            translations[option.Code] = new Dictionary<string, string>(
                BuildLoginText(option.Code),
                StringComparer.OrdinalIgnoreCase);

        if (!translations.ContainsKey(FallbackLanguage))
        {
            translations[FallbackLanguage] = new Dictionary<string, string>(
                BuildLoginText(FallbackLanguage),
                StringComparer.OrdinalIgnoreCase);
        }

        return translations;
    }

    private void ValidateInput()
    {
        ModelState.Clear();

        if (string.IsNullOrWhiteSpace(Input.Email))
        {
            ModelState.AddModelError("Input.Email", Text["emailRequired"]);
        }
        else if (!IsValidEmail(Input.Email))
        {
            ModelState.AddModelError("Input.Email", Text["emailInvalid"]);
        }

        if (string.IsNullOrWhiteSpace(Input.Password))
            ModelState.AddModelError("Input.Password", Text["passwordRequired"]);
    }

    private string LocalizeAuthenticationError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return Text["loginFailed"];

        if (error.Contains("Invalid email or password", StringComparison.OrdinalIgnoreCase))
            return Text["invalidCredentials"];
        if (error.Contains("Too many", StringComparison.OrdinalIgnoreCase))
            return Text["tooManyAttempts"];
        if (error.Contains("temporarily unavailable", StringComparison.OrdinalIgnoreCase))
            return Text["temporaryUnavailable"];
        if (error.Contains("Unable to sign in", StringComparison.OrdinalIgnoreCase))
            return Text["unableToSignIn"];
        if (error.Contains("invalid response", StringComparison.OrdinalIgnoreCase))
            return Text["invalidSession"];

        return Text["loginFailed"];
    }

    private string ResolveLanguage(string? requestedLanguage)
    {
        var normalized = NormalizeLanguageCode(requestedLanguage);
        var match = LanguageOptions.FirstOrDefault(l =>
            string.Equals(l.Code, normalized, StringComparison.OrdinalIgnoreCase));
        if (match is not null) return match.Code;

        var fallback = LanguageOptions.FirstOrDefault(l =>
            string.Equals(l.Code, FallbackLanguage, StringComparison.OrdinalIgnoreCase));
        return fallback?.Code ?? LanguageOptions[0].Code;
    }

    private string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Content("~/");

    private static IReadOnlyDictionary<string, string> BuildLoginText(string languageCode)
    {
        languageCode = AdminUiLocalizer.CompiledCatalogs.ContainsKey(languageCode) ? languageCode : "en";

        var text = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in LoginTextKeys)
            text[key] = AdminUiLocalizer.T(LoginTextCatalogKeys[key], languageCode);

        return text;
    }

    private static string NormalizeReason(string? reason) => reason switch
    {
        "signed-out" => "signedOut",
        "session-expired" => "sessionExpired",
        "password-changed" => "passwordChanged",
        "password-reset" => "passwordReset",
        "account-disabled" => "accountDisabled",
        "account-deleted" => "accountDeleted",
        "account-updated" => "accountUpdated",
        "access-denied" => "accessDenied",
        _ => string.Empty
    };

    private static bool IsValidEmail(string value)
    {
        try
        {
            var address = new MailAddress(value);
            return string.Equals(address.Address, value, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeLanguageCode(string? languageCode) =>
        string.IsNullOrWhiteSpace(languageCode)
            ? "en"
            : languageCode.Trim().ToLowerInvariant();

    private static string NormalizeDirection(string? direction) =>
        string.Equals(direction, "rtl", StringComparison.OrdinalIgnoreCase) ? "rtl" : "ltr";

    public sealed class LoginInput
    {
        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }

    public sealed record LoginLanguageOption(string Code, string Name, string NativeName, string Direction);
}
