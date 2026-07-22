using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

namespace Contracts.Forms;

public static class FormDesignV2Policy
{
    public const int TargetSchemaVersion = 2;
    public const int MaximumFieldsPerRow = 3;
    public const int MaximumAuxiliaryActions = 3;
    public const int MinimumFieldGapPx = 1;
    public const int MaximumFieldGapPx = 16;
    public const int DefaultFieldGapPx = 8;
    public const int StructuralGapPx = 16;
    public const int InformationItemGapPx = 12;
    public const int MinimumSplitPanelPercent = 30;
    public const int MaximumSplitPanelPercent = 55;
    public const int DefaultSplitPanelPercent = 40;
    public const int StandardMinimumWidthPx = 320;
    public const int StandardDefaultWidthPx = 560;
    public const int StandardMaximumWidthPx = 720;
    public const int SplitPanelMinimumWidthPx = 720;
    public const int SplitPanelDefaultWidthPx = 840;
    public const int SplitPanelMaximumWidthPx = 1200;
    public const int CtaMinimumWidthPx = 680;
    public const int CtaDefaultWidthPx = 760;
    public const int CtaMaximumWidthPx = 1200;

    private const string RowIdentityNamespace = "form-design-v2-row";
    private static readonly HashSet<string> InformationIconKeys = new(StringComparer.Ordinal)
    {
        "fas fa-circle-info",
        "fas fa-phone",
        "fas fa-envelope",
        "fas fa-location-dot",
        "fas fa-clock",
        "fas fa-link"
    };

    public static FormDesignV2SettingsDto CreateDefault(
        string? definitionId,
        FormDesignSettingsDto? design,
        IEnumerable<FormFieldDefinitionDto>? fields)
    {
        design ??= FormDesignPolicy.CreateDefault();
        var orderedFields = (fields ?? Array.Empty<FormFieldDefinitionDto>())
            .OrderBy(field => field.Order)
            .ToList();
        var outerLayout = design.Shape switch
        {
            FormDesignShape.TwoColumns => FormOuterLayout.SplitPanel,
            FormDesignShape.Cta => FormOuterLayout.Cta,
            _ => FormOuterLayout.Standard
        };
        var rows = PackInitialRows(definitionId, design.Shape, orderedFields);

        return Normalize(new FormDesignV2SettingsDto
        {
            OuterLayout = outerLayout,
            FieldRows = rows,
            InformationBackgroundColor = design.Shape == FormDesignShape.TwoColumns
                ? design.BackgroundColor
                : "#0f2740",
            InformationTextColor = design.Shape == FormDesignShape.TwoColumns
                ? design.TextColor
                : "#ffffff",
            FormBackgroundColor = design.BackgroundColor,
            FormTextColor = design.TextColor,
            SplitPanelPercent = DefaultSplitPanelPercent,
            SubmitLayout = design.ButtonWidth == FormDesignButtonWidth.Full
                ? FormSubmitLayout.Full
                : design.TextAlign == FormDesignTextAlign.Center
                    ? FormSubmitLayout.Center
                    : FormSubmitLayout.Left
        });
    }

    public static FormDesignV2SettingsDto Normalize(FormDesignV2SettingsDto? design)
    {
        design ??= new FormDesignV2SettingsDto();
        return new FormDesignV2SettingsDto
        {
            OuterLayout = Enum.IsDefined(design.OuterLayout) ? design.OuterLayout : FormOuterLayout.Standard,
            FieldRows = (design.FieldRows ?? new())
                .OrderBy(row => row.Order)
                .Select((row, index) => new FormFieldRowDto
                {
                    Id = NormalizeOpaqueId(row.Id),
                    Order = index,
                    FieldKeys = (row.FieldKeys ?? new())
                        .Select(NormalizeFieldKey)
                        .Where(key => key.Length > 0)
                        .ToList()
                })
                .ToList(),
            InformationBackgroundColor = NormalizeColor(design.InformationBackgroundColor, "#0f2740"),
            InformationTextColor = NormalizeColor(design.InformationTextColor, "#ffffff"),
            FormBackgroundColor = NormalizeColor(design.FormBackgroundColor, "#ffffff"),
            FormTextColor = NormalizeColor(design.FormTextColor, "#0f172a"),
            SplitPanelPercent = Math.Clamp(
                design.SplitPanelPercent,
                MinimumSplitPanelPercent,
                MaximumSplitPanelPercent),
            SubmitLayout = Enum.IsDefined(design.SubmitLayout) ? design.SubmitLayout : FormSubmitLayout.Full,
            AuxiliaryActionLayouts = (design.AuxiliaryActionLayouts ?? new())
                .Select(layout => new FormAuxiliaryActionLayoutDto
                {
                    ActionId = NormalizeOpaqueId(layout.ActionId),
                    Style = Enum.IsDefined(layout.Style) ? layout.Style : FormAuxiliaryActionStyle.Theme,
                    Placement = Enum.IsDefined(layout.Placement)
                        ? layout.Placement
                        : FormAuxiliaryActionPlacement.BelowFields
                })
                .ToList()
        };
    }

    public static IReadOnlyList<string> Validate(
        FormDesignV2SettingsDto? design,
        IEnumerable<FormFieldDefinitionDto>? fields,
        IEnumerable<FormInformationItemDto>? informationItems = null,
        IEnumerable<FormAuxiliaryActionDto>? auxiliaryActions = null)
    {
        var errors = new List<string>();
        if (design is null)
        {
            errors.Add("Form Design v2 settings are required.");
            return errors;
        }

        var rows = design.FieldRows ?? new();
        var fieldList = (fields ?? Array.Empty<FormFieldDefinitionDto>()).ToList();
        var normalizedFieldKeys = fieldList
            .Select(field => NormalizeFieldKey(field.Key))
            .Where(key => key.Length > 0)
            .ToList();
        var fieldKeys = normalizedFieldKeys
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rowKeys = rows
            .SelectMany(row => row.FieldKeys ?? new())
            .Select(NormalizeFieldKey)
            .Where(key => key.Length > 0)
            .ToList();

        if (!Enum.IsDefined(design.OuterLayout))
            errors.Add("Choose a supported outer Form layout.");
        if (normalizedFieldKeys.GroupBy(key => key, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            errors.Add("Persisted field keys must be unique.");
        if (!Enum.IsDefined(design.SubmitLayout))
            errors.Add("Choose a supported Submit layout.");
        if (design.SplitPanelPercent is < MinimumSplitPanelPercent or > MaximumSplitPanelPercent)
            errors.Add($"Split Panel percentage must be between {MinimumSplitPanelPercent} and {MaximumSplitPanelPercent}.");
        if (rows.Any(row => string.IsNullOrWhiteSpace(row.Id)))
            errors.Add("Every field row must have a stable ID.");
        if (rows.GroupBy(row => row.Id, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            errors.Add("Field row IDs must be unique.");
        if (!HasSequentialOrders(rows.Select(row => row.Order)))
            errors.Add("Field row orders must be unique and sequential.");
        if (rows.Any(row => row.FieldKeys is null || row.FieldKeys.Count is < 1 or > MaximumFieldsPerRow))
            errors.Add($"Every field row must contain between one and {MaximumFieldsPerRow} fields.");
        if (rowKeys.Any(key => !fieldKeys.Contains(key)))
            errors.Add("Field rows contain an unknown field key.");
        if (rowKeys.GroupBy(key => key, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            errors.Add("Every field may appear in only one field row.");
        if (!fieldKeys.SetEquals(rowKeys))
            errors.Add("Every persisted field must appear exactly once across field rows.");

        var fieldTypes = fieldList
            .Where(field => NormalizeFieldKey(field.Key).Length > 0)
            .GroupBy(field => NormalizeFieldKey(field.Key), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().Type,
                StringComparer.OrdinalIgnoreCase);
        if (rows.Any(row => row.FieldKeys is { Count: > 1 } keys && keys.Any(key =>
                fieldTypes.TryGetValue(NormalizeFieldKey(key), out var type) && FormDesignPolicy.IsWideField(type))))
        {
            errors.Add("Textarea and checkbox fields must occupy their own row.");
        }

        ValidateInformationItems(informationItems ?? Array.Empty<FormInformationItemDto>(), errors);
        ValidateAuxiliaryActions(design, auxiliaryActions ?? Array.Empty<FormAuxiliaryActionDto>(), errors);
        return errors;
    }

    public static int MinimumWidth(FormOuterLayout layout) => layout switch
    {
        FormOuterLayout.SplitPanel => SplitPanelMinimumWidthPx,
        FormOuterLayout.Cta => CtaMinimumWidthPx,
        _ => StandardMinimumWidthPx
    };

    public static int DefaultWidth(FormOuterLayout layout) => layout switch
    {
        FormOuterLayout.SplitPanel => SplitPanelDefaultWidthPx,
        FormOuterLayout.Cta => CtaDefaultWidthPx,
        _ => StandardDefaultWidthPx
    };

    public static int MaximumWidth(FormOuterLayout layout) => layout switch
    {
        FormOuterLayout.SplitPanel => SplitPanelMaximumWidthPx,
        FormOuterLayout.Cta => CtaMaximumWidthPx,
        _ => StandardMaximumWidthPx
    };

    public static FormDesignShape ShapeForOuterLayout(FormOuterLayout layout) => layout switch
    {
        FormOuterLayout.SplitPanel => FormDesignShape.TwoColumns,
        FormOuterLayout.Cta => FormDesignShape.Cta,
        _ => FormDesignShape.Stacked
    };

    public static bool IsGovernedInformationIcon(string? icon) =>
        !string.IsNullOrWhiteSpace(icon) && InformationIconKeys.Contains(icon.Trim());

    public static bool AreEquivalent(FormDesignV2SettingsDto? left, FormDesignV2SettingsDto? right)
    {
        if (left is null || right is null) return left is null && right is null;
        var first = Normalize(left);
        var second = Normalize(right);
        return first.OuterLayout == second.OuterLayout &&
               first.InformationBackgroundColor == second.InformationBackgroundColor &&
               first.InformationTextColor == second.InformationTextColor &&
               first.FormBackgroundColor == second.FormBackgroundColor &&
               first.FormTextColor == second.FormTextColor &&
               first.SplitPanelPercent == second.SplitPanelPercent &&
               first.SubmitLayout == second.SubmitLayout &&
               first.FieldRows.Count == second.FieldRows.Count &&
               first.FieldRows.Zip(second.FieldRows).All(pair =>
                   pair.First.Id == pair.Second.Id &&
                   pair.First.Order == pair.Second.Order &&
                   pair.First.FieldKeys.SequenceEqual(pair.Second.FieldKeys, StringComparer.OrdinalIgnoreCase)) &&
               first.AuxiliaryActionLayouts.Count == second.AuxiliaryActionLayouts.Count &&
               first.AuxiliaryActionLayouts.Zip(second.AuxiliaryActionLayouts).All(pair =>
                   pair.First.ActionId == pair.Second.ActionId &&
                   pair.First.Style == pair.Second.Style &&
                   pair.First.Placement == pair.Second.Placement);
    }

    public static int CalculateHeight(
        FormDesignSettingsDto baseDesign,
        FormDesignV2SettingsDto design,
        IEnumerable<FormFieldDefinitionDto>? fields,
        IReadOnlyDictionary<string, string>? name = null,
        IReadOnlyDictionary<string, string>? introduction = null,
        IReadOnlyDictionary<string, string>? submitLabel = null,
        IEnumerable<FormInformationItemDto>? informationItems = null,
        IEnumerable<FormAuxiliaryActionDto>? auxiliaryActions = null) =>
        Math.Clamp(
            CalculateRequiredHeight(
                baseDesign,
                design,
                fields,
                name,
                introduction,
                submitLabel,
                informationItems,
                auxiliaryActions),
            FormDesignPolicy.MinimumHeightPx,
            FormDesignPolicy.MaximumHeightPx);

    public static int CalculateRequiredHeight(
        FormDesignSettingsDto baseDesign,
        FormDesignV2SettingsDto design,
        IEnumerable<FormFieldDefinitionDto>? fields,
        IReadOnlyDictionary<string, string>? name = null,
        IReadOnlyDictionary<string, string>? introduction = null,
        IReadOnlyDictionary<string, string>? submitLabel = null,
        IEnumerable<FormInformationItemDto>? informationItems = null,
        IEnumerable<FormAuxiliaryActionDto>? auxiliaryActions = null)
    {
        var normalized = Normalize(design);
        var fieldLookup = (fields ?? Array.Empty<FormFieldDefinitionDto>())
            .Where(field => NormalizeFieldKey(field.Key).Length > 0)
            .GroupBy(field => NormalizeFieldKey(field.Key), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var width = Math.Clamp(
            baseDesign.WidthPx,
            MinimumWidth(normalized.OuterLayout),
            MaximumWidth(normalized.OuterLayout));
        var padding = Math.Clamp(baseDesign.PaddingPx, FormDesignPolicy.MinimumPaddingPx, FormDesignPolicy.MaximumPaddingPx);
        var gap = Math.Clamp(baseDesign.FieldGapPx, MinimumFieldGapPx, MaximumFieldGapPx);
        var formWidth = normalized.OuterLayout == FormOuterLayout.SplitPanel
            ? Math.Max(220, (int)Math.Round(width * (100 - normalized.SplitPanelPercent) / 100d) - padding * 2)
            : Math.Max(180, width - padding * 2);

        var rowHeights = normalized.FieldRows.Select(row =>
            row.FieldKeys
                .Where(fieldLookup.ContainsKey)
                .Select(key => FieldHeight(fieldLookup[key], baseDesign.LabelMode))
                .DefaultIfEmpty(0)
                .Max()).ToList();
        var fieldsHeight = rowHeights.Sum() + Math.Max(0, rowHeights.Count - 1) * gap;
        var submitHeight = Math.Max(46, MaxTextHeight(submitLabel, formWidth, 15, 20) + 24);
        var actionList = (auxiliaryActions ?? Array.Empty<FormAuxiliaryActionDto>()).ToList();
        var actionLayouts = normalized.AuxiliaryActionLayouts
            .GroupBy(layout => layout.ActionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var informationActionCount = normalized.OuterLayout == FormOuterLayout.SplitPanel
            ? actionList.Count(action => actionLayouts.TryGetValue(action.Id, out var layout) &&
                                         layout.Placement == FormAuxiliaryActionPlacement.InformationPanel)
            : 0;
        var belowActionCount = actionList.Count - informationActionCount;
        var formSide = padding * 2 + fieldsHeight + (rowHeights.Count > 0 ? StructuralGapPx : 0) + submitHeight +
                       (belowActionCount > 0 ? StructuralGapPx + 46 : 0) +
                       FormDesignPolicy.ReservedStatusHeightPx;

        var headerWidth = normalized.OuterLayout == FormOuterLayout.SplitPanel
            ? Math.Max(180, (int)Math.Round(width * normalized.SplitPanelPercent / 100d) - padding * 2)
            : formWidth;
        var informationCount = normalized.OuterLayout == FormOuterLayout.SplitPanel
            ? (informationItems ?? Array.Empty<FormInformationItemDto>()).Count()
            : 0;
        var nameHeight = MaxTextHeight(name, headerWidth, 28, 34);
        var introductionHeight = MaxTextHeight(introduction, headerWidth, 15, 22);
        var header = nameHeight + introductionHeight +
                     (nameHeight > 0 && introductionHeight > 0 ? 8 : 0) +
                     Math.Max(0, informationCount) * 38 +
                     Math.Max(0, informationCount - 1) * InformationItemGapPx +
                     (informationActionCount > 0 ? StructuralGapPx + 46 : 0);
        if (header > 0) header += padding * 2 + StructuralGapPx;

        var required = normalized.OuterLayout switch
        {
            FormOuterLayout.SplitPanel => Math.Max(header, formSide),
            FormOuterLayout.Cta => formSide + Math.Max(0, header - padding * 2),
            _ => formSide + Math.Max(0, header - padding * 2)
        };
        return Math.Max(required, FormDesignPolicy.MinimumHeightPx);
    }

    public static string DeterministicRowId(string? definitionId, IEnumerable<string> orderedFieldKeys)
    {
        var identity = string.Join('|', orderedFieldKeys.Select(NormalizeFieldKey));
        var material = $"{RowIdentityNamespace}|{definitionId?.Trim().ToLowerInvariant()}|{identity}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return $"row-{Convert.ToHexString(hash[..10]).ToLowerInvariant()}";
    }

    public static string? ResolveHref(
        FormActionTargetDto? target,
        IReadOnlyDictionary<string, string>? managedResourceUrls = null)
    {
        if (target is null) return null;
        if (!string.IsNullOrWhiteSpace(target.ResolvedHref) && IsSafeHttpOrRelative(target.ResolvedHref))
            return target.ResolvedHref.Trim();
        return target.Type switch
        {
            FormActionTargetType.InternalLink when IsSafeInternalTarget(target) =>
                !string.IsNullOrWhiteSpace(target.Path) ? target.Path!.Trim() : $"/page/{target.PageId!.Trim()}",
            FormActionTargetType.ExternalUrl when IsSafeExternalUrl(target.Url) => target.Url!.Trim(),
            FormActionTargetType.ManagedResource when !string.IsNullOrWhiteSpace(target.ResourceId) &&
                                                        managedResourceUrls?.TryGetValue(target.ResourceId, out var url) == true &&
                                                        IsSafeHttpOrRelative(url) => url,
            FormActionTargetType.Phone when IsSafePhone(target.Phone) => $"tel:{NormalizePhone(target.Phone!)}",
            FormActionTargetType.Email when IsSafeEmail(target.Email) => $"mailto:{target.Email!.Trim()}",
            _ => null
        };
    }

    private static List<FormFieldRowDto> PackInitialRows(
        string? definitionId,
        FormDesignShape shape,
        IReadOnlyList<FormFieldDefinitionDto> fields)
    {
        var capacity = shape switch
        {
            FormDesignShape.TwoColumns => 2,
            _ => 1
        };
        var groups = new List<List<string>>();
        var current = new List<string>();

        void Flush()
        {
            if (current.Count == 0) return;
            groups.Add(current);
            current = new List<string>();
        }

        foreach (var field in fields)
        {
            var key = NormalizeFieldKey(field.Key);
            if (key.Length == 0) continue;
            if (FormDesignPolicy.IsWideField(field.Type))
            {
                Flush();
                groups.Add(new List<string> { key });
                continue;
            }

            current.Add(key);
            if (current.Count == capacity) Flush();
        }
        Flush();

        return groups.Select((keys, index) => new FormFieldRowDto
        {
            Id = DeterministicRowId(definitionId, keys),
            Order = index,
            FieldKeys = keys
        }).ToList();
    }

    private static void ValidateInformationItems(IEnumerable<FormInformationItemDto> source, ICollection<string> errors)
    {
        var items = source.ToList();
        if (items.Any(item => string.IsNullOrWhiteSpace(item.Id)))
            errors.Add("Every information item must have a stable ID.");
        if (items.GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            errors.Add("Information item IDs must be unique.");
        if (!HasSequentialOrders(items.Select(item => item.Order)))
            errors.Add("Information item orders must be unique and sequential.");
        if (items.Any(item => item.Text?.Values.Any(value => !string.IsNullOrWhiteSpace(value)) != true))
            errors.Add("Every information item must contain localized text.");
        if (items.Any(item => !IsGovernedInformationIcon(item.Icon)))
            errors.Add("Information item icons must use a governed icon key.");
        if (items.Any(item => item.Target is not null && !IsValidTarget(item.Target)))
            errors.Add("An information item contains an invalid target.");
        if (items.Any(item => item.Target?.Type == FormActionTargetType.ManagedResource))
            errors.Add("Managed resources are allowed only for auxiliary actions.");
    }

    private static void ValidateAuxiliaryActions(
        FormDesignV2SettingsDto design,
        IEnumerable<FormAuxiliaryActionDto> source,
        ICollection<string> errors)
    {
        var actions = source.OrderBy(action => action.Order).ToList();
        var layouts = design.AuxiliaryActionLayouts ?? new();
        if (actions.Count > MaximumAuxiliaryActions)
            errors.Add($"A Form may contain no more than {MaximumAuxiliaryActions} auxiliary actions.");
        if (actions.Any(action => string.IsNullOrWhiteSpace(action.Id)))
            errors.Add("Every auxiliary action must have a stable ID.");
        if (actions.GroupBy(action => action.Id, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            errors.Add("Auxiliary action IDs must be unique.");
        if (!HasSequentialOrders(actions.Select(action => action.Order)))
            errors.Add("Auxiliary action orders must be unique and sequential.");
        if (actions.Any(action => action.Label?.Values.Any(value => !string.IsNullOrWhiteSpace(value)) != true))
            errors.Add("Every auxiliary action must contain a localized label.");
        if (actions.Any(action => !IsValidTarget(action.Target)))
            errors.Add("An auxiliary action contains an invalid target.");

        var actionIds = actions.Select(action => action.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (layouts.Any(layout => string.IsNullOrWhiteSpace(layout.ActionId) || !actionIds.Contains(layout.ActionId)))
            errors.Add("Auxiliary action layout references an unknown action.");
        if (actionIds.Any(actionId => layouts.All(layout => !string.Equals(layout.ActionId, actionId, StringComparison.OrdinalIgnoreCase))))
            errors.Add("Every auxiliary action must have one layout record.");
        if (layouts.GroupBy(layout => layout.ActionId, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            errors.Add("Each auxiliary action may have only one layout record.");
    }

    private static bool IsValidTarget(FormActionTargetDto? target) => target is not null && target.Type switch
    {
        FormActionTargetType.InternalLink => IsSafeInternalTarget(target) &&
                                             AreBlank(target.Url, target.ResourceId, target.Phone, target.Email),
        FormActionTargetType.ExternalUrl => IsSafeExternalUrl(target.Url) &&
                                            AreBlank(target.PageId, target.Path, target.ResourceId, target.Phone, target.Email),
        FormActionTargetType.ManagedResource => !string.IsNullOrWhiteSpace(target.ResourceId) &&
                                                AreBlank(target.PageId, target.Path, target.Url, target.Phone, target.Email),
        FormActionTargetType.Phone => IsSafePhone(target.Phone) &&
                                      AreBlank(target.PageId, target.Path, target.Url, target.ResourceId, target.Email),
        FormActionTargetType.Email => IsSafeEmail(target.Email) &&
                                      AreBlank(target.PageId, target.Path, target.Url, target.ResourceId, target.Phone),
        _ => false
    };

    private static bool AreBlank(params string?[] values) => values.All(string.IsNullOrWhiteSpace);

    private static bool IsSafeInternalTarget(FormActionTargetDto target)
    {
        if (!string.IsNullOrWhiteSpace(target.Path))
        {
            var path = target.Path.Trim();
            return path.StartsWith("/", StringComparison.Ordinal) &&
                   !path.StartsWith("//", StringComparison.Ordinal) &&
                   !path.Contains('\r') &&
                   !path.Contains('\n');
        }

        return !string.IsNullOrWhiteSpace(target.PageId) &&
               target.PageId.Trim().All(character => char.IsLetterOrDigit(character) || character is '-' or '_');
    }

    private static bool IsSafeExternalUrl(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static bool IsSafeHttpOrRelative(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (value.StartsWith("/", StringComparison.Ordinal) && !value.StartsWith("//", StringComparison.Ordinal) ||
         IsSafeExternalUrl(value));

    private static bool IsSafePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var trimmed = value.Trim();
        var digits = trimmed.Count(char.IsDigit);
        return digits is >= 6 and <= 15 &&
               trimmed.All(character => char.IsDigit(character) || character is '+' or ' ' or '-' or '(' or ')' or '.') &&
               trimmed.Count(character => character == '+') <= 1 &&
               (!trimmed.Contains('+') || trimmed[0] == '+');
    }

    private static string NormalizePhone(string value) =>
        new(value.Trim().Where(character => char.IsDigit(character) || character == '+').ToArray());

    private static bool IsSafeEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            return new MailAddress(value.Trim()).Address == value.Trim();
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static int FieldHeight(FormFieldDefinitionDto field, FormLabelMode labelMode)
    {
        var type = FormInputTypeCatalog.NormalizeType(field.Type);
        if (type == "checkbox") return 44 + FormDesignPolicy.ReservedErrorHeightPx;
        var labelHeight = labelMode == FormLabelMode.Visible ? 28 : 0;
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
        return labelHeight + inputHeight + FormDesignPolicy.ReservedErrorHeightPx;
    }

    private static int MaxTextHeight(
        IReadOnlyDictionary<string, string>? values,
        int width,
        int fontSize,
        int lineHeight)
    {
        if (values is null || values.Count == 0) return 0;
        var charactersPerLine = Math.Max(10, (int)Math.Floor(width / Math.Max(1d, fontSize * .56d)));
        return values.Values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Replace("\r", string.Empty, StringComparison.Ordinal)
                .Split('\n')
                .Sum(line => Math.Max(1, (int)Math.Ceiling(line.Length / (double)charactersPerLine))) * lineHeight)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static string NormalizeFieldKey(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
    private static string NormalizeOpaqueId(string? value) => value?.Trim() ?? string.Empty;
    private static bool HasSequentialOrders(IEnumerable<int> orders) =>
        orders.OrderBy(order => order).SequenceEqual(Enumerable.Range(0, orders.Count()));

    private static string NormalizeColor(string? value, string fallback)
    {
        var color = value?.Trim();
        return !string.IsNullOrWhiteSpace(color) &&
               color.StartsWith('#') &&
               color.Length is 4 or 7 or 9 &&
               color.Skip(1).All(Uri.IsHexDigit)
            ? color.ToLowerInvariant()
            : fallback;
    }

}
