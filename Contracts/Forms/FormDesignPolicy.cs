namespace Contracts.Forms;

public static class FormDesignPolicy
{
    public const int CurrentSchemaVersion = 1;
    public const int MinimumHeightPx = 220;
    public const int MaximumHeightPx = 1800;
    public const int StackedDefaultWidthPx = 560;
    public const int TwoColumnDefaultWidthPx = 840;
    public const int CtaDefaultWidthPx = 760;
    public const int MinimumPaddingPx = 16;
    public const int MaximumPaddingPx = 64;
    public const int MinimumGapPx = 1;
    public const int MaximumGapPx = 32;
    public const int ReservedErrorHeightPx = 0;
    public const int ReservedStatusHeightPx = 0;

    public static FormDesignSettingsDto CreateDefault(FormDesignShape shape = FormDesignShape.Stacked)
    {
        return Normalize(new FormDesignSettingsDto
        {
            UseThemeDefaults = true,
            Shape = shape,
            WidthPx = shape == FormDesignShape.TwoColumns
                ? TwoColumnDefaultWidthPx
                : shape == FormDesignShape.Cta
                    ? CtaDefaultWidthPx
                : StackedDefaultWidthPx,
            LabelMode = FormLabelMode.Visible,
            ButtonWidth = FormDesignButtonWidth.Full
        });
    }

    public static FormDesignSettingsDto Normalize(
        FormDesignSettingsDto? design,
        IEnumerable<FormFieldDefinitionDto>? fields = null,
        IReadOnlyDictionary<string, string>? name = null,
        IReadOnlyDictionary<string, string>? introduction = null,
        IReadOnlyDictionary<string, string>? submitLabel = null)
    {
        design ??= new FormDesignSettingsDto();
        var width = Math.Clamp(design.WidthPx, MinimumWidth(design.Shape), MaximumWidth(design.Shape));
        var padding = Math.Clamp(design.PaddingPx, MinimumPaddingPx, MaximumPaddingPx);
        var gap = Math.Clamp(design.FieldGapPx, MinimumGapPx, MaximumGapPx);
        var labelMode = design.Shape == FormDesignShape.Cta && design.LabelMode == FormLabelMode.Visible
            ? FormLabelMode.Visible
            : design.LabelMode;

        var normalized = new FormDesignSettingsDto
        {
            UseThemeDefaults = design.UseThemeDefaults,
            SchemaVersion = CurrentSchemaVersion,
            Shape = design.Shape,
            WidthPx = width,
            BackgroundMode = design.BackgroundMode,
            BackgroundColor = NormalizeColor(design.BackgroundColor, "#ffffff"),
            TextColor = NormalizeColor(design.TextColor, "#0f172a"),
            AccentColor = NormalizeColor(design.AccentColor, "#1d4ed8"),
            BorderColor = NormalizeColor(design.BorderColor, "#dbe3ef"),
            BorderWidthPx = Math.Clamp(design.BorderWidthPx, 0, 4),
            BorderRadiusPx = Math.Clamp(design.BorderRadiusPx, 0, 40),
            Shadow = NormalizeChoice(design.Shadow, "none", "small", "medium", "large"),
            PaddingPx = padding,
            FieldGapPx = gap,
            TextAlign = design.TextAlign,
            LabelMode = labelMode,
            ButtonStyle = NormalizeChoice(design.ButtonStyle, "filled", "outline", "ghost"),
            ButtonWidth = design.ButtonWidth
        };

        normalized.CalculatedHeightPx = CalculateHeight(
            normalized,
            fields ?? Array.Empty<FormFieldDefinitionDto>(),
            name,
            introduction,
            submitLabel);
        return normalized;
    }

    public static int MinimumWidth(FormDesignShape shape) => shape switch
    {
        FormDesignShape.TwoColumns => 640,
        FormDesignShape.Cta => 680,
        _ => 320
    };

    public static int MaximumWidth(FormDesignShape shape) => shape switch
    {
        FormDesignShape.TwoColumns => 1100,
        FormDesignShape.Cta => 1200,
        _ => 720
    };

    public static int DefaultWidth(FormDesignShape shape) => shape switch
    {
        FormDesignShape.TwoColumns => TwoColumnDefaultWidthPx,
        FormDesignShape.Cta => CtaDefaultWidthPx,
        _ => StackedDefaultWidthPx
    };

    public static int CalculateHeight(
        FormDesignSettingsDto design,
        IEnumerable<FormFieldDefinitionDto> fields,
        IReadOnlyDictionary<string, string>? name = null,
        IReadOnlyDictionary<string, string>? introduction = null,
        IReadOnlyDictionary<string, string>? submitLabel = null)
        => Math.Clamp(
            CalculateRequiredHeight(design, fields, name, introduction, submitLabel),
            MinimumHeightPx,
            MaximumHeightPx);

    public static int CalculateRequiredHeight(
        FormDesignSettingsDto design,
        IEnumerable<FormFieldDefinitionDto> fields,
        IReadOnlyDictionary<string, string>? name = null,
        IReadOnlyDictionary<string, string>? introduction = null,
        IReadOnlyDictionary<string, string>? submitLabel = null)
    {
        var ordered = fields.OrderBy(field => field.Order).ToList();
        var contentWidth = Math.Max(180, design.WidthPx - design.PaddingPx * 2);
        var nameHeight = MaxTextHeight(name, contentWidth, 28, 34);
        var introductionHeight = MaxTextHeight(introduction, contentWidth, 15, 22);
        var headerHeight = nameHeight + introductionHeight;
        if (nameHeight > 0 && introductionHeight > 0)
            headerHeight += 8;
        if (headerHeight > 0)
            headerHeight += design.FieldGapPx;

        var fieldHeights = ordered.Select(field => FieldHeight(field, design.LabelMode)).ToList();
        var fieldsHeight = design.Shape switch
        {
            FormDesignShape.TwoColumns => PackedRowsHeight(ordered, fieldHeights, 2, design.FieldGapPx),
            _ => fieldHeights.Sum() + Math.Max(0, fieldHeights.Count - 1) * design.FieldGapPx
        };

        var submitHeight = Math.Max(46, MaxTextHeight(submitLabel, contentWidth, 15, 20) + 24);
        var bodyHeight = fieldsHeight + (ordered.Count > 0 ? design.FieldGapPx : 0) + submitHeight;
        var raw = design.PaddingPx * 2 + headerHeight + bodyHeight + ReservedStatusHeightPx;
        return Math.Max(MinimumHeightPx, raw);
    }

    public static bool IsWideField(string? type) =>
        FormInputTypeCatalog.NormalizeType(type) is "textarea" or "checkbox";

    private static int FieldHeight(FormFieldDefinitionDto field, FormLabelMode labelMode)
    {
        var type = FormInputTypeCatalog.NormalizeType(field.Type);
        if (type == "checkbox")
            return 44 + ReservedErrorHeightPx;

        var labelHeight = labelMode == FormLabelMode.Visible ? 22 + 6 : 0;
        var inputHeight = type == "textarea"
            ? FormInputTypeCatalog.NormalizeInputBoxSize(type, field.InputBoxSize) switch
            {
                1 => 86,
                2 => 118,
                3 => 150,
                4 => 182,
                _ => 214
            }
            : 46;
        return labelHeight + inputHeight + ReservedErrorHeightPx;
    }

    private static int PackedRowsHeight(
        IReadOnlyList<FormFieldDefinitionDto> fields,
        IReadOnlyList<int> heights,
        int columns,
        int gap)
    {
        if (fields.Count == 0) return 0;
        var rows = new List<int>();
        var current = new List<int>();

        void Flush()
        {
            if (current.Count == 0) return;
            rows.Add(current.Max());
            current.Clear();
        }

        for (var index = 0; index < fields.Count; index++)
        {
            if (IsWideField(fields[index].Type))
            {
                Flush();
                rows.Add(heights[index]);
                continue;
            }

            current.Add(heights[index]);
            if (current.Count == columns)
                Flush();
        }
        Flush();
        return rows.Sum() + Math.Max(0, rows.Count - 1) * gap;
    }

    private static int MaxTextHeight(
        IReadOnlyDictionary<string, string>? values,
        int width,
        int fontSize,
        int lineHeight)
    {
        if (values is null || values.Count == 0) return 0;
        var max = 0;
        var charactersPerLine = Math.Max(10, (int)Math.Floor(width / Math.Max(1d, fontSize * 0.56d)));
        foreach (var value in values.Values.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            var explicitLines = value.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');
            var lines = explicitLines.Sum(line => Math.Max(1, (int)Math.Ceiling(line.Length / (double)charactersPerLine)));
            max = Math.Max(max, lines * lineHeight);
        }
        return max;
    }

    private static string NormalizeChoice(string? value, params string[] allowed) =>
        allowed.FirstOrDefault(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase)) ?? allowed[0];

    private static string NormalizeColor(string? value, string fallback)
    {
        var color = value?.Trim();
        if (string.IsNullOrWhiteSpace(color)) return fallback;
        if (color.StartsWith('#') && color.Length is 4 or 7 or 9 && color.Skip(1).All(Uri.IsHexDigit))
            return color.ToLowerInvariant();
        return fallback;
    }
}
