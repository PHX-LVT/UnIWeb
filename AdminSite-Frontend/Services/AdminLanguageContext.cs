namespace AdminSite.Services;

public interface IAdminLanguageContext
{
    string CurrentLanguage { get; }
    void Set(string? language);
}

public sealed class AdminLanguageContext : IAdminLanguageContext
{
    public string CurrentLanguage { get; private set; } = "en";

    public void Set(string? language)
    {
        if (!string.IsNullOrWhiteSpace(language))
            CurrentLanguage = language.Trim().ToLowerInvariant();
    }
}
