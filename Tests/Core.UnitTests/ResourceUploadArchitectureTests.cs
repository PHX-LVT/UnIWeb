using FullProject.Security;
using FullProject.Services.AssetService;
using FullProject.Settings;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;

namespace Core.UnitTests;

public sealed class ResourceUploadArchitectureTests
{
    [Fact]
    public void PendingKeys_AreIsolatedAndDoNotReuseOriginalNames()
    {
        var storage = Storage();

        var key = storage.CreatePendingKey("session-123", "Quarterly Report.PDF");

        Assert.StartsWith("cms/pending/session-123/", key);
        Assert.EndsWith(".pdf", key);
        Assert.DoesNotContain("Quarterly", key, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PresignedPutUrl_IsBoundToBucketKeyAndExpires()
    {
        var storage = Storage();

        var url = storage.CreatePresignedPutUrl("cms/pending/session/file.png", TimeSpan.FromMinutes(10));
        var text = url.ToString();

        Assert.Contains("account.r2.cloudflarestorage.com/bucket/cms/pending/session/file.png", text);
        Assert.Contains("X-Amz-Algorithm=AWS4-HMAC-SHA256", text);
        Assert.Contains("X-Amz-Expires=600", text);
        Assert.Contains("X-Amz-Signature=", text);
        Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PresignedPutUrl_BindsTheDeclaredContentType()
    {
        var storage = Storage();

        var pngUrl = storage.CreatePresignedPutUrl(
            "cms/pending/session/file.png",
            TimeSpan.FromMinutes(10),
            contentType: "image/png");
        var jpegUrl = storage.CreatePresignedPutUrl(
            "cms/pending/session/file.png",
            TimeSpan.FromMinutes(10),
            contentType: "image/jpeg");

        Assert.Contains("X-Amz-SignedHeaders=content-type%3Bhost", pngUrl.Query);
        Assert.NotEqual(pngUrl.Query, jpegUrl.Query);
    }

    [Fact]
    public async Task ReadPrefixAsync_DoesNotTrustAnOriginThatIgnoresRange()
    {
        var handler = new IgnoredRangeHandler(new byte[16 * 1024]);
        var storage = Storage(new HttpClient(handler));

        var prefix = await storage.ReadPrefixAsync(
            "cms/pending/session/file.bin",
            512,
            TestContext.Current.CancellationToken);

        Assert.Equal(512, prefix.Length);
        Assert.Equal(new RangeHeaderValue(0, 511), handler.Range);
    }

    [Fact]
    public void ImageInspection_RejectsDecompressionBombDimensions()
    {
        var png = new byte[24];
        new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }.CopyTo(png, 0);
        WriteBigEndian(png, 16, 20_000);
        WriteBigEndian(png, 20, 20_000);

        var safe = ResourceUploadInspection.HasSafeImageDimensions(png, "large.png", out var error);

        Assert.False(safe);
        Assert.Contains("100 megapixel", error);
    }

    [Fact]
    public void ImageInspection_AcceptsNormalPngDimensions()
    {
        var png = new byte[24];
        WriteBigEndian(png, 16, 1920);
        WriteBigEndian(png, 20, 1080);

        Assert.True(ResourceUploadInspection.HasSafeImageDimensions(png, "normal.png", out var error));
        Assert.Null(error);
    }

    [Fact]
    public void ImageInspection_RejectsAnImageWhoseDimensionsCannotBeVerified()
    {
        var safe = ResourceUploadInspection.HasSafeImageDimensions(new byte[24], "broken.jpg", out var error);

        Assert.False(safe);
        Assert.Contains("could not be verified", error);
    }

    private static R2StorageService Storage(HttpClient? httpClient = null) => new(
        httpClient ?? new HttpClient(),
        Options.Create(new R2StorageSettings
        {
            AccountId = "account",
            AccessKeyId = "access",
            SecretAccessKey = "secret",
            BucketName = "bucket",
            PublicBaseUrl = "https://public.example",
            KeyPrefix = "cms"
        }));

    private static void WriteBigEndian(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)(value >> 24);
        bytes[offset + 1] = (byte)(value >> 16);
        bytes[offset + 2] = (byte)(value >> 8);
        bytes[offset + 3] = (byte)value;
    }

    private sealed class IgnoredRangeHandler(byte[] content) : HttpMessageHandler
    {
        public RangeHeaderValue? Range { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Range = request.Headers.Range;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
            });
        }
    }
}
