using Contracts.Forms;

namespace Core.UnitTests;

public sealed class FormDesignPolicyTests
{
    [Theory]
    [InlineData(FormDesignShape.Stacked, 560, 320, 720)]
    [InlineData(FormDesignShape.TwoColumns, 840, 640, 1100)]
    [InlineData(FormDesignShape.Cta, 760, 680, 1200)]
    public void CreateDefault_UsesShapeSpecificWidth(
        FormDesignShape shape,
        int expectedWidth,
        int expectedMinimum,
        int expectedMaximum)
    {
        var result = FormDesignPolicy.CreateDefault(shape);

        Assert.True(result.UseThemeDefaults);
        Assert.Equal(expectedWidth, result.WidthPx);
        Assert.Equal(expectedMinimum, FormDesignPolicy.MinimumWidth(shape));
        Assert.Equal(expectedMaximum, FormDesignPolicy.MaximumWidth(shape));
        Assert.InRange(result.CalculatedHeightPx, FormDesignPolicy.MinimumHeightPx, FormDesignPolicy.MaximumHeightPx);
    }

    [Fact]
    public void Normalize_ClampsNumbersAndReplacesUnsafeValues()
    {
        var result = FormDesignPolicy.Normalize(new FormDesignSettingsDto
        {
            Shape = FormDesignShape.Stacked,
            WidthPx = 10_000,
            PaddingPx = -1,
            FieldGapPx = 100,
            BorderWidthPx = 20,
            BorderRadiusPx = -4,
            BackgroundColor = "red",
            TextColor = " #ABCDEF ",
            Shadow = "unsafe",
            ButtonStyle = "unsafe"
        });

        Assert.Equal(FormDesignPolicy.MaximumWidth(FormDesignShape.Stacked), result.WidthPx);
        Assert.Equal(FormDesignPolicy.MinimumPaddingPx, result.PaddingPx);
        Assert.Equal(FormDesignPolicy.MaximumGapPx, result.FieldGapPx);
        Assert.Equal(4, result.BorderWidthPx);
        Assert.Equal(0, result.BorderRadiusPx);
        Assert.Equal("#ffffff", result.BackgroundColor);
        Assert.Equal("#abcdef", result.TextColor);
        Assert.Equal("none", result.Shadow);
        Assert.Equal("filled", result.ButtonStyle);
    }

    [Theory]
    [InlineData("textarea", true)]
    [InlineData("longtext", true)]
    [InlineData("checkbox", true)]
    [InlineData("text", false)]
    [InlineData("email", false)]
    public void IsWideField_NormalizesKnownInputTypes(string type, bool expected) =>
        Assert.Equal(expected, FormDesignPolicy.IsWideField(type));

    [Fact]
    public void CalculateHeight_GrowsForLongTextButNeverExceedsLimit()
    {
        var design = FormDesignPolicy.CreateDefault();
        var shortForm = new[] { Field("name", "text", 0) };
        var longForm = Enumerable.Range(0, 40).Select(index => Field($"details-{index}", "textarea", index, 5));

        var shortHeight = FormDesignPolicy.CalculateHeight(design, shortForm);
        var longHeight = FormDesignPolicy.CalculateHeight(design, longForm);

        Assert.True(longHeight > shortHeight);
        Assert.Equal(FormDesignPolicy.MaximumHeightPx, longHeight);
    }

    [Theory]
    [InlineData(FormLabelMode.InsideInputs, "", "Email", true, "Email *")]
    [InlineData(FormLabelMode.InsideInputs, "Your email", "Email", true, "Your email *")]
    [InlineData(FormLabelMode.InsideInputs, "Your email *", "Email", true, "Your email *")]
    [InlineData(FormLabelMode.Visible, "Your email", "Email", true, "Your email")]
    public void ResolvePlaceholder_HandlesInsideLabelsAndRequiredMarker(
        FormLabelMode mode,
        string configured,
        string label,
        bool required,
        string expected) =>
        Assert.Equal(expected, FormFieldPresentationPolicy.ResolvePlaceholder(mode, configured, label, required));

    [Theory]
    [InlineData(FormLabelMode.Visible, "placeholder", true)]
    [InlineData(FormLabelMode.InsideInputs, "placeholder", false)]
    [InlineData(FormLabelMode.InsideInputs, "", true)]
    public void ShouldShowLabel_PreventsDuplicateInsideLabel(
        FormLabelMode mode,
        string placeholder,
        bool expected) =>
        Assert.Equal(expected, FormFieldPresentationPolicy.ShouldShowLabel(mode, placeholder));

    [Fact]
    public void BlockDefaultSize_RespectsAvailableWidthAndTwelveColumnGrid()
    {
        var design = FormDesignPolicy.CreateDefault(FormDesignShape.Cta);

        var result = FormBlockLayoutPolicy.CalculateDefaultSize(design, 700);

        Assert.Equal(700, result.WidthPx);
        Assert.Equal(12, result.WidthUnits);
        Assert.Equal(100d, result.WidthPercent);
        Assert.InRange(result.HeightPx, FormDesignPolicy.MinimumHeightPx, FormBlockLayoutPolicy.MaximumHeightPx);
    }

    [Theory]
    [InlineData("narrow", FormBlockLayoutPolicy.NarrowContentWidthPx)]
    [InlineData("full", FormBlockLayoutPolicy.FullContentWidthPx)]
    [InlineData("normal", FormBlockLayoutPolicy.NormalContentWidthPx)]
    [InlineData(null, FormBlockLayoutPolicy.NormalContentWidthPx)]
    public void AvailableContentWidth_UsesSectionWidth(string? value, int expected) =>
        Assert.Equal(expected, FormBlockLayoutPolicy.AvailableContentWidthPx(value));

    [Fact]
    public void V2Projection_IsDeterministicAndKeepsWideFieldsExclusive()
    {
        var fields = new[]
        {
            Field("first", "text", 0),
            Field("last", "text", 1),
            Field("message", "textarea", 2),
            Field("email", "email", 3)
        };

        var first = FormDesignV2Policy.CreateDefault("form-1", FormDesignPolicy.CreateDefault(FormDesignShape.TwoColumns), fields);
        var second = FormDesignV2Policy.CreateDefault("form-1", FormDesignPolicy.CreateDefault(FormDesignShape.TwoColumns), fields);

        Assert.True(FormDesignV2Policy.AreEquivalent(first, second));
        Assert.All(first.FieldRows.Where(row => row.FieldKeys.Contains("message")), row => Assert.Single(row.FieldKeys));
        Assert.Empty(FormDesignV2Policy.Validate(first, fields));
    }

    [Fact]
    public void V2Validation_RejectsDuplicateAndUnknownFields()
    {
        var fields = new[] { Field("email", "email", 0) };
        var design = new FormDesignV2SettingsDto
        {
            FieldRows =
            [
                new() { Id = "row-1", Order = 0, FieldKeys = ["email"] },
                new() { Id = "row-2", Order = 1, FieldKeys = ["email", "missing"] }
            ]
        };

        var errors = FormDesignV2Policy.Validate(design, fields);

        Assert.Contains(errors, error => error.Contains("unknown", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("only one", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ResolveHref_AcceptsGovernedTargetsAndRejectsScriptUrls()
    {
        var internalHref = FormDesignV2Policy.ResolveHref(new FormActionTargetDto
        {
            Type = FormActionTargetType.InternalLink,
            Path = "/contact"
        });
        var unsafeHref = FormDesignV2Policy.ResolveHref(new FormActionTargetDto
        {
            Type = FormActionTargetType.ExternalUrl,
            Url = "javascript:alert(1)"
        });

        Assert.Equal("/contact", internalHref);
        Assert.Null(unsafeHref);
    }

    private static FormFieldDefinitionDto Field(string key, string type, int order, int inputBoxSize = 1) => new()
    {
        Key = key,
        Type = type,
        Order = order,
        InputBoxSize = inputBoxSize,
        Label = new() { ["en"] = key }
    };
}
