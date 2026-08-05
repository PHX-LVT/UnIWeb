using Contracts.Admin;
using Contracts.Auth;
using FullProject.Security;
using FullProject.Services;
using FullProject.Services.AssetService;
using FullProject.Settings;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace Core.UnitTests;

public sealed class ResourceUploadArchitectureTests
{
    [Theory]
    [InlineData(ResourceUploadContextCodes.BrandLogo, ResourceUploadContextPolicy.BrandRootKey)]
    [InlineData(ResourceUploadContextCodes.BrandFavicon, ResourceUploadContextPolicy.BrandRootKey)]
    [InlineData(ResourceUploadContextCodes.BrandFooter, ResourceUploadContextPolicy.BrandRootKey)]
    [InlineData(ResourceUploadContextCodes.BackgroundSection, ResourceUploadContextPolicy.BackgroundsRootKey)]
    [InlineData(ResourceUploadContextCodes.BackgroundBanner, ResourceUploadContextPolicy.BackgroundsRootKey)]
    [InlineData(ResourceUploadContextCodes.ContentManagementHero, ResourceUploadContextPolicy.ContentRootKey)]
    [InlineData(ResourceUploadContextCodes.ContentManagementThumbnail, ResourceUploadContextPolicy.ContentRootKey)]
    [InlineData(ResourceUploadContextCodes.ContentManagementBody, ResourceUploadContextPolicy.ContentRootKey)]
    [InlineData(ResourceUploadContextCodes.ContentManagementGallery, ResourceUploadContextPolicy.ContentRootKey)]
    [InlineData(ResourceUploadContextCodes.ContentManagementLegacy, ResourceUploadContextPolicy.ContentRootKey)]
    [InlineData(ResourceUploadContextCodes.ContentSectionHeroMedia, ResourceUploadContextPolicy.ContentRootKey)]
    [InlineData(ResourceUploadContextCodes.ContentSectionListItem, ResourceUploadContextPolicy.ContentRootKey)]
    [InlineData(ResourceUploadContextCodes.ContentSectionCarousel, ResourceUploadContextPolicy.ContentRootKey)]
    [InlineData(ResourceUploadContextCodes.ContentSectionHighlight, ResourceUploadContextPolicy.ContentRootKey)]
    [InlineData(ResourceUploadContextCodes.ContentSectionShowcase, ResourceUploadContextPolicy.ContentRootKey)]
    [InlineData(ResourceUploadContextCodes.ContentSectionGallery, ResourceUploadContextPolicy.ContentRootKey)]
    [InlineData(ResourceUploadContextCodes.ContentSectionLegacy, ResourceUploadContextPolicy.ContentRootKey)]
    [InlineData(ResourceUploadContextCodes.ContentBlockImage, ResourceUploadContextPolicy.ContentRootKey)]
    [InlineData(ResourceUploadContextCodes.ContentBlockCard, ResourceUploadContextPolicy.ContentRootKey)]
    [InlineData(ResourceUploadContextCodes.ContentBlockLegacy, ResourceUploadContextPolicy.ContentRootKey)]
    [InlineData(ResourceUploadContextCodes.LibraryManual, null)]
    public void UploadContexts_MapToExactlyThreeAutomaticRoots(string context, string? expectedRoot)
    {
        Assert.Equal(expectedRoot, ResourceUploadContextPolicy.RootSystemKeyFor(context));
    }

    [Fact]
    public void UploadContextCatalog_RejectsUnknownOrGenericContexts()
    {
        Assert.Null(ResourceUploadContextCodes.Normalize("content"));
        Assert.Null(ResourceUploadContextCodes.Normalize("background"));
        Assert.Null(ResourceUploadContextCodes.Normalize("misc.image"));
        Assert.Equal(ResourceUploadContextCodes.LibraryManual, ResourceUploadContextCodes.Normalize(null));
        Assert.Null(ResourceUploadContextCodes.NormalizeClient(null));
        Assert.Null(ResourceUploadContextCodes.NormalizeClient(""));
        Assert.Equal(
            ResourceUploadContextCodes.LibraryManual,
            ResourceUploadContextCodes.NormalizeClient(ResourceUploadContextCodes.LibraryManual));
    }

    [Theory]
    [InlineData(ResourceUploadContextCodes.ContentManagementLegacy)]
    [InlineData(ResourceUploadContextCodes.ContentSectionLegacy)]
    [InlineData(ResourceUploadContextCodes.ContentBlockLegacy)]
    public void LegacyOnlyContexts_CannotBeInitiatedByClients(string context)
    {
        Assert.Equal(context, ResourceUploadContextCodes.Normalize(context));
        Assert.Null(ResourceUploadContextCodes.NormalizeClient(context));
    }

    [Fact]
    public void UploadContextPermissions_AreSeparatedByAdministrativeDomain()
    {
        var settings = Principal(AdminPermissionKeys.ManageSettings);
        Assert.True(ResourceUploadContextPolicy.CanUse(settings, ResourceUploadContextCodes.BrandLogo));
        Assert.False(ResourceUploadContextPolicy.CanUse(settings, ResourceUploadContextCodes.BackgroundSection));
        Assert.False(ResourceUploadContextPolicy.CanUse(settings, ResourceUploadContextCodes.LibraryManual));

        var pageBuilder = Principal(AdminPermissionKeys.PageBuilder);
        Assert.True(ResourceUploadContextPolicy.CanUse(pageBuilder, ResourceUploadContextCodes.BackgroundSection));
        Assert.True(ResourceUploadContextPolicy.CanUse(pageBuilder, ResourceUploadContextCodes.ContentSectionHeroMedia));
        Assert.True(ResourceUploadContextPolicy.CanUse(pageBuilder, ResourceUploadContextCodes.ContentBlockImage));
        Assert.False(ResourceUploadContextPolicy.CanUse(pageBuilder, ResourceUploadContextCodes.BrandLogo));
        Assert.False(ResourceUploadContextPolicy.CanUse(pageBuilder, ResourceUploadContextCodes.ContentManagementHero));
        Assert.False(ResourceUploadContextPolicy.CanUse(pageBuilder, ResourceUploadContextCodes.LibraryManual));

        var contentAuthor = Principal(AdminPermissionKeys.CreateEditContent);
        Assert.True(ResourceUploadContextPolicy.CanUse(contentAuthor, ResourceUploadContextCodes.LibraryManual));
        Assert.True(ResourceUploadContextPolicy.CanUse(contentAuthor, ResourceUploadContextCodes.ContentManagementHero));
        Assert.False(ResourceUploadContextPolicy.CanUse(contentAuthor, ResourceUploadContextCodes.BrandLogo));
        Assert.False(ResourceUploadContextPolicy.CanUse(contentAuthor, ResourceUploadContextCodes.ContentSectionHeroMedia));

        var protectedAdmin = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("isAdminAdmin", "true")],
            "test"));
        Assert.All(
            ResourceUploadContextCodes.ClientInitiable,
            context => Assert.True(ResourceUploadContextPolicy.CanUse(protectedAdmin, context)));
    }

    [Fact]
    public void AutomaticUploadCatalog_UsesOnlyTheThreeProtectedRoots()
    {
        var roots = ResourceUploadContextCodes.All
            .Where(ResourceUploadContextCodes.IsAutomatic)
            .Select(context => ResourceUploadContextPolicy.RootSystemKeyFor(context))
            .Where(root => root is not null)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(3, roots.Count);
        Assert.Contains(ResourceUploadContextPolicy.BrandRootKey, roots);
        Assert.Contains(ResourceUploadContextPolicy.BackgroundsRootKey, roots);
        Assert.Contains(ResourceUploadContextPolicy.ContentRootKey, roots);
    }

    [Fact]
    public void ResourceLibraryBootstrap_DefinesExactlyTheThreeProtectedRootAlbums()
    {
        var service = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "AdminSite-API",
            "Services",
            "ManagedResourceServices",
            "ManagedResourceAlbumService.cs"));

        Assert.Equal(
            3,
            Regex.Matches(service, @"new\(ResourceUploadContextPolicy\.\w+RootKey").Count);
        Assert.Contains("BrandRootKey, \"Brand\"", service, StringComparison.Ordinal);
        Assert.Contains("BackgroundsRootKey, \"Backgrounds\"", service, StringComparison.Ordinal);
        Assert.Contains("ContentRootKey, \"Content\"", service, StringComparison.Ordinal);

        Assert.True(ManagedResourceAlbumService.IsReservedSystemRootName("Brand"));
        Assert.True(ManagedResourceAlbumService.IsReservedSystemRootName(" backgrounds "));
        Assert.True(ManagedResourceAlbumService.IsReservedSystemRootName("CONTENT"));
        Assert.False(ManagedResourceAlbumService.IsReservedSystemRootName("Campaign Images"));
    }

    [Theory]
    [InlineData("global", "branding", "branding", ResourceUploadContextCodes.BrandLogo)]
    [InlineData("global", "footer", "footer", ResourceUploadContextCodes.BrandFooter)]
    [InlineData("content", "content-item", "hero", ResourceUploadContextCodes.ContentManagementHero)]
    [InlineData("content", "content-item", "thumbnail", ResourceUploadContextCodes.ContentManagementThumbnail)]
    [InlineData("page-builder", "section", "background", ResourceUploadContextCodes.BackgroundSection)]
    [InlineData("page-builder", "section", "carousel", ResourceUploadContextCodes.ContentSectionCarousel)]
    [InlineData("page-builder", "section", "gallery", ResourceUploadContextCodes.ContentSectionGallery)]
    [InlineData("page-builder", "block", "image", ResourceUploadContextCodes.ContentBlockImage)]
    [InlineData("page-builder", "block", "card", ResourceUploadContextCodes.ContentBlockCard)]
    public void LegacyImageOwnership_MapsToPreciseManagedContext(
        string domain,
        string type,
        string role,
        string expected)
    {
        Assert.Equal(expected, LegacyManagedImageContextPolicy.Resolve(domain, type, role));
    }

    [Fact]
    public void EditorImageUploads_DoNotUseLegacyMultipartFolders()
    {
        var root = RepositoryRoot();
        var frontend = Path.Combine(root, "AdminSite-Frontend");
        var source = string.Join('\n', Directory
            .EnumerateFiles(frontend, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Select(File.ReadAllText));

        string[] retiredImageFolders =
        [
            "branding", "content-hero", "content-thumbnails", "content-body", "hero",
            "carousel", "showcase", "list-items", "highlights", "image-blocks", "card-blocks"
        ];
        foreach (var folder in retiredImageFolders)
            Assert.DoesNotContain($"api/admin/assets/upload?folder={folder}", source, StringComparison.OrdinalIgnoreCase);

        var expectedContextsByFile = new Dictionary<string, string[]>
        {
            ["Components/Pages/Global/Branding.razor"] = [nameof(ResourceUploadContextCodes.BrandLogo)],
            ["Components/Pages/Content/ContentEditorShell.razor"] =
            [
                nameof(ResourceUploadContextCodes.LibraryManual),
                nameof(ResourceUploadContextCodes.ContentManagementHero),
                nameof(ResourceUploadContextCodes.ContentManagementThumbnail),
                nameof(ResourceUploadContextCodes.ContentManagementBody),
                nameof(ResourceUploadContextCodes.ContentManagementGallery)
            ],
            ["Components/Pages/Content/ContentResources.razor"] = [nameof(ResourceUploadContextCodes.LibraryManual)],
            ["Components/Pages/SectionEditors/SectionDesignTab.razor"] = [nameof(ResourceUploadContextCodes.BackgroundSection)],
            ["Components/Pages/SectionEditors/HeroSectionEditor.razor"] = [nameof(ResourceUploadContextCodes.ContentSectionHeroMedia)],
            ["Components/Pages/SectionEditors/ListSectionEditor.razor"] = [nameof(ResourceUploadContextCodes.ContentSectionListItem)],
            ["Components/Pages/SectionEditors/CarouselSectionEditor.razor"] = [nameof(ResourceUploadContextCodes.ContentSectionCarousel)],
            ["Components/Pages/SectionEditors/TestimonialSectionEditor.razor"] = [nameof(ResourceUploadContextCodes.ContentSectionHighlight)],
            ["Components/Pages/SectionEditors/ShowcaseSectionEditor.razor"] = [nameof(ResourceUploadContextCodes.ContentSectionShowcase)],
            ["Components/Pages/BlockEditors/ImageBlockEditor.razor"] = [nameof(ResourceUploadContextCodes.ContentBlockImage)],
            ["Components/Pages/BlockEditors/DesignBlockEditor.razor"] = [nameof(ResourceUploadContextCodes.ContentBlockCard)]
        };
        foreach (var (relativePath, contextSymbols) in expectedContextsByFile)
        {
            var fileSource = File.ReadAllText(Path.Combine(
                frontend,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            foreach (var contextSymbol in contextSymbols)
                Assert.Contains($"ResourceUploadContextCodes.{contextSymbol}", fileSource, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ResourceUploadFailures_PreserveTheirStructuredErrorCode()
    {
        var controller = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "AdminSite-API",
            "Controllers",
            "ResourceUploadsController.cs"));

        Assert.Contains(".WithOutcome(exception.Code)", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void CompletedUploadRecovery_ReconcilesRegistryAndReplacementCleanup()
    {
        var root = RepositoryRoot();
        var sessions = File.ReadAllText(Path.Combine(
            root,
            "AdminSite-API",
            "Services",
            "ManagedResourceServices",
            "ResourceUploadSessionService.cs"));
        var storedAssets = File.ReadAllText(Path.Combine(
            root,
            "AdminSite-API",
            "Services",
            "AssetService",
            "StoredAssetService.cs"));

        Assert.Contains("reconciliation-required", sessions, StringComparison.Ordinal);
        Assert.Contains("ReconcileStoredAssetRegistryAsync", sessions, StringComparison.Ordinal);
        Assert.Contains("ReconcileUploadReplacementAsync", sessions, StringComparison.Ordinal);
        Assert.Contains("new UpdateOptions { IsUpsert = true }", storedAssets, StringComparison.Ordinal);
    }

    [Fact]
    public void PendingKeys_AreIsolatedAndDoNotReuseOriginalNames()
    {
        var storage = Storage();

        var key = storage.CreatePendingKey("session-123", "Quarterly Report.PDF");

        Assert.StartsWith("cms/temporary/uploads/session-123/", key);
        Assert.EndsWith(".pdf", key);
        Assert.DoesNotContain("Quarterly", key, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResourceKeys_UseStableIdentityVersionAndReadableName()
    {
        var settings = Options.Create(Settings());
        var policy = new AssetStorageKeyPolicy(settings);

        var key = policy.CreateResourceKey(
            "66b0a82f524cbe51f129d347",
            3,
            "image",
            "Logistics Conference 2026",
            "camera-original.JPG");

        Assert.Equal(
            "cms/resource-library/media/66b0a82f524cbe51f129d347-v3-logistics-conference-2026.jpg",
            key);
        Assert.DoesNotMatch(@"/\d{4}/\d{2}/\d{2}/", key);
    }

    [Fact]
    public void AlbumCoverKeys_AreOwnedByAlbumAndRemainHumanNavigable()
    {
        var policy = new AssetStorageKeyPolicy(Options.Create(Settings()));

        var key = policy.CreateAlbumCoverKey(
            "66b0a82f524cbe51f129d347",
            "66b0a82f524cbe51f129d348",
            2,
            "Logistics Team Cover.PNG");

        Assert.Equal(
            "cms/resource-library/album-covers/66b0a82f524cbe51f129d347/66b0a82f524cbe51f129d348-v2-logistics-team-cover.png",
            key);
        Assert.DoesNotMatch(@"/\d{4}/\d{2}/\d{2}/", key);
    }

    [Fact]
    public void LegacyUploadFolders_MapToOwnershipDomainsWithoutDates()
    {
        var settings = Options.Create(Settings());
        var policy = new AssetStorageKeyPolicy(settings);

        var content = policy.CreateMappedLegacyUploadKey(
            "content-hero",
            "66b0a82f524cbe51f129d347",
            1,
            "Hero Photo.png");
        var section = policy.CreateMappedLegacyUploadKey(
            "highlights",
            "66b0a82f524cbe51f129d348",
            1,
            "Proof Icon.svg");

        Assert.StartsWith("cms/content/content-item/unassigned/hero/", content);
        Assert.StartsWith("cms/page-builder/section/unassigned/highlight/", section);
        Assert.DoesNotMatch(@"/\d{4}/\d{2}/\d{2}/", content);
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
    public async Task ListObjectsAsync_ParsesInventoryMetadata()
    {
        const string xml = """
            <ListBucketResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/">
              <IsTruncated>false</IsTruncated>
              <Contents>
                <Key>cms/resource-library/media/item-v1-photo.jpg</Key>
                <LastModified>2026-08-04T10:00:00.000Z</LastModified>
                <ETag>&quot;abc&quot;</ETag>
                <Size>2048</Size>
              </Contents>
            </ListBucketResult>
            """;
        var handler = new StaticResponseHandler(xml);
        var storage = Storage(new HttpClient(handler));

        var result = await storage.ListObjectsAsync("cms/", cancellationToken: TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Objects);
        Assert.False(result.Truncated);
        Assert.Equal("cms/resource-library/media/item-v1-photo.jpg", item.Key);
        Assert.Equal(2048, item.SizeBytes);
        Assert.Contains("list-type=2", handler.RequestUri!.Query);
        Assert.Contains("prefix=cms%2F", handler.RequestUri.Query);
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
        Options.Create(Settings()),
        new AssetStorageKeyPolicy(Options.Create(Settings())));

    private static R2StorageSettings Settings() => new()
        {
            AccountId = "account",
            AccessKeyId = "access",
            SecretAccessKey = "secret",
            BucketName = "bucket",
            PublicBaseUrl = "https://public.example",
            KeyPrefix = "cms"
        };

    private static ClaimsPrincipal Principal(params string[] permissions) => new(
        new ClaimsIdentity(
            permissions.Select(permission => new Claim("permission", permission)),
            "test"));

    private static void WriteBigEndian(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)(value >> 24);
        bytes[offset + 1] = (byte)(value >> 16);
        bytes[offset + 2] = (byte)(value >> 8);
        bytes[offset + 3] = (byte)value;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "AdminSite-API")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
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

    private sealed class StaticResponseHandler(string body) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            });
        }
    }
}
