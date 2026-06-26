using System.Globalization;
using System.Text.RegularExpressions;
using Contracts.Forms;
using FullProject.Models;
using FullProject.Security.Forms;

namespace FullProject.Services.FormServices;

public sealed class FormValidationService
{
    private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text", "email", "tel", "phone", "textarea", "select", "radio", "checkbox", "date", "number", "url"
    };
    private static readonly HashSet<string> ReservedFieldKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "PageUrl", "SourcePage", "Honeypot", "CaptchaToken"
    };
    private static readonly Regex FieldKeyRegex = new(
        "^[A-Za-z][A-Za-z0-9_-]{0,99}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex EmailRegex = new(
        "^[^\\s@]+@[^\\s@]+\\.[^\\s@]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex PhoneRegex = new(
        "^[0-9+().\\-\\s]{6,40}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public Dictionary<string, string> Validate(
        FormDefinition definition,
        IReadOnlyDictionary<string, string>? input,
        string language)
    {
        var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var data = input ?? new Dictionary<string, string>();
        var fields = definition.Fields
            .Where(field => !string.IsNullOrWhiteSpace(field.Key))
            .GroupBy(field => field.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var key in data.Keys)
        {
            if (!fields.ContainsKey(key))
                errors[key] = "This field is not accepted by the form.";
        }

        foreach (var field in fields.Values)
        {
            var value = data.GetValueOrDefault(field.Key)?.Trim() ?? string.Empty;
            var label = ResolveText(field.Label, language, field.Key);

            if (field.Required && string.IsNullOrWhiteSpace(value))
            {
                errors[field.Key] = $"{label} is required.";
                continue;
            }

            if (string.IsNullOrWhiteSpace(value)) continue;

            var minimum = Math.Clamp(field.MinLength, 0, 2_000);
            var maximum = Math.Clamp(field.MaxLength <= 0 ? DefaultMaximum(field.Type) : field.MaxLength, 1, 2_000);
            if (minimum > maximum) minimum = maximum;

            if (value.Length < minimum)
                errors[field.Key] = $"{label} must contain at least {minimum} characters.";
            else if (value.Length > maximum)
                errors[field.Key] = $"{label} must contain no more than {maximum} characters.";
            else if (!IsValidType(field.Type, value))
                errors[field.Key] = $"{label} has an invalid value.";
            else if (field.Options.Count > 0 && !field.Options.Any(option =>
                         string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase)))
                errors[field.Key] = $"{label} is not an available option.";
        }

        return errors;
    }

    internal IReadOnlyList<FormFieldValidationRule> BuildSecurityRules(FormDefinition definition) =>
        definition.Fields
            .Where(field => !string.IsNullOrWhiteSpace(field.Key) && SupportedTypes.Contains(field.Type))
            .OrderBy(field => field.Order)
            .Select(field => new FormFieldValidationRule(
                field.Key,
                field.Type,
                field.Required,
                Math.Clamp(field.MaxLength <= 0 ? DefaultMaximum(field.Type) : field.MaxLength, 1, 2_000),
                field.Options.Count == 0
                    ? null
                    : field.Options
                        .Where(option => !string.IsNullOrWhiteSpace(option.Value))
                        .Select(option => option.Value)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase)))
            .ToList();

    public List<string> ValidateDefinition(FormDefinition definition)
    {
        var errors = new List<string>();
        if (FormDefinitionService.NormalizeKey(definition.Key) is null)
            errors.Add("Form key is invalid.");
        if (!definition.Name.Values.Any(value => !string.IsNullOrWhiteSpace(value)))
            errors.Add("Form name is required.");
        if (definition.Fields.Count == 0)
            errors.Add("At least one form field is required.");
        if (definition.Fields.Any(field => !FieldKeyRegex.IsMatch(field.Key ?? string.Empty)))
            errors.Add("Every field must have a valid key.");
        if (definition.Fields.Any(field => ReservedFieldKeys.Contains(field.Key ?? string.Empty)))
            errors.Add("Form fields cannot use reserved metadata keys.");
        if (definition.Fields.GroupBy(field => field.Key, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            errors.Add("Form field keys must be unique.");
        if (definition.Fields.Any(field => !SupportedTypes.Contains(field.Type)))
            errors.Add("The form contains an unsupported field type.");
        return errors;
    }

    public List<string> ValidateDefinition(FormDefinitionUpsertRequest request)
    {
        var errors = new List<string>();
        var fields = request.Fields ?? new();

        if (FormDefinitionService.NormalizeKey(request.Key) is null)
            errors.Add("Enter a valid form key using lowercase letters, numbers, and hyphens.");
        if (request.Name?.Values.Any(value => !string.IsNullOrWhiteSpace(value)) != true)
            errors.Add("Form name is required.");
        if (request.SubmitButtonLabel?.Values.Any(value => !string.IsNullOrWhiteSpace(value)) != true)
            errors.Add("Submit button label is required.");
        if (fields.Count == 0)
            errors.Add("Add at least one form field.");
        if (fields.Any(field => !FieldKeyRegex.IsMatch(field.Key ?? string.Empty)))
            errors.Add("Every field must have a valid key.");
        if (fields.Any(field => ReservedFieldKeys.Contains(field.Key ?? string.Empty)))
            errors.Add("Form fields cannot use reserved metadata keys.");
        if (fields.GroupBy(field => field.Key, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            errors.Add("Form field keys must be unique.");
        if (fields.Any(field => !SupportedTypes.Contains(field.Type ?? string.Empty)))
            errors.Add("Every field must use a supported field type.");
        if (fields.Any(field => field.Label?.Values.Any(value => !string.IsNullOrWhiteSpace(value)) != true))
            errors.Add("Every field must have a label.");
        if (fields.Any(field => field.MaxLength is < 1 or > 2_000))
            errors.Add("Every field maximum length must be between 1 and 2000.");

        return errors;
    }

    public static string ResolveText(IReadOnlyDictionary<string, string> values, string language, string fallback) =>
        values.GetValueOrDefault(language) is { Length: > 0 } localized
            ? localized
            : values.GetValueOrDefault("en") is { Length: > 0 } english
                ? english
                : values.Values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? fallback;

    private static bool IsValidType(string? type, string value) => type?.Trim().ToLowerInvariant() switch
    {
        "email" => value.Length <= 254 && EmailRegex.IsMatch(value),
        "tel" or "phone" => PhoneRegex.IsMatch(value),
        "number" => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
        "date" => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
        "checkbox" => value is "true" or "false" or "on" or "1" or "0",
        "url" => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https",
        _ => true
    };

    private static int DefaultMaximum(string? type) => type?.ToLowerInvariant() switch
    {
        "email" => 254,
        "tel" or "phone" => 40,
        "textarea" => 2_000,
        _ => 500
    };

}
