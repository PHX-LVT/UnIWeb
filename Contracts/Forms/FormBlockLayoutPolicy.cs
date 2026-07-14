namespace Contracts.Forms;

public readonly record struct FormBlockSizingField(string Type, int InputBoxSize);

public readonly record struct FormBlockDefaultSize(
    int WidthPx,
    int WidthUnits,
    double WidthPercent,
    int HeightPx);

public static class FormBlockLayoutPolicy
{
    public const double MinimumScale = 0.5d;
    public const double MaximumScale = 1d;
    public const int StackedDefaultWidthPx = 680;
    public const int TwoColumnDefaultWidthPx = 960;
    public const int NormalContentWidthPx = 1400;
    public const int NarrowContentWidthPx = 720;
    public const int FullContentWidthPx = 1440;
    public const int MaximumHeightPx = 1800;
    public const int MaximumSectionHeightPx = 3000;
    public const int SectionBottomPaddingPx = 40;

    public static FormBlockDefaultSize CalculateDefaultSize(
        FormLayout layout,
        IEnumerable<FormBlockSizingField> fields,
        bool hasName,
        bool hasIntroduction,
        int availableWidthPx = NormalContentWidthPx)
    {
        var orderedFields = fields?.ToList() ?? new List<FormBlockSizingField>();
        var twoColumns = layout == FormLayout.TwoColumns;
        var preferredWidthPx = twoColumns ? TwoColumnDefaultWidthPx : StackedDefaultWidthPx;
        var safeAvailableWidthPx = Math.Max(1, availableWidthPx);
        var widthPx = Math.Min(preferredWidthPx, safeAvailableWidthPx);
        var widthPercent = Math.Clamp(widthPx / (double)safeAvailableWidthPx * 100d, 1d, 100d);
        var widthUnits = Math.Clamp((int)Math.Round(widthPercent / 100d * 12d), 1, 12);
        var heightPx = CalculateDefinitionHeight(
            orderedFields,
            twoColumns,
            hasName,
            hasIntroduction);

        return new FormBlockDefaultSize(
            widthPx,
            widthUnits,
            widthPercent,
            heightPx);
    }

    public static int AvailableContentWidthPx(string? contentWidth) =>
        contentWidth?.Trim().ToLowerInvariant() switch
        {
            "narrow" => NarrowContentWidthPx,
            "full" => FullContentWidthPx,
            _ => NormalContentWidthPx
        };

    private static int CalculateDefinitionHeight(
        IReadOnlyList<FormBlockSizingField> fields,
        bool twoColumns,
        bool hasName,
        bool hasIntroduction)
    {
        const int outerPadding = 72;
        const int buttonHeight = 44;
        const int blockGap = 16;
        var headerHeight = 0;

        if (hasName)
            headerHeight += 32;
        if (hasIntroduction)
            headerHeight += 44;
        if (headerHeight > 0)
            headerHeight += blockGap;

        var fieldsHeight = twoColumns
            ? CalculateTwoColumnFieldsHeight(fields)
            : CalculateStackedFieldsHeight(fields);

        var rawHeight = outerPadding + headerHeight + fieldsHeight + blockGap + buttonHeight;
        return Math.Clamp(rawHeight, 280, MaximumHeightPx);
    }

    private static int CalculateStackedFieldsHeight(IReadOnlyList<FormBlockSizingField> fields) =>
        fields.Count == 0
            ? 0
            : fields.Sum(FieldHeight) + Math.Max(0, fields.Count - 1) * 16;

    private static int CalculateTwoColumnFieldsHeight(IReadOnlyList<FormBlockSizingField> fields)
    {
        var total = 0;
        var rowHeights = new List<int>(2);

        void FlushRegularRow()
        {
            if (rowHeights.Count == 0)
                return;
            if (total > 0)
                total += 16;
            total += rowHeights.Max();
            rowHeights.Clear();
        }

        foreach (var field in fields)
        {
            var height = FieldHeight(field);
            if (IsWideField(field))
            {
                FlushRegularRow();
                if (total > 0)
                    total += 16;
                total += height;
                continue;
            }

            rowHeights.Add(height);
            if (rowHeights.Count == 2)
                FlushRegularRow();
        }

        FlushRegularRow();
        return total;
    }

    private static int FieldHeight(FormBlockSizingField field)
    {
        var type = FormInputTypeCatalog.NormalizeType(field.Type);
        if (type == "checkbox")
            return 48;
        if (type == "textarea")
        {
            var size = FormInputTypeCatalog.NormalizeInputBoxSize(type, field.InputBoxSize);
            var textareaHeight = size switch
            {
                1 => 86,
                2 => 118,
                3 => 150,
                4 => 182,
                _ => 214
            };
            return textareaHeight + 28;
        }

        return 68;
    }

    private static bool IsWideField(FormBlockSizingField field)
    {
        var type = FormInputTypeCatalog.NormalizeType(field.Type);
        return type is "textarea" or "checkbox";
    }
}
