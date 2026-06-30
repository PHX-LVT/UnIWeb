namespace Contracts.Forms;

public enum FormDisplayMode
{
    Modal,
    Embedded
}

public enum FormLayout
{
    Stacked,
    TwoColumns
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
    public FormDisplayMode DisplayMode { get; set; }
    public FormLayout Layout { get; set; } = FormLayout.Stacked;
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
    public FormDisplayMode DisplayMode { get; set; } = FormDisplayMode.Embedded;
    public FormLayout Layout { get; set; } = FormLayout.Stacked;
    public bool Active { get; set; } = true;
    public List<FormFieldDefinitionDto> Fields { get; set; } = new();
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
