using Contracts.Icons;
using FullProject.Security;
using FullProject.Services.IconServices;
using System.Text;

namespace Core.UnitTests;

public sealed class IconCatalogTests
{
    [Theory]
    [InlineData("fas fa-truck", "fa-solid fa-truck")]
    [InlineData("fa fa-check", "fa-solid fa-check")]
    [InlineData("fab fa-facebook", "fa-brands fa-facebook")]
    public void Normalize_ConvertsSupportedLegacyClasses(string input, string expected)
    {
        Assert.Equal(expected, IconCatalog.Normalize(input));
    }

    [Fact]
    public void Context_RestrictsSocialBrandsAndFormInformation()
    {
        Assert.True(IconCatalog.IsAllowed("fab fa-facebook", IconContext.SocialBrand));
        Assert.False(IconCatalog.IsAllowed("fab fa-facebook", IconContext.FormInformation));
        Assert.True(IconCatalog.IsAllowed("fas fa-envelope", IconContext.FormInformation));
    }

    [Fact]
    public async Task SvgUpload_RemovesExecutableAndExternalContent()
    {
        const string svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'><script>alert(1)</script><use href='https://evil.test/x.svg#x'/><path onclick='x()' d='M0 0h24v24z'/></svg>";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));

        var result = await CustomIconUploadPolicy.ReadAndValidateAsync(stream, "safe.svg", "image/svg+xml", stream.Length, TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error);
        var sanitized = Encoding.UTF8.GetString(result.Bytes!);
        Assert.DoesNotContain("script", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evil.test", sanitized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SvgUpload_RejectsOversizedViewBox()
    {
        const string svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 4096 24'><path d='M0 0h1v1z'/></svg>";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));

        var result = await CustomIconUploadPolicy.ReadAndValidateAsync(stream, "wide.svg", "image/svg+xml", stream.Length, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("dimensions", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RasterUpload_RejectsExtensionSignatureMismatch()
    {
        await using var stream = new MemoryStream("not a png"u8.ToArray());

        var result = await CustomIconUploadPolicy.ReadAndValidateAsync(stream, "fake.png", "image/png", stream.Length, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("extension", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuiltInIcon_AppearanceIsNormalizedPerUsage()
    {
        var result = IconReferenceService.ToModel(new IconReferenceDto
        {
            Source = IconSources.BuiltIn,
            ClassName = "fa-solid fa-star",
            Appearance = new IconAppearanceDto
            {
                ColorMode = "custom",
                Color = "#ABCDEF",
                Size = "x-large",
                BackgroundMode = "theme",
                BackgroundThemeRole = "primary",
                Shape = "circle"
            }
        });

        Assert.NotNull(result?.Appearance);
        Assert.Equal("#abcdef", result.Appearance.Color);
        Assert.Equal("x-large", result.Appearance.Size);
        Assert.Equal("primary", result.Appearance.BackgroundThemeRole);
        Assert.Equal("circle", result.Appearance.Shape);
    }

    [Fact]
    public void CustomIcon_DiscardsAppearanceOverrides()
    {
        var result = IconReferenceService.ToModel(new IconReferenceDto
        {
            SchemaVersion = 1,
            Source = IconSources.Custom,
            ResourceId = "resource",
            Url = "https://assets.test/icon.svg",
            Appearance = new IconAppearanceDto
            {
                ColorMode = "custom",
                Color = "#abcdef",
                Size = "x-large",
                BackgroundMode = "custom",
                BackgroundColor = "#000000",
                Shape = "circle"
            }
        });

        Assert.NotNull(result);
        Assert.Null(result.Appearance);
    }

    [Theory]
    [InlineData("rounded-square")]
    [InlineData("circle")]
    [InlineData("oval")]
    [InlineData("diamond")]
    [InlineData("hexagon")]
    [InlineData("star")]
    public void BuiltInIcon_PreservesSupportedBackgroundShape(string shape)
    {
        var result = IconReferenceService.ToModel(new IconReferenceDto
        {
            Source = IconSources.BuiltIn,
            ClassName = "fa-solid fa-truck",
            Appearance = new IconAppearanceDto { BackgroundMode = "theme", Shape = shape }
        });

        Assert.Equal(shape, result?.Appearance?.Shape);
    }
}
