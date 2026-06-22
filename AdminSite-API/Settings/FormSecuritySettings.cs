namespace FullProject.Settings;

public sealed class FormSecuritySettings
{
    public int MaximumFieldCount { get; set; } = 50;
    public int MaximumPayloadCharacters { get; set; } = 20_000;
    public int CooldownSeconds { get; set; } = 3;
    public int DuplicateWindowMinutes { get; set; } = 10;
    public string CaptchaMode { get; set; } = "Off";
}
