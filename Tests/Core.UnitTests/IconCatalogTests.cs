using Contracts.Icons;
using FullProject.Security;
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
}
