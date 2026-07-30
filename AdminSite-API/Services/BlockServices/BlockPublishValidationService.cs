using Contracts.Admin;
using FullProject.Models;

namespace FullProject.Services.BlockServices;

public static class BlockPublishValidationService
{
    public static IReadOnlyList<string> Validate(IEnumerable<Block>? source)
    {
        var blocks = (source ?? Array.Empty<Block>()).ToList();
        var byId = blocks.ToDictionary(block => block.Id, StringComparer.Ordinal);
        var errors = new List<string>();

        foreach (var block in blocks)
        {
            if (!string.IsNullOrWhiteSpace(block.ParentBlockId))
            {
                if (!byId.TryGetValue(block.ParentBlockId, out var parent))
                    errors.Add($"Block '{DisplayName(block)}' belongs to a missing Container.");
                else if (parent is not ContainerBlock)
                    errors.Add($"Block '{DisplayName(block)}' has a parent that is not a Container.");
                else if (block is ContainerBlock)
                    errors.Add("Containers cannot be nested inside another Container.");
            }

            ValidateFunctionalBlock(block, errors);
        }

        foreach (var container in blocks.OfType<ContainerBlock>().Where(block => block.Visible))
        {
            var allChildren = blocks
                .Where(block => string.Equals(block.ParentBlockId, container.Id, StringComparison.Ordinal))
                .OrderBy(block => block.Order)
                .ToList();
            var children = allChildren
                .Where(block => block.Visible && string.Equals(block.ParentBlockId, container.Id, StringComparison.Ordinal))
                .OrderBy(block => block.Order)
                .ToList();

            if (ContainerPresetCatalog.TryGetFormation(container.PresetKey, out var formation))
            {
                if (allChildren.Count != formation.MaximumChildren)
                    errors.Add($"{ContainerName(container)} must contain exactly {formation.MaximumChildren} governed slots.");

                foreach (var child in allChildren.Where(child =>
                             child.Authoring?.IsPlaceholder == true || !HasFormationContent(child)))
                    errors.Add($"{ContainerName(container)} has an incomplete {ContainerCapacityPolicy.SlotDisplayName(container.PresetKey, child.Authoring?.PresetSlotName)} slot.");

                var duplicateSlot = allChildren
                    .Where(child => !string.IsNullOrWhiteSpace(child.Authoring?.PresetSlotName))
                    .GroupBy(child => child.Authoring.PresetSlotName!, StringComparer.Ordinal)
                    .FirstOrDefault(group => group.Count() > 1);
                if (duplicateSlot is not null)
                    errors.Add($"{ContainerName(container)} assigns more than one Block to {duplicateSlot.Key}.");

                foreach (var child in allChildren)
                {
                    if (!string.Equals(child.Authoring?.PresetSourceId, formation.Key, StringComparison.Ordinal))
                        errors.Add($"{ContainerName(container)} contains a Block with invalid Formation ownership.");
                    if (!formation.AllowedBlockTypes.Contains(BlockType(child), StringComparer.Ordinal))
                        errors.Add($"{ContainerName(container)} does not allow {BlockType(child)} Blocks.");
                }
            }

            var missing = ContainerCapacityPolicy.MissingRequiredSlots(
                container.PresetKey,
                children.Select(child => child.Authoring?.PresetSlotName));
            foreach (var slot in missing)
                errors.Add($"{ContainerName(container)} requires a {slot.DisplayName} Block before publishing.");

            var capacity = ContainerCapacityPolicy.MaxChildren(
                container.PresetKey,
                container.ContainerLayout?.Mode,
                container.ContainerLayout?.Columns);
            if (children.Count > capacity)
                errors.Add($"{ContainerName(container)} contains {children.Count} Blocks but supports only {capacity}.");

            if (container.ContainerLayout?.Purpose == "collection")
            {
                var types = children.Select(BlockType).Distinct(StringComparer.Ordinal).ToList();
                if (types.Count > 1)
                    errors.Add($"{ContainerName(container)} is a Collection and cannot publish mixed Block types.");
                if (types.Count == 1 && !string.IsNullOrWhiteSpace(container.ContainerLayout.AllowedChildType) &&
                    !string.Equals(types[0], container.ContainerLayout.AllowedChildType, StringComparison.Ordinal))
                    errors.Add($"{ContainerName(container)} is locked to {container.ContainerLayout.AllowedChildType} Blocks.");
            }
        }

        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static void ValidateFunctionalBlock(Block block, ICollection<string> errors)
    {
        if (!block.Visible || block.Authoring?.IsPlaceholder == true) return;

        switch (block)
        {
            case ImageBlock image when string.IsNullOrWhiteSpace(image.Asset?.Url):
                errors.Add("A visible Image Block must contain an image before publishing.");
                break;
            case ImageBlock image when image.Appearance?.Decorative != true && !HasText(image.AltText):
                errors.Add("A visible Image Block requires alt text unless it is decorative.");
                break;
            case VideoBlock video when string.IsNullOrWhiteSpace(video.Asset?.Url):
                errors.Add("A visible Video Block must contain an upload or YouTube link before publishing.");
                break;
            case FileBlock file when string.IsNullOrWhiteSpace(file.Asset?.Url):
                errors.Add("A visible File Block must contain a file before publishing.");
                break;
            case ButtonBlock button when !HasText(button.Label):
                errors.Add("A visible Button Block must have a label before publishing.");
                break;
            case ButtonBlock button when HasText(button.Label):
                ValidateAction(button.Action, button.Href, button.FormDefinitionId, "Button", errors);
                break;
            case CardBlock card when HasText(card.ButtonLabel):
                ValidateAction(card.Action, card.Href, card.FormDefinitionId, "Card action", errors);
                break;
            case IconBlock icon when icon.ActionEnabled:
                ValidateAction(icon.Action, icon.Href, icon.FormDefinitionId, "Icon action", errors);
                break;
            case FormBlock form when string.IsNullOrWhiteSpace(form.FormDefinitionId):
                errors.Add("A visible Form Block must reference an active Form Definition before publishing.");
                break;
        }
    }

    private static void ValidateAction(
        string? action,
        string? href,
        string? formDefinitionId,
        string label,
        ICollection<string> errors)
    {
        if (string.Equals(action, "openForm", StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(formDefinitionId))
                errors.Add($"{label} must reference a Form Definition before publishing.");
            return;
        }

        if (string.IsNullOrWhiteSpace(href))
            errors.Add($"{label} requires a destination before publishing.");
    }

    private static bool HasText(IReadOnlyDictionary<string, string>? values) =>
        values?.Values.Any(value => !string.IsNullOrWhiteSpace(value)) == true;

    private static bool HasFormationContent(Block block) => block switch
    {
        TextBlock text => HasText(text.Title) || HasText(text.Content),
        ImageBlock image => !string.IsNullOrWhiteSpace(image.Asset?.Url),
        CardBlock card => HasText(card.Title) || HasText(card.Description) ||
            !string.IsNullOrWhiteSpace(card.Icon) || !string.IsNullOrWhiteSpace(card.Asset?.Url),
        ButtonBlock button => HasText(button.Label),
        MetricBlock metric => HasText(metric.Label) || !string.IsNullOrWhiteSpace(metric.Value),
        StepBlock step => HasText(step.Title) || HasText(step.Description) || HasText(step.StepLabel),
        IconBlock icon => !string.IsNullOrWhiteSpace(icon.Icon) || HasText(icon.Label) || HasText(icon.Description),
        _ => false
    };

    private static string ContainerName(ContainerBlock container) =>
        container.Title?.GetValueOrDefault("en") is { Length: > 0 } title
            ? $"Formation '{title}'"
            : ContainerPresetCatalog.IsFormation(container.PresetKey) ? "The Formation" : "The Container";

    private static string DisplayName(Block block) =>
        block.EditorLabel?.GetValueOrDefault("en") is { Length: > 0 } label ? label : block.Id;

    private static string BlockType(Block block) => block switch
    {
        TextBlock => "text", ImageBlock => "image", VideoBlock => "video", FileBlock => "file",
        MapBlock => "map", FormBlock => "form", CardBlock => "card", ButtonBlock => "button",
        MetricBlock => "metric", BulletListBlock => "bullet-list", StepBlock => "step", IconBlock => "icon",
        ContainerBlock => "container", _ => "text"
    };
}
