namespace AdminSite.Models;

public sealed record BlockCreationRequest(
    string PageId,
    string SectionId,
    string? SlotId,
    string? ParentBlockId,
    string BlockZone,
    string? PositionMode,
    IReadOnlyCollection<string>? AllowedTypes,
    string? ConstraintLabel,
    string TargetLabel,
    bool ReturnToArrange);
