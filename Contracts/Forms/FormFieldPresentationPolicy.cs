namespace Contracts.Forms;

public static class FormFieldPresentationPolicy
{
    public static string ResolvePlaceholder(
        FormLabelMode labelMode,
        string? configuredPlaceholder,
        string? fallbackLabel,
        bool required)
    {
        var placeholder = configuredPlaceholder?.Trim() ?? string.Empty;
        if (labelMode == FormLabelMode.InsideInputs && string.IsNullOrWhiteSpace(placeholder))
            placeholder = fallbackLabel?.Trim() ?? string.Empty;

        if (labelMode == FormLabelMode.InsideInputs &&
            required &&
            !string.IsNullOrWhiteSpace(placeholder) &&
            !placeholder.EndsWith('*'))
        {
            placeholder += " *";
        }

        return placeholder;
    }

    public static bool ShouldShowLabel(FormLabelMode labelMode, string? placeholder) =>
        labelMode == FormLabelMode.Visible || string.IsNullOrWhiteSpace(placeholder);
}
