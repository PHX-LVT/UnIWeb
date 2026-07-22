using AdminSite.Components.Pages.SectionEditors;
using AdminSite.Models;
using Bunit;

namespace AdminSite.ComponentTests;

public sealed class SectionEditorComponentTests : BunitContext
{
    [Fact]
    public void SegmentField_MarksCurrentOptionAndRaisesChangedValue()
    {
        string? selected = null;
        var options = new SectionDesignTab.DesignOption[]
        {
            new("stack", "Stack"),
            new("grid", "Grid", "fa-grid")
        };
        var cut = Render<SegmentField>(parameters => parameters
            .AddCascadingValue("AdminLanguage", "en")
            .Add(component => component.Label, "Layout")
            .Add(component => component.Value, "stack")
            .Add(component => component.Options, options)
            .Add(component => component.ValueChanged, value => selected = value));

        var buttons = cut.FindAll("button");
        Assert.Equal(2, buttons.Count);
        Assert.Contains("active", buttons[0].ClassList);
        Assert.DoesNotContain("active", buttons[1].ClassList);

        buttons[1].Click();

        Assert.Equal("grid", selected);
    }

    [Fact]
    public void ArrangementEditor_ShowsColumnsOnlyForGridAndClampsChanges()
    {
        var changes = 0;
        var draft = new SectionStyleModel
        {
            BlockLayoutMode = "grid",
            BlockGridColumns = 4,
            BlockGap = "medium"
        };
        var cut = Render<SectionBlockArrangementEditor>(parameters => parameters
            .AddCascadingValue("AdminLanguage", "en")
            .Add(component => component.Draft, draft)
            .Add(component => component.Changed, () => changes++));

        var input = cut.Find("input[type=number]");
        Assert.Equal("4", input.GetAttribute("value"));

        input.Change("99");

        Assert.Equal(12, draft.BlockGridColumns);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void ArrangementEditor_SegmentUpdatesDraftAndNotifiesParent()
    {
        var changes = 0;
        var draft = new SectionStyleModel
        {
            BlockLayoutMode = "stack",
            BlockGridColumns = 12,
            BlockGap = "medium"
        };
        var cut = Render<SectionBlockArrangementEditor>(parameters => parameters
            .AddCascadingValue("AdminLanguage", "en")
            .Add(component => component.Draft, draft)
            .Add(component => component.Changed, () => changes++));

        cut.FindAll("button.section-design-segment")
            .Single(button => button.TextContent.Trim() == "Grid")
            .Click();

        Assert.Equal("grid", draft.BlockLayoutMode);
        Assert.Equal(1, changes);
        Assert.NotEmpty(cut.FindAll("input[type=number]"));
    }

    [Fact]
    public void ArrangementEditor_CollapsesWithoutMutatingDraft()
    {
        var draft = new SectionStyleModel { BlockLayoutMode = "stack" };
        var cut = Render<SectionBlockArrangementEditor>(parameters => parameters
            .AddCascadingValue("AdminLanguage", "en")
            .Add(component => component.Draft, draft));

        cut.Find("button.section-design-group__toggle").Click();

        Assert.Empty(cut.FindAll(".section-design-group__body"));
        Assert.Equal("stack", draft.BlockLayoutMode);
    }
}
