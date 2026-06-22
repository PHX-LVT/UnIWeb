namespace FullProject.Services.FormServices;

internal sealed record FormFieldValidationRule(
    string Key,
    string Type,
    bool Required,
    int MaximumLength,
    IReadOnlySet<string>? AllowedValues = null);
