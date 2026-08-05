using AdminSite.Models;
using Contracts.Admin;
using Contracts.Api;
using Microsoft.JSInterop;

namespace AdminSite.Services;

public sealed class DirectResourceUploadService
{
    private readonly AdminContentService _content;
    private readonly IJSRuntime _js;
    private readonly ILogger<DirectResourceUploadService> _logger;

    public DirectResourceUploadService(
        AdminContentService content,
        IJSRuntime js,
        ILogger<DirectResourceUploadService> logger)
    {
        _content = content;
        _js = js;
        _logger = logger;
    }

    public async Task<ApiResponse<ManagedResourceModel>> UploadInputAsync(
        string inputId,
        string kind,
        string uploadContext,
        string? albumId = null,
        string? resourceName = null,
        string? replaceResourceId = null)
    {
        BrowserCapturedFile? browserFile = null;
        string? sessionId = null;
        var sessionCompleted = false;
        var requestId = Guid.NewGuid().ToString("N");
        try
        {
            var capture = await _js.InvokeAsync<BrowserCaptureBatch>("resourceUploadClient.captureFiles", inputId, 1);
            browserFile = capture.Files.FirstOrDefault();
            if (browserFile is null)
                return UploadFailure("Upload failed: choose a file.", 422, "validation-failed");

            var normalizedUploadContext = ResourceUploadContextCodes.NormalizeClient(uploadContext);
            if (normalizedUploadContext is null)
                return UploadFailure("Upload failed: the upload context is not supported.", 422, "validation-failed");

            var capabilities = await _content.GetResourceUploadCapabilitiesAsync();
            if (!capabilities.Success || capabilities.Data is null || !capabilities.Data.DirectUploadEnabled)
                return UploadFailure(
                    FailureMessage(capabilities, "Upload failed: storage is not available."),
                    capabilities.StatusCode > 0 ? capabilities.StatusCode : 503,
                    capabilities.ErrorCode ?? "service-unavailable");

            var initiated = await RetryAsync(
                () => _content.InitiateResourceUploadAsync(new ResourceUploadInitiateRequest
                {
                    FileName = browserFile.Name,
                    ResourceName = string.IsNullOrWhiteSpace(resourceName)
                        ? Path.GetFileNameWithoutExtension(browserFile.Name)
                        : resourceName.Trim(),
                    Kind = kind,
                    ContentType = NormalizeContentType(browserFile.ContentType),
                    SizeBytes = browserFile.Size,
                    AlbumId = albumId,
                    ReplaceResourceId = replaceResourceId,
                    UploadContext = normalizedUploadContext
                }));
            if (!initiated.Success || initiated.Data is null)
                return UploadFailure(
                    FailureMessage(initiated, "Upload failed: storage could not start the transfer."),
                    initiated.StatusCode,
                    initiated.ErrorCode ?? "initiation-failed");

            var upload = initiated.Data;
            sessionId = upload.SessionId;
            var completedParts = new List<ResourceUploadCompletedPartDto>();
            if (string.Equals(upload.Mode, "multipart", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var batch in Enumerable.Range(1, upload.PartCount).Chunk(100))
                {
                    var urls = await RetryAsync(() => _content.GetResourceUploadPartUrlsAsync(upload.SessionId, batch));
                    if (!urls.Success || urls.Data is null)
                        return UploadFailure(
                            FailureMessage(urls, "Upload failed: storage could not prepare the file parts."),
                            urls.StatusCode,
                            urls.ErrorCode ?? "service-unavailable");

                    foreach (var part in urls.Data.Parts.OrderBy(value => value.PartNumber))
                    {
                        var start = (part.PartNumber - 1L) * upload.PartSizeBytes;
                        var end = Math.Min(browserFile.Size, start + upload.PartSizeBytes);
                        var result = await _js.InvokeAsync<BrowserUploadResult>(
                            "resourceUploadClient.upload",
                            browserFile.Token,
                            part.UploadUrl,
                            upload.RequiredHeaders,
                            requestId,
                            null,
                            start,
                            end,
                            UploadTimeoutMilliseconds(end - start));
                        if (string.IsNullOrWhiteSpace(result.ETag))
                            return UploadFailure(
                                "Upload failed: storage could not verify a file part.",
                                422,
                                "verification-failed");
                        completedParts.Add(new ResourceUploadCompletedPartDto
                        {
                            PartNumber = part.PartNumber,
                            ETag = result.ETag
                        });
                    }
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(upload.UploadUrl))
                    return UploadFailure(
                        "Upload failed: storage did not provide a transfer URL.",
                        502,
                        "service-unavailable");
                await _js.InvokeAsync<BrowserUploadResult>(
                    "resourceUploadClient.upload",
                    browserFile.Token,
                    upload.UploadUrl,
                    upload.RequiredHeaders,
                    requestId,
                    null,
                    null,
                    null,
                    UploadTimeoutMilliseconds(browserFile.Size));
            }

            var completed = await RetryAsync(() => _content.CompleteResourceUploadAsync(upload.SessionId, completedParts));
            if (!completed.Success || completed.Data?.Status != "ready" || completed.Data.Resource is null)
                return UploadFailure(
                    !string.IsNullOrWhiteSpace(completed.Data?.ErrorMessage)
                        ? completed.Data.ErrorMessage
                        : FailureMessage(completed, "Upload failed: the file could not be verified."),
                    completed.StatusCode,
                    completed.Data?.ErrorCode ?? completed.ErrorCode ?? "verification-failed");

            sessionCompleted = true;
            return new ApiResponse<ManagedResourceModel>
            {
                Success = true,
                StatusCode = 200,
                Message = string.IsNullOrWhiteSpace(replaceResourceId) ? "Resource uploaded." : "Resource file replaced.",
                Data = Map(completed.Data.Resource)
            };
        }
        catch (JSException exception)
        {
            _logger.LogWarning(exception, "Browser-to-storage resource upload failed.");
            return UploadFailure(
                "Upload failed: the storage service is temporarily unavailable.",
                502,
                "service-unavailable");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected direct resource upload failure.");
            return UploadFailure(
                "Upload failed: an unexpected system error occurred.",
                500,
                "unexpected-error");
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(sessionId) && !sessionCompleted)
            {
                try { await _content.AbortResourceUploadAsync(sessionId); }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Resource upload session {SessionId} could not be aborted during cleanup.", sessionId);
                }
            }
            if (browserFile is not null && !string.IsNullOrWhiteSpace(browserFile.Token))
            {
                try { await _js.InvokeVoidAsync("resourceUploadClient.release", browserFile.Token); }
                catch (JSDisconnectedException) { }
                catch (JSException) { }
            }
        }
    }

    private static async Task<ApiResponse<T>> RetryAsync<T>(Func<Task<ApiResponse<T>>> operation)
    {
        ApiResponse<T>? response = null;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            response = await operation();
            if (response.StatusCode != 429) return response;
            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(response.RetryAfterSeconds ?? attempt + 1, 1, 30)));
        }
        return response ?? ApiResponse<T>.Fail("Request failed.", 500);
    }

    private static int UploadTimeoutMilliseconds(long bytes) =>
        (int)Math.Clamp(60_000d + bytes / (256d * 1024) * 1000d, 120_000d, 30 * 60_000d);

    private static string FailureMessage<T>(ApiResponse<T>? response, string fallback)
    {
        var message = response?.Message?.Trim();
        return string.IsNullOrWhiteSpace(message) ||
               string.Equals(message, "Request failed.", StringComparison.OrdinalIgnoreCase)
            ? fallback
            : message;
    }

    private static ApiResponse<ManagedResourceModel> UploadFailure(
        string message,
        int statusCode,
        string errorCode) =>
        ApiResponse<ManagedResourceModel>.Fail(
            message,
            statusCode,
            notificationKey: "NotificationActionFailed",
            notificationArgs: ["@action:resource.uploaded", $"@reason:{errorCode}"],
            errorCode: errorCode,
            traceId: Guid.NewGuid().ToString("N"));

    private static string NormalizeContentType(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "application/octet-stream" : value.Trim();

    public static ManagedResourceModel Map(ResourceUploadResourceDto resource) => new()
    {
        Id = resource.Id,
        AssetId = resource.AssetId,
        AssetVersion = resource.AssetVersion,
        StorageSchemaVersion = resource.StorageSchemaVersion,
        Kind = resource.Kind,
        Purpose = resource.Purpose,
        Name = new Dictionary<string, string>(resource.Name),
        Description = new Dictionary<string, string>(resource.Description),
        Url = resource.Url,
        StorageKey = resource.StorageKey,
        ThumbnailUrl = resource.ThumbnailUrl,
        FileName = resource.FileName,
        ContentType = resource.ContentType,
        SizeBytes = resource.SizeBytes,
        Source = resource.Source,
        OriginalSourceUrl = resource.OriginalSourceUrl,
        LicenseName = resource.LicenseName,
        Attribution = resource.Attribution,
        DeletionState = resource.DeletionState,
        Tags = [.. resource.Tags],
        AlbumId = resource.AlbumId,
        OriginContext = resource.OriginContext,
        Active = resource.Active,
        UsageCount = resource.UsageCount,
        IsInUse = resource.IsInUse,
        CreatedById = resource.CreatedById,
        UpdatedById = resource.UpdatedById,
        CreatedAt = resource.CreatedAt,
        UpdatedAt = resource.UpdatedAt
    };

    private sealed class BrowserCaptureBatch
    {
        public List<BrowserCapturedFile> Files { get; set; } = [];
    }

    private sealed class BrowserCapturedFile
    {
        public string Token { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public long Size { get; set; }
        public string ContentType { get; set; } = string.Empty;
    }

    private sealed class BrowserUploadResult
    {
        public string ETag { get; set; } = string.Empty;
    }
}
