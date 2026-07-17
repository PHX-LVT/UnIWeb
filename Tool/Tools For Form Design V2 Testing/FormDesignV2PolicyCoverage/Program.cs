using Contracts.Forms;
using Contracts.Global;

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

var fields = new List<FormFieldDefinitionDto>
{
    new() { Key = "first-name", Type = "text", Order = 0 },
    new() { Key = "last-name", Type = "text", Order = 1 },
    new() { Key = "email", Type = "email", Order = 2 },
    new() { Key = "message", Type = "textarea", InputBoxSize = 3, Order = 3 }
};
var legacy = FormDesignPolicy.Normalize(new FormDesignSettingsDto
{
    Shape = FormDesignShape.TwoColumns,
    WidthPx = 840,
    FieldGapPx = 16
}, fields);
var projected = FormDesignV2Policy.ProjectFromV1("definition-1", legacy, fields);
var projectedAgain = FormDesignV2Policy.ProjectFromV1("definition-1", legacy, fields);

Assert(FormDesignPolicy.CurrentSchemaVersion == 1, "Phase 2 must not activate schema v2 writes.");
Assert(FormDesignV2Policy.TargetSchemaVersion == 2, "The target schema must be v2.");
Assert(FormDesignV2Policy.StandardDefaultWidthPx == 560 && FormDesignV2Policy.StandardMaximumWidthPx == 720,
    "Standard width must remain compact and capped at 720px.");
Assert(FormDesignV2Policy.CtaDefaultWidthPx == 760 && FormDesignV2Policy.CtaMaximumWidthPx == 1200,
    "CTA must start at 760px and remain resizable to 1200px.");
Assert(FormDesignV2Policy.SplitPanelDefaultWidthPx == 840 && FormDesignV2Policy.SplitPanelMaximumWidthPx == 1200,
    "Split Panel must start at 840px and remain resizable to 1200px.");
Assert(projected.OuterLayout == FormOuterLayout.SplitPanel, "TwoColumns must project to SplitPanel.");
Assert(projected.FieldRows.Count == 3, "Legacy two-column packing must produce two short rows plus one wide row.");
Assert(projected.FieldRows[0].FieldKeys.SequenceEqual(new[] { "first-name", "last-name" }), "Short fields must retain legacy pairing.");
Assert(projected.FieldRows[^1].FieldKeys.SequenceEqual(new[] { "message" }), "Wide fields must remain standalone.");
Assert(projected.FieldRows.Select(row => row.Id).SequenceEqual(projectedAgain.FieldRows.Select(row => row.Id)), "Projected row IDs must be deterministic.");
Assert(FormDesignV2Policy.Validate(projected, fields).Count == 0, "A deterministic projection must validate.");
legacy.V2 = projected;
Assert(FormDesignV2Policy.IsReadOnlyProjection("definition-1", legacy, fields), "An unchanged reader projection must remain compatible with v1 writes.");

var lifecycleProjection = FormDesignV2Policy.Normalize(projected);
lifecycleProjection.FieldRows[0].Id = "row-persist-once-editor-id";
legacy.V2 = lifecycleProjection;
Assert(FormDesignV2Policy.IsReadOnlyProjection("definition-1", legacy, fields), "A lifecycle-generated row ID may round-trip before v2 persistence when its structure is unchanged.");

var mixedRows = FormDesignV2Policy.Normalize(projected);
mixedRows.FieldRows =
[
    new() { Id = "row-a", Order = 0, FieldKeys = ["first-name"] },
    new() { Id = "row-b", Order = 1, FieldKeys = ["last-name", "email"] },
    new() { Id = "row-c", Order = 2, FieldKeys = ["message"] }
];
legacy.V2 = mixedRows;
Assert(FormDesignV2Policy.Validate(mixedRows, fields).Count == 0, "Mixed explicit rows must validate when coverage and wide-field rules are satisfied.");
Assert(!FormDesignV2Policy.IsReadOnlyProjection("definition-1", legacy, fields), "An authored mixed-row structure must not be discarded by the v1 write path.");

legacy.V2 = projected;
legacy.FieldGapPx = FormDesignV2Policy.MinimumFieldGapPx;
Assert(FormDesignPolicy.MinimumGapPx == 1 && FormDesignV2Policy.MinimumFieldGapPx == FormDesignPolicy.MinimumGapPx,
    "Legacy and v2 forms must both support the agreed compact 1px row gap.");
Assert(FormDesignV2Policy.IsReadOnlyProjection("definition-1", legacy, fields),
    "The shared compact spacing range must remain compatible with legacy-schema writes.");
legacy.FieldGapPx = FormDesignPolicy.MinimumGapPx;

var invalid = FormDesignV2Policy.Normalize(projected);
invalid.FieldRows[0].FieldKeys.Add("message");
Assert(FormDesignV2Policy.Validate(invalid, fields).Count > 0, "Duplicate and wide-field grouping must be rejected.");

var action = new FormAuxiliaryActionDto
{
    Id = "action-call",
    Label = new() { ["en"] = "Call now" },
    Target = new FormActionTargetDto { Type = FormActionTargetType.Phone, Phone = "+84 123 456 789" }
};
projected.AuxiliaryActionLayouts.Add(new FormAuxiliaryActionLayoutDto
{
    ActionId = action.Id,
    Placement = FormAuxiliaryActionPlacement.InformationPanel
});
Assert(FormDesignV2Policy.Validate(projected, fields, auxiliaryActions: new[] { action }).Count == 0, "A safe phone action must validate.");
Assert(FormDesignV2Policy.ResolveHref(action.Target) == "tel:+84123456789", "Phone targets must normalize to tel links.");
var preservedPlacement = FormDesignV2Policy.Normalize(new FormDesignV2SettingsDto
{
    OuterLayout = FormOuterLayout.Standard,
    AuxiliaryActionLayouts =
    [
        new() { ActionId = action.Id, Style = FormAuxiliaryActionStyle.Theme, Placement = FormAuxiliaryActionPlacement.InformationPanel }
    ]
});
Assert(preservedPlacement.AuxiliaryActionLayouts[0].Placement == FormAuxiliaryActionPlacement.InformationPanel,
    "A hidden Split Panel placement must be preserved while another layout is active.");
Assert(FormDesignV2Policy.ResolveHref(new FormActionTargetDto
{
    Type = FormActionTargetType.ExternalUrl,
    Url = "javascript:alert(1)"
}) is null, "Unsafe URL schemes must not resolve.");
var smuggledTarget = new FormAuxiliaryActionDto
{
    Id = "action-smuggled",
    Label = new() { ["en"] = "Unsafe" },
    Target = new FormActionTargetDto
    {
        Type = FormActionTargetType.ExternalUrl,
        Url = "https://example.com",
        PageId = "https://example.com"
    }
};
var smuggledDesign = FormDesignV2Policy.Normalize(projected);
smuggledDesign.AuxiliaryActionLayouts =
[
    new() { ActionId = smuggledTarget.Id, Placement = FormAuxiliaryActionPlacement.BelowFields }
];
Assert(FormDesignV2Policy.Validate(smuggledDesign, fields, auxiliaryActions: new[] { smuggledTarget }).Count > 0,
    "A target must reject populated fields that belong to a different discriminant even when their values match.");
var unsafeInternal = new FormInformationItemDto
{
    Id = "info-unsafe",
    Icon = "fas fa-link",
    Text = new() { ["en"] = "Unsafe" },
    Target = new FormActionTargetDto
    {
        Type = FormActionTargetType.InternalLink,
        PageId = "safe-page",
        Path = "javascript:alert(1)"
    }
};
Assert(FormDesignV2Policy.Validate(projected, fields, informationItems: new[] { unsafeInternal }).Count > 0,
    "An unsafe internal path must not be hidden by a valid Page ID.");
var duplicateFields = fields.Append(new FormFieldDefinitionDto { Key = "email", Type = "email", Order = 4 }).ToList();
var duplicateFieldErrors = FormDesignV2Policy.Validate(projected, duplicateFields);
Assert(duplicateFieldErrors.Any(error => error.Contains("field keys must be unique", StringComparison.OrdinalIgnoreCase)),
    "Duplicate field keys must return validation errors instead of throwing.");
var invalidIcon = new FormInformationItemDto
{
    Id = "info-1",
    Icon = "fas fa-user-secret",
    Text = new() { ["en"] = "Hidden" }
};
Assert(FormDesignV2Policy.Validate(projected, fields, informationItems: new[] { invalidIcon }).Count > 0,
    "Information items must use the governed icon allowlist.");
Assert(!FormDesignV2Policy.IsReadOnlyProjection("definition-1", legacy, fields, auxiliaryActions: new[] { action }),
    "Auxiliary actions must remain blocked from the v1 write path before activation.");

var spacingFields = fields.Take(2).ToList();
var tightSpacingLegacy = new FormDesignSettingsDto
{
    Shape = FormDesignShape.Stacked,
    WidthPx = FormDesignV2Policy.StandardDefaultWidthPx,
    FieldGapPx = FormDesignV2Policy.MinimumFieldGapPx
};
var looseSpacingLegacy = new FormDesignSettingsDto
{
    Shape = FormDesignShape.Stacked,
    WidthPx = FormDesignV2Policy.StandardDefaultWidthPx,
    FieldGapPx = FormDesignV2Policy.MaximumFieldGapPx
};
var spacingDesign = FormDesignV2Policy.ProjectFromV1("definition-spacing", tightSpacingLegacy, spacingFields);
var tightHeight = FormDesignV2Policy.CalculateRequiredHeight(tightSpacingLegacy, spacingDesign, spacingFields);
var looseHeight = FormDesignV2Policy.CalculateRequiredHeight(looseSpacingLegacy, spacingDesign, spacingFields);
Assert(looseHeight - tightHeight == FormDesignV2Policy.MaximumFieldGapPx - FormDesignV2Policy.MinimumFieldGapPx,
    "Field spacing must affect only the gap between persisted field rows, not structural header/Submit spacing.");

var height = FormDesignV2Policy.CalculateHeight(
    legacy,
    projected,
    fields,
    new Dictionary<string, string> { ["en"] = "Contact us" },
    new Dictionary<string, string> { ["en"] = "We will reply shortly." },
    new Dictionary<string, string> { ["en"] = "Send" },
    auxiliaryActions: new[] { action });
Assert(height is >= FormDesignPolicy.MinimumHeightPx and <= FormDesignPolicy.MaximumHeightPx, "Calculated height must remain governed.");

var contactFields = new List<FormFieldDefinitionDto>
{
    new() { Key = "first-name", Type = "text", Order = 0 },
    new() { Key = "last-name", Type = "text", Order = 1 },
    new() { Key = "email", Type = "email", Order = 2 },
    new() { Key = "phone", Type = "tel", Order = 3 },
    new() { Key = "message", Type = "textarea", InputBoxSize = 4, Order = 4 }
};
var contactInformation = new List<FormInformationItemDto>
{
    new() { Id = "contact-phone", Icon = "fas fa-phone", Text = new() { ["en"] = "+84 123 456 789" }, Target = new() { Type = FormActionTargetType.Phone, Phone = "+84 123 456 789" }, Order = 0 },
    new() { Id = "contact-email", Icon = "fas fa-envelope", Text = new() { ["en"] = "hello@example.com" }, Target = new() { Type = FormActionTargetType.Email, Email = "hello@example.com" }, Order = 1 },
    new() { Id = "contact-hours", Icon = "fas fa-clock", Text = new() { ["en"] = "Mon-Fri, 09:00-17:00" }, Order = 2 }
};
var contactActions = new List<FormAuxiliaryActionDto>
{
    new() { Id = "contact-call", Label = new() { ["en"] = "Call now" }, Target = new() { Type = FormActionTargetType.Phone, Phone = "+84 123 456 789" }, Order = 0 },
    new() { Id = "contact-email-action", Label = new() { ["en"] = "Email now" }, Target = new() { Type = FormActionTargetType.Email, Email = "hello@example.com" }, Order = 1 }
};
var contactDesign = FormDesignV2Policy.Normalize(new FormDesignV2SettingsDto
{
    OuterLayout = FormOuterLayout.SplitPanel,
    FieldRows =
    [
        new() { Id = "contact-row-1", Order = 0, FieldKeys = ["first-name", "last-name"] },
        new() { Id = "contact-row-2", Order = 1, FieldKeys = ["email", "phone"] },
        new() { Id = "contact-row-3", Order = 2, FieldKeys = ["message"] }
    ],
    InformationBackgroundColor = "#102b46",
    InformationTextColor = "#ffffff",
    FormBackgroundColor = "#ffffff",
    FormTextColor = "#0f172a",
    SplitPanelPercent = 42,
    SubmitLayout = FormSubmitLayout.Full,
    AuxiliaryActionLayouts =
    [
        new() { ActionId = "contact-call", Style = FormAuxiliaryActionStyle.Filled, Placement = FormAuxiliaryActionPlacement.InformationPanel },
        new() { ActionId = "contact-email-action", Style = FormAuxiliaryActionStyle.Outline, Placement = FormAuxiliaryActionPlacement.InformationPanel }
    ]
});
Assert(FormDesignV2Policy.Validate(contactDesign, contactFields, contactInformation, contactActions).Count == 0,
    "The Contact Network SplitPanel fixture must satisfy the governed v2 contract.");
var contactLegacy = new FormDesignSettingsDto { WidthPx = 1040, PaddingPx = 32, FieldGapPx = 8 };
var contactHeight = FormDesignV2Policy.CalculateHeight(
    contactLegacy,
    contactDesign,
    contactFields,
    new Dictionary<string, string> { ["en"] = "Contact our network" },
    new Dictionary<string, string> { ["en"] = "Talk with our regional team." },
    new Dictionary<string, string> { ["en"] = "Send request" },
    contactInformation,
    contactActions);
Assert(contactHeight is >= FormDesignPolicy.MinimumHeightPx and <= FormDesignPolicy.MaximumHeightPx,
    "The Contact Network fixture height must stay intrinsic and governed.");

var insightFields = new List<FormFieldDefinitionDto>
{
    new() { Key = "first-name", Type = "text", Order = 0 },
    new() { Key = "email", Type = "email", Order = 1 },
    new() { Key = "privacy-consent", Type = "checkbox", Order = 2 }
};
var insightLegacy = new FormDesignSettingsDto
{
    Shape = FormDesignShape.Cta,
    WidthPx = FormDesignV2Policy.CtaDefaultWidthPx,
    FieldGapPx = 12
};
var insightProjection = FormDesignV2Policy.ProjectFromV1("6a3507ee4a176a86ce0c2697", insightLegacy, insightFields);
var insightProjectionAgain = FormDesignV2Policy.ProjectFromV1("6a3507ee4a176a86ce0c2697", insightLegacy, insightFields);
Assert(insightProjection.OuterLayout == FormOuterLayout.Cta, "The Insight subscription fixture must project to CTA.");
Assert(insightProjection.FieldRows.SelectMany(row => row.FieldKeys).SequenceEqual(new[] { "first-name", "email", "privacy-consent" }),
    "The Insight projection must preserve its stable field keys in order.");
Assert(insightProjection.FieldRows.Select(row => row.Id).SequenceEqual(insightProjectionAgain.FieldRows.Select(row => row.Id)),
    "The Insight projection must retain deterministic row IDs across repeated runs.");
Assert(insightProjection.FieldRows[^1].FieldKeys.SequenceEqual(new[] { "privacy-consent" }),
    "The Insight consent checkbox must remain a standalone wide row.");

var themeDefault = FormDesignPolicy.CreateDefault();
Assert(themeDefault.UseThemeDefaults, "New Form designs must inherit Theme defaults.");
var themeCss = ThemeCssBuilder.Build(new PublicTheme
{
    ButtonStyle = "outline",
    ButtonColorRole = "primary",
    ButtonRadius = "14px"
});
Assert(themeCss.Contains("--theme-button-style: outline") &&
       themeCss.Contains("--theme-button-color-role: primary") &&
       themeCss.Contains("--theme-button-radius: 14px") &&
       themeCss.Contains("--theme-button-background-image: none") &&
       themeCss.Contains("--theme-button-hover-lift: -2px") &&
       themeCss.Contains("--theme-button-press-scale: 0.985"),
    "Theme CSS must expose independent button appearance and motion tokens.");
var motionlessThemeCss = ThemeCssBuilder.Build(new PublicTheme
{
    ButtonStyle = "filled",
    AnimationsEnabled = false
});
Assert(motionlessThemeCss.Contains("--theme-button-background-image: linear-gradient") &&
       motionlessThemeCss.Contains("--theme-button-hover-lift: 0px") &&
       motionlessThemeCss.Contains("--theme-button-press-scale: 1"),
    "Theme motion-off must preserve button styling without movement.");

var requiredInsidePlaceholder = FormFieldPresentationPolicy.ResolvePlaceholder(
    FormLabelMode.InsideInputs,
    configuredPlaceholder: null,
    fallbackLabel: "Message",
    required: true);
Assert(requiredInsidePlaceholder == "Message *",
    "Required fields using inside labels must expose the required marker in their placeholder.");
Assert(!FormFieldPresentationPolicy.ShouldShowLabel(FormLabelMode.InsideInputs, requiredInsidePlaceholder),
    "Long Text must not render a second visible label when its label is already inside the textarea.");
Assert(FormFieldPresentationPolicy.ResolvePlaceholder(
        FormLabelMode.InsideInputs,
        configuredPlaceholder: "Your message * ",
        fallbackLabel: "Message",
        required: true) == "Your message *",
    "A configured required marker must not be duplicated.");
Assert(FormFieldPresentationPolicy.ResolvePlaceholder(
        FormLabelMode.Visible,
        configuredPlaceholder: "Your message",
        fallbackLabel: "Message",
        required: true) == "Your message",
    "Visible-label mode must keep its required marker on the external label only.");

Assert(FormInputValueValidator.Validate("textarea", "   ", required: true) == FormInputValidationError.Required,
    "Whitespace-only required Long Text values must remain invalid.");
Assert(FormInputValueValidator.Validate("textarea", "   ", required: false) == FormInputValidationError.None,
    "Whitespace-only optional Long Text values must remain valid.");
Assert(FormInputValueValidator.Validate("select", "", required: true, ["one"]) == FormInputValidationError.Required,
    "Required selects must reject their empty placeholder option.");
Assert(FormInputValueValidator.Validate("checkbox", "false", required: true) == FormInputValidationError.Required &&
       FormInputValueValidator.Validate("checkbox", "true", required: true) == FormInputValidationError.None,
    "Required checkboxes must reject an unchecked value and accept a checked value.");

Console.WriteLine("Form Design v2 policy coverage passed.");
