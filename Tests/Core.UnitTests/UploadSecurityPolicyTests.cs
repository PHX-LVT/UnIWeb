using FullProject.Models;
using FullProject.Security;

namespace Core.UnitTests;

public sealed class UploadSecurityPolicyTests
{
    [Theory]
    [InlineData("photo.png", "image/png", true)]
    [InlineData("report.pdf", "application/pdf", true)]
    [InlineData("notes.txt", "text/plain", true)]
    [InlineData("payload.exe", "application/octet-stream", false)]
    [InlineData("payload.exe", null, false)]
    public void IsAllowedUpload_UsesAllowList(string fileName, string? contentType, bool expected) =>
        Assert.Equal(expected, UploadSecurityPolicy.IsAllowedUpload(fileName, contentType));

    [Theory]
    [InlineData("uploads", true)]
    [InlineData(" SECTION-BACKGROUNDS ", true)]
    [InlineData("../uploads", false)]
    [InlineData("unknown", false)]
    [InlineData(null, false)]
    public void IsAllowedFolder_RejectsTraversalAndUnknownFolders(string? folder, bool expected) =>
        Assert.Equal(expected, UploadSecurityPolicy.IsAllowedFolder(folder));

    [Theory]
    [MemberData(nameof(SignatureCases))]
    public async Task SignatureValidation_RecognizesFileMagic(
        byte[] bytes,
        string fileName,
        string contentType,
        bool expected)
    {
        await using var stream = new MemoryStream(bytes);

        var result = await UploadSecurityPolicy.HasAllowedSignatureAsync(
            stream,
            fileName,
            contentType,
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task SignatureValidation_RejectsExtensionSpoofing()
    {
        await using var stream = new MemoryStream("not a png"u8.ToArray());

        var result = await UploadSecurityPolicy.HasAllowedSignatureAsync(
            stream,
            "photo.png",
            "image/png",
            TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    [Theory]
    [InlineData("image.png", "image/png", "image", true)]
    [InlineData("image.svg", "image/svg+xml", "image", false)]
    [InlineData("movie.mp4", "video/mp4", "video", true)]
    [InlineData("movie.exe", "video/mp4", "video", false)]
    [InlineData("report.pdf", "application/pdf", "file", true)]
    public void ManagedResourceUpload_RequiresKindAndConfiguredExtension(
        string fileName,
        string contentType,
        string kind,
        bool expected)
    {
        var settings = new ResourceLibrarySettings();

        Assert.Equal(expected, UploadSecurityPolicy.IsAllowedManagedResourceUpload(fileName, contentType, kind, settings));
    }

    [Fact]
    public async Task VideoSignature_RecognizesMp4FtypBox()
    {
        var bytes = new byte[] { 0, 0, 0, 20, (byte)'f', (byte)'t', (byte)'y', (byte)'p', 0, 0, 0, 0 };
        await using var stream = new MemoryStream(bytes);

        var result = await UploadSecurityPolicy.HasAllowedManagedResourceSignatureAsync(
            stream,
            "movie.mp4",
            "video/mp4",
            "video",
            TestContext.Current.CancellationToken);

        Assert.True(result);
    }

    public static TheoryData<byte[], string, string, bool> SignatureCases => new()
    {
        { [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a], "photo.png", "image/png", true },
        { [(byte)'%', (byte)'P', (byte)'D', (byte)'F', (byte)'-'], "report.pdf", "application/pdf", true },
        { [0x50, 0x4b, 0x03, 0x04], "document.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", true },
        { [(byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o'], "notes.txt", "text/plain", true },
        { [(byte)'a', 0x00, (byte)'b'], "notes.txt", "text/plain", false }
    };
}
