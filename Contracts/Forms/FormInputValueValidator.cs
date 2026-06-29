using System.Globalization;
using System.Text.RegularExpressions;

namespace Contracts.Forms;

public enum FormInputValidationError
{
    None,
    Required,
    Email,
    Phone,
    Number,
    Date,
    Url,
    Option,
    Checkbox
}

public static class FormInputValueValidator
{
    private static readonly Regex EmailPattern = new(
        "^[A-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[A-Z0-9](?:[A-Z0-9-]{0,61}[A-Z0-9])?(?:\\.[A-Z0-9](?:[A-Z0-9-]{0,61}[A-Z0-9])?)+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex PhoneCharacters = new(
        "^\\+?[0-9().\\-\\s]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static FormInputValidationError Validate(
        string? type,
        string? value,
        bool required,
        IReadOnlyCollection<string>? allowedValues = null)
    {
        var normalizedType = FormInputTypeCatalog.NormalizeType(type);
        var trimmed = value?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(trimmed))
            return required ? FormInputValidationError.Required : FormInputValidationError.None;

        return normalizedType switch
        {
            "email" => IsValidEmail(trimmed) ? FormInputValidationError.None : FormInputValidationError.Email,
            "tel" => IsValidPhone(trimmed) ? FormInputValidationError.None : FormInputValidationError.Phone,
            "number" => decimal.TryParse(
                trimmed,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out _)
                    ? FormInputValidationError.None
                    : FormInputValidationError.Number,
            "date" => DateOnly.TryParseExact(
                trimmed,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _)
                    ? FormInputValidationError.None
                    : FormInputValidationError.Date,
            "url" => IsValidHttpUrl(trimmed) ? FormInputValidationError.None : FormInputValidationError.Url,
            "select" => allowedValues is null || allowedValues.Contains(trimmed, StringComparer.OrdinalIgnoreCase)
                ? FormInputValidationError.None
                : FormInputValidationError.Option,
            "checkbox" => ValidateCheckbox(trimmed, required),
            _ => FormInputValidationError.None
        };
    }

    private static bool IsValidEmail(string value)
    {
        if (value.Length > 254 || value.Contains(' ') || !EmailPattern.IsMatch(value))
            return false;

        var at = value.LastIndexOf('@');
        if (at is <= 0 or > 64 || value[..at].StartsWith('.') || value[..at].EndsWith('.') || value[..at].Contains(".."))
            return false;

        return true;
    }

    private static bool IsValidPhone(string value)
    {
        if (value.Length > 40 || !PhoneCharacters.IsMatch(value))
            return false;

        var digits = value.Count(char.IsDigit);
        return digits is >= 6 and <= 15;
    }

    private static bool IsValidHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https" &&
        !string.IsNullOrWhiteSpace(uri.Host);

    private static FormInputValidationError ValidateCheckbox(string value, bool required)
    {
        var normalized = value.ToLowerInvariant();
        if (normalized is not ("true" or "false" or "on" or "1" or "0"))
            return FormInputValidationError.Checkbox;

        return required && normalized is ("false" or "0")
            ? FormInputValidationError.Required
            : FormInputValidationError.None;
    }
}
