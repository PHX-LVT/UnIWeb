using System.Text.RegularExpressions;
using Contracts.Forms;
using FullProject.Models;
using FullProject.Security.Forms;

namespace FullProject.Services.FormServices;

public sealed class FormValidationService
{
    private static readonly HashSet<string> ReservedFieldKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "PageUrl", "SourcePage", "Honeypot", "CaptchaToken"
    };
    private static readonly Regex FieldKeyRegex = new(
        "^[A-Za-z][A-Za-z0-9_-]{0,99}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly FormInputTypeService _inputTypes;

    public FormValidationService(FormInputTypeService inputTypes)
    {
        _inputTypes = inputTypes;
    }

    public async Task<Dictionary<string, string>> ValidateAsync(
        FormDefinition definition,
        IReadOnlyDictionary<string, string>? input,
        string language)
    {
        var capabilities = await _inputTypes.GetCapabilityLookupAsync();
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

            var capability = Capability(field.Type, capabilities);
            if (!string.IsNullOrWhiteSpace(value) && capability.SupportsMaxCharacters)
            {
                var maximum = FormInputTypeCatalog.MaximumInputLength(capability);
                var minimum = Math.Clamp(field.MinLength, 0, maximum);

                if (value.Length < minimum)
                {
                    errors[field.Key] = $"{label} must contain at least {minimum} characters.";
                    continue;
                }

                if (value.Length > maximum)
                {
                    errors[field.Key] = $"{label} must contain no more than {maximum} characters.";
                    continue;
                }
            }

            var validationError = FormInputValueValidator.Validate(
                field.Type,
                value,
                field.Required,
                capability.SupportsOptions
                    ? field.Options.Select(option => option.Value).ToList()
                    : null);
            if (validationError != FormInputValidationError.None)
                errors[field.Key] = ValidationMessage(validationError, label);
        }

        return errors;
    }

    internal async Task<IReadOnlyList<FormFieldValidationRule>> BuildSecurityRulesAsync(FormDefinition definition)
    {
        var capabilities = await _inputTypes.GetCapabilityLookupAsync();
        return definition.Fields
            .Where(field => !string.IsNullOrWhiteSpace(field.Key) && IsSupported(field.Type, capabilities))
            .OrderBy(field => field.Order)
            .Select(field => new FormFieldValidationRule(
                field.Key,
                FormInputTypeCatalog.NormalizeType(field.Type),
                field.Required,
                SecurityMaximum(field.Type, field.MaxLength, capabilities),
                !Capability(field.Type, capabilities).SupportsOptions || field.Options.Count == 0
                    ? null
                    : field.Options
                        .Where(option => !string.IsNullOrWhiteSpace(option.Value))
                        .Select(option => option.Value)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase)))
            .ToList();
    }

    public async Task<List<string>> ValidateDefinitionAsync(FormDefinition definition)
    {
        var capabilities = await _inputTypes.GetCapabilityLookupAsync();
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
        if (definition.Fields.Any(field => !IsSupported(field.Type, capabilities)))
            errors.Add("The form contains an unsupported field type.");
        AddCapabilityErrors(definition.Fields, errors, enforceMetadataShape: false, capabilities);
        return errors;
    }

    public async Task<List<string>> ValidateDefinitionAsync(
        FormDefinitionUpsertRequest request,
        IReadOnlySet<string>? permittedInactiveTypes = null)
    {
        var capabilities = await _inputTypes.GetCapabilityLookupAsync();
        var activeTypes = await _inputTypes.GetActiveTypeSetAsync();
        var errors = new List<string>();
        var fields = request.Fields ?? new();

        if (string.IsNullOrWhiteSpace(request.Key))
            errors.Add("Form Key is required.");
        else if (FormDefinitionService.NormalizeKey(request.Key) is null)
            errors.Add("Form Key must use lowercase letters, numbers, and hyphens.");
        if (request.Name?.Values.Any(value => !string.IsNullOrWhiteSpace(value)) != true)
            errors.Add("Form name is required.");
        if (request.SubmitButtonLabel?.Values.Any(value => !string.IsNullOrWhiteSpace(value)) != true)
            errors.Add("Submit button label is required.");
        if (fields.Count == 0)
            errors.Add("Add at least one form field.");
        foreach (var field in fields.OrderBy(field => field.Order))
        {
            var fieldLabel = FieldValidationLabel(field.Key, field.Label, field.Order);
            if (string.IsNullOrWhiteSpace(field.Key))
                errors.Add($"{fieldLabel}: Field Key is required.");
            else if (!FieldKeyRegex.IsMatch(field.Key))
                errors.Add($"{fieldLabel}: Field Key must start with a letter and contain only letters, numbers, hyphens, or underscores.");
        }
        if (fields.Any(field => ReservedFieldKeys.Contains(field.Key ?? string.Empty)))
            errors.Add("Form fields cannot use reserved metadata keys.");
        foreach (var duplicate in fields
            .Where(field => !string.IsNullOrWhiteSpace(field.Key))
            .GroupBy(field => field.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1))
        {
            errors.Add($"Field Key \"{duplicate.First().Key.Trim()}\" is used more than once in this form.");
        }
        if (fields.Any(field => !IsSupported(field.Type, capabilities)))
            errors.Add("Every field must use a supported field type.");
        if (fields.Any(field =>
                !activeTypes.Contains(FormInputTypeCatalog.NormalizeType(field.Type)) &&
                permittedInactiveTypes?.Contains(FormInputTypeCatalog.NormalizeType(field.Type)) != true))
            errors.Add("Inactive field types cannot be added to a form.");
        if (fields.Any(field => field.Label?.Values.Any(value => !string.IsNullOrWhiteSpace(value)) != true))
            errors.Add("Every field must have a label.");
        AddCapabilityErrors(fields, errors, enforceMetadataShape: true, capabilities);

        return errors;
    }

    public static string ResolveText(IReadOnlyDictionary<string, string> values, string language, string fallback) =>
        values.GetValueOrDefault(language) is { Length: > 0 } localized
            ? localized
            : values.GetValueOrDefault("en") is { Length: > 0 } english
                ? english
                : values.Values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? fallback;

    private static string FieldValidationLabel(
        string? key,
        IReadOnlyDictionary<string, string>? label,
        int order)
    {
        var text = label?.Values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? key;
        return string.IsNullOrWhiteSpace(text)
            ? $"Field {order + 1}"
            : $"Field {order + 1} \"{text}\"";
    }

    private static string ValidationMessage(FormInputValidationError error, string label) => error switch
    {
        FormInputValidationError.Required => $"{label} is required.",
        FormInputValidationError.Email => $"{label} must be a complete email address, such as name@company.com.",
        FormInputValidationError.Phone => $"{label} must contain a valid phone number with 6 to 15 digits.",
        FormInputValidationError.Number => $"{label} must be a valid number.",
        FormInputValidationError.Date => $"{label} must be a valid date.",
        FormInputValidationError.Url => $"{label} must be a complete http or https URL.",
        FormInputValidationError.Option => $"{label} is not an available option.",
        FormInputValidationError.Checkbox => $"{label} has an invalid checkbox value.",
        _ => $"{label} has an invalid value."
    };

    private static int SecurityMaximum(
        string? type,
        int maxLength,
        IReadOnlyDictionary<string, FormInputTypeCapability> capabilities)
    {
        var capability = Capability(type, capabilities);
        if (capability.SupportsMaxCharacters)
            return FormInputTypeCatalog.MaximumInputLength(capability);

        return FormInputTypeCatalog.NormalizeType(type) switch
        {
            "email" => 254,
            "tel" or "phone" => 40,
            "textarea" => 2_000,
            "select" => 500,
            "checkbox" => 10,
            "date" => 80,
            "number" => 80,
            "url" => 500,
            _ => 500
        };
    }

    private static void AddCapabilityErrors(
        IEnumerable<FormDefinitionField> fields,
        List<string> errors,
        bool enforceMetadataShape,
        IReadOnlyDictionary<string, FormInputTypeCapability> capabilities)
    {
        foreach (var field in fields)
            AddCapabilityErrors(field.Key, field.Type, field.Label, field.MaxLength, field.InputBoxSize, field.Options, errors, enforceMetadataShape, capabilities);
    }

    private static void AddCapabilityErrors(
        IEnumerable<FormFieldDefinitionDto> fields,
        List<string> errors,
        bool enforceMetadataShape,
        IReadOnlyDictionary<string, FormInputTypeCapability> capabilities)
    {
        foreach (var field in fields)
            AddCapabilityErrors(field.Key, field.Type, field.Label, field.MaxLength, field.InputBoxSize, field.Options, errors, enforceMetadataShape, capabilities);
    }

    private static void AddCapabilityErrors(
        string? key,
        string? type,
        IReadOnlyDictionary<string, string>? label,
        int maxLength,
        int inputBoxSize,
        IEnumerable<FormDefinitionFieldOption> options,
        List<string> errors,
        bool enforceMetadataShape,
        IReadOnlyDictionary<string, FormInputTypeCapability> capabilities) =>
        AddCapabilityErrors(
            key,
            type,
            label,
            maxLength,
            inputBoxSize,
            options.Select(option => (option.Value, Label: (IReadOnlyDictionary<string, string>?)option.Label)),
            errors,
            enforceMetadataShape,
            capabilities);

    private static void AddCapabilityErrors(
        string? key,
        string? type,
        IReadOnlyDictionary<string, string>? label,
        int maxLength,
        int inputBoxSize,
        IEnumerable<FormFieldOptionDto> options,
        List<string> errors,
        bool enforceMetadataShape,
        IReadOnlyDictionary<string, FormInputTypeCapability> capabilities) =>
        AddCapabilityErrors(
            key,
            type,
            label,
            maxLength,
            inputBoxSize,
            options.Select(option => (option.Value, Label: (IReadOnlyDictionary<string, string>?)option.Label)),
            errors,
            enforceMetadataShape,
            capabilities);

    private static void AddCapabilityErrors(
        string? key,
        string? type,
        IReadOnlyDictionary<string, string>? label,
        int maxLength,
        int inputBoxSize,
        IEnumerable<(string Value, IReadOnlyDictionary<string, string>? Label)> options,
        List<string> errors,
        bool enforceMetadataShape,
        IReadOnlyDictionary<string, FormInputTypeCapability> capabilities)
    {
        if (!IsSupported(type, capabilities))
            return;

        var fieldLabel = label?.Values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? key ?? "Field";
        var capability = Capability(type, capabilities);
        var optionList = options
            .Where(option => !string.IsNullOrWhiteSpace(option.Value) ||
                             option.Label?.Values.Any(value => !string.IsNullOrWhiteSpace(value)) == true)
            .ToList();

        if (capability.SupportsInputBoxSize)
        {
            if (enforceMetadataShape && inputBoxSize is < FormInputTypeCatalog.MinInputBoxSize or > FormInputTypeCatalog.MaxInputBoxSize)
                errors.Add($"{fieldLabel}: input box size must be between {FormInputTypeCatalog.MinInputBoxSize} and {FormInputTypeCatalog.MaxInputBoxSize}.");
        }
        else if (enforceMetadataShape && inputBoxSize > 1)
        {
            errors.Add($"{fieldLabel}: input box size is not supported for this field type.");
        }

        if (capability.SupportsOptions)
        {
            if (optionList.Count == 0)
            {
                errors.Add($"{fieldLabel}: add at least one option.");
                return;
            }

            if (optionList.Any(option => string.IsNullOrWhiteSpace(option.Value)))
                errors.Add($"{fieldLabel}: every option needs a value.");
            if (optionList.GroupBy(option => option.Value, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
                errors.Add($"{fieldLabel}: option values must be unique.");
        }
        else if (enforceMetadataShape && optionList.Count > 0)
        {
            errors.Add($"{fieldLabel}: options are not supported for this field type.");
        }
    }

    private static FormInputTypeCapability Capability(
        string? type,
        IReadOnlyDictionary<string, FormInputTypeCapability> capabilities) =>
        FormInputTypeService.Capability(type, capabilities);

    private static bool IsSupported(
        string? type,
        IReadOnlyDictionary<string, FormInputTypeCapability> capabilities) =>
        FormInputTypeService.IsSupported(type, capabilities);

}
