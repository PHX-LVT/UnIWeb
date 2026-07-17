namespace Contracts.Forms;

public enum FormDesignShape
{
    Stacked,
    TwoColumns,
    Cta
}

public enum FormLabelMode
{
    Visible,
    InsideInputs
}

public enum FormDesignBackgroundMode
{
    Transparent,
    Solid
}

public enum FormDesignTextAlign
{
    Left,
    Center
}

public enum FormDesignButtonWidth
{
    Content,
    Full
}

public enum FormOuterLayout
{
    Standard,
    SplitPanel,
    Cta
}

public enum FormSubmitLayout
{
    Left,
    Center,
    Right,
    Full
}

public enum FormActionTargetType
{
    InternalLink,
    ExternalUrl,
    ManagedResource,
    Phone,
    Email
}

public enum FormAuxiliaryActionStyle
{
    Filled,
    Outline,
    Ghost,
    Theme
}

public enum FormAuxiliaryActionPlacement
{
    InformationPanel,
    BelowFields
}

public enum FormDesignCompatibilityMode
{
    V1Only,
    DualReadV1Write,
    DualReadV2Write
}

public class FormFieldRowDto
{
    public string Id { get; set; } = string.Empty;
    public int Order { get; set; }
    public List<string> FieldKeys { get; set; } = new();
}

public class FormActionTargetDto
{
    public FormActionTargetType Type { get; set; } = FormActionTargetType.InternalLink;
    public string? PageId { get; set; }
    public string? Path { get; set; }
    public string? Url { get; set; }
    public string? ResourceId { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    // Public/API projections may resolve a governed target without persisting the URL.
    public string? ResolvedHref { get; set; }
}

public class FormInformationItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public Dictionary<string, string> Text { get; set; } = new();
    public FormActionTargetDto? Target { get; set; }
    public int Order { get; set; }
}

public class FormAuxiliaryActionDto
{
    public string Id { get; set; } = string.Empty;
    public Dictionary<string, string> Label { get; set; } = new();
    public FormActionTargetDto Target { get; set; } = new();
    public int Order { get; set; }
}

public class FormAuxiliaryActionLayoutDto
{
    public string ActionId { get; set; } = string.Empty;
    public FormAuxiliaryActionStyle Style { get; set; } = FormAuxiliaryActionStyle.Outline;
    public FormAuxiliaryActionPlacement Placement { get; set; } = FormAuxiliaryActionPlacement.BelowFields;
}

public class FormDesignV2SettingsDto
{
    public FormOuterLayout OuterLayout { get; set; } = FormOuterLayout.Standard;
    public List<FormFieldRowDto> FieldRows { get; set; } = new();
    public string InformationBackgroundColor { get; set; } = "#0f2740";
    public string InformationTextColor { get; set; } = "#ffffff";
    public string FormBackgroundColor { get; set; } = "#ffffff";
    public string FormTextColor { get; set; } = "#0f172a";
    public int SplitPanelPercent { get; set; } = 40;
    public FormSubmitLayout SubmitLayout { get; set; } = FormSubmitLayout.Full;
    public List<FormAuxiliaryActionLayoutDto> AuxiliaryActionLayouts { get; set; } = new();
}

public class FormDesignSettingsDto
{
    // False is the backward-compatible deserialization default for forms saved
    // before Theme inheritance existed. New forms opt in through CreateDefault.
    public bool UseThemeDefaults { get; set; }
    public int SchemaVersion { get; set; } = FormDesignPolicy.CurrentSchemaVersion;
    public FormDesignShape Shape { get; set; } = FormDesignShape.Stacked;
    public int WidthPx { get; set; } = FormDesignPolicy.StackedDefaultWidthPx;
    public int CalculatedHeightPx { get; set; } = FormDesignPolicy.MinimumHeightPx;
    public FormDesignBackgroundMode BackgroundMode { get; set; } = FormDesignBackgroundMode.Solid;
    public string BackgroundColor { get; set; } = "#ffffff";
    public string TextColor { get; set; } = "#0f172a";
    public string AccentColor { get; set; } = "#1d4ed8";
    public string BorderColor { get; set; } = "#dbe3ef";
    public int BorderWidthPx { get; set; } = 1;
    public int BorderRadiusPx { get; set; } = 16;
    public string Shadow { get; set; } = "small";
    public int PaddingPx { get; set; } = 32;
    public int FieldGapPx { get; set; } = 16;
    public FormDesignTextAlign TextAlign { get; set; } = FormDesignTextAlign.Left;
    public FormLabelMode LabelMode { get; set; } = FormLabelMode.Visible;
    public string ButtonStyle { get; set; } = "filled";
    public FormDesignButtonWidth ButtonWidth { get; set; } = FormDesignButtonWidth.Full;
    public FormDesignV2SettingsDto? V2 { get; set; }
}

public enum FormSubmissionStatus
{
    New,
    InProgress,
    Resolved,
    Spam,
    Archived
}

public class FormFieldOptionDto
{
    public string Value { get; set; } = string.Empty;
    public Dictionary<string, string> Label { get; set; } = new();
    public int Order { get; set; }
}

public class FormFieldDefinitionDto
{
    public string Key { get; set; } = string.Empty;
    public string Type { get; set; } = "text";
    public Dictionary<string, string> Label { get; set; } = new();
    public Dictionary<string, string> Placeholder { get; set; } = new();
    public bool Required { get; set; }
    public int MinLength { get; set; }
    public int MaxLength { get; set; }
    public int InputBoxSize { get; set; }
    public List<FormFieldOptionDto> Options { get; set; } = new();
    public int Order { get; set; }
}

public class FormDefinitionResponse
{
    public string Id { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public Dictionary<string, string> Name { get; set; } = new();
    public Dictionary<string, string> Introduction { get; set; } = new();
    public Dictionary<string, string> SubmitButtonLabel { get; set; } = new();
    public List<FormInformationItemDto> InformationItems { get; set; } = new();
    public List<FormAuxiliaryActionDto> AuxiliaryActions { get; set; } = new();
    public FormDesignSettingsDto Design { get; set; } = new();
    public bool Active { get; set; }
    public List<FormFieldDefinitionDto> Fields { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class FormDefinitionUsageItemDto
{
    public string Area { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string PageName { get; set; } = string.Empty;
    public string PageSlug { get; set; } = string.Empty;
    public string SectionType { get; set; } = string.Empty;
    public string SectionTitle { get; set; } = string.Empty;
    public string ElementLabel { get; set; } = string.Empty;
}

public class FormDefinitionUsageResponse
{
    public string FormDefinitionId { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public long SubmissionCount { get; set; }
    public List<FormDefinitionUsageItemDto> Items { get; set; } = new();
}

public class FormInputTypeResponse
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string LabelKey { get; set; } = string.Empty;
    public Dictionary<string, string> Name { get; set; } = new();
    public bool Active { get; set; } = true;
    public bool SupportsMaxCharacters { get; set; }
    public bool SupportsOptions { get; set; }
    public bool SupportsInputBoxSize { get; set; }
    public bool UsesMultilineInput { get; set; }
    public int DefaultMaxCharacters { get; set; }
    public int DefaultInputBoxSize { get; set; }
    public int Order { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class FormInputTypeUpdateRequest
{
    public Dictionary<string, string> Name { get; set; } = new();
    public bool Active { get; set; } = true;
    public bool SupportsMaxCharacters { get; set; }
    public bool SupportsOptions { get; set; }
    public bool SupportsInputBoxSize { get; set; }
    public int DefaultMaxCharacters { get; set; }
    public int DefaultInputBoxSize { get; set; }
}
public class FormSubmissionFieldResponse
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "text";
    public string Value { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool IsDeletedField { get; set; }
}

public class FormSubmissionTimelineEventResponse
{
    public string EventType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ManagedFormSubmissionResponse
{
    public string Id { get; set; } = string.Empty;
    public string FormId { get; set; } = string.Empty;
    public string FormKey { get; set; } = string.Empty;
    public string FormName { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public string SourcePage { get; set; } = string.Empty;
    public FormSubmissionStatus Status { get; set; }
    public List<FormSubmissionFieldResponse> Fields { get; set; } = new();
    public string? InternalNotes { get; set; }
    public string? AssignedToAdminId { get; set; }
    public string? AssignedToAdminName { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ViewedAt { get; set; }
    public string? ViewedByAdminId { get; set; }
    public List<FormSubmissionTimelineEventResponse> Timeline { get; set; } = new();
    public DateTime SubmittedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ManagedFormSubmissionUpdateRequest
{
    public FormSubmissionStatus Status { get; set; }
    public string? InternalNotes { get; set; }
    public string? AssignedToAdminId { get; set; }
}

public class FormSubmissionAssigneeResponse
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class FormDefinitionUpsertRequest
{
    public string Key { get; set; } = string.Empty;
    public Dictionary<string, string> Name { get; set; } = new();
    public Dictionary<string, string> Introduction { get; set; } = new();
    public Dictionary<string, string> SubmitButtonLabel { get; set; } = new();
    public List<FormInformationItemDto> InformationItems { get; set; } = new();
    public List<FormAuxiliaryActionDto> AuxiliaryActions { get; set; } = new();
    public FormDesignSettingsDto Design { get; set; } = new();
    public bool Active { get; set; } = true;
    public List<FormFieldDefinitionDto> Fields { get; set; } = new();
}

public class FormDefinitionOrderResponse
{
    public int Revision { get; set; }
    public List<string> DefinitionIds { get; set; } = new();
    public bool PersistentOrderAvailable { get; set; }
    public bool WritesEnabled { get; set; }
}

public class FormDefinitionReorderRequest
{
    public int ExpectedRevision { get; set; }
    public List<string> DefinitionIds { get; set; } = new();
}

public class PublicFormSubmitRequest
{
    public Dictionary<string, string> Data { get; set; } = new();
    public string Language { get; set; } = "en";
    public string SourcePage { get; set; } = string.Empty;
    public string Honeypot { get; set; } = string.Empty;
    public string? CaptchaToken { get; set; }
}

public class PublicFormSubmitResponse
{
    public bool Accepted { get; set; }
    public string? SubmissionId { get; set; }
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string> FieldErrors { get; set; } = new();
}
