using Contracts.Admin;
using FullProject.Data;
using FullProject.DTOs;
using FullProject.Models;
using FullProject.Security;
using FullProject.Services.AssetService;
using FullProject.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FullProject.Services;

public sealed class ResourceUploadSessionService
{
    private static readonly HashSet<string> TerminalStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "ready", "failed", "cancelled", "expired"
    };

    private readonly MongoDbContext _context;
    private readonly ManagedResourceService _resources;
    private readonly ManagedResourceAlbumService _albums;
    private readonly SettingsService _siteSettings;
    private readonly R2StorageService _storage;
    private readonly StoredAssetService _storedAssets;
    private readonly R2StorageSettings _settings;
    private readonly ILogger<ResourceUploadSessionService> _logger;

    public ResourceUploadSessionService(
        MongoDbContext context,
        ManagedResourceService resources,
        ManagedResourceAlbumService albums,
        SettingsService siteSettings,
        R2StorageService storage,
        StoredAssetService storedAssets,
        IOptions<R2StorageSettings> settings,
        ILogger<ResourceUploadSessionService> logger)
    {
        _context = context;
        _resources = resources;
        _albums = albums;
        _siteSettings = siteSettings;
        _storage = storage;
        _storedAssets = storedAssets;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<ResourceUploadCapabilitiesDto> GetCapabilitiesAsync()
    {
        var library = await _siteSettings.GetResourceLibrarySettingsAsync();
        return new ResourceUploadCapabilitiesDto
        {
            DirectUploadEnabled = _settings.DirectUploadEnabled && _settings.IsConfigured,
            MaxImageBytes = Math.Min(library.MaxImageBytes, _settings.MaxUploadBytes),
            MaxFileBytes = Math.Min(library.MaxFileBytes, _settings.MaxUploadBytes),
            MaxVideoBytes = Math.Min(library.MaxVideoBytes, _settings.MaxUploadBytes),
            AllowedImageFormats = [.. library.AllowedImageFormats],
            AllowedFileFormats = [.. library.AllowedFileFormats],
            AllowedVideoFormats = [.. library.AllowedVideoFormats],
            MultipartThresholdBytes = Math.Max(5L * 1024 * 1024, _settings.MultipartThresholdBytes),
            MultipartPartSizeBytes = Math.Max(5L * 1024 * 1024, _settings.MultipartPartSizeBytes),
            MaxConcurrentUploads = Math.Clamp(_settings.MaxConcurrentUploads, 1, 6),
            PendingLifetimeMinutes = Math.Clamp(_settings.PendingUploadMinutes, 10, 24 * 60)
        };
    }

    public async Task<ResourceUploadInitiateResponse> InitiateAsync(
        ResourceUploadInitiateRequest request,
        string actorId,
        CancellationToken cancellationToken)
    {
        if (!_settings.DirectUploadEnabled)
            throw new ResourceUploadException("direct_upload_disabled", "Direct browser uploads are not enabled.", StatusCodes.Status409Conflict);
        if (!_settings.IsConfigured)
            throw new ResourceUploadException("storage_unavailable", "R2 storage is not configured.", StatusCodes.Status503ServiceUnavailable);

        var uploadContext = ResourceUploadContextCodes.NormalizeClient(request.UploadContext);
        if (uploadContext is null)
            throw Validation("Upload context is not supported.");

        var library = await _siteSettings.GetResourceLibrarySettingsAsync();
        var kind = _resources.NormalizeKind(request.Kind);
        if (kind is null)
            throw Validation("Resource kind must be image, file, or video.");

        var fileName = Path.GetFileName(request.FileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255)
            throw Validation("A valid file name is required.");
        var inferredKind = ManagedResourceService.InferKindFromUpload(fileName, request.ContentType);
        if (!string.Equals(kind, inferredKind, StringComparison.OrdinalIgnoreCase))
            throw Validation($"Uploaded file does not match the selected {kind} resource type.");
        if (ResourceUploadContextCodes.IsAutomatic(uploadContext) &&
            !string.Equals(kind, "image", StringComparison.OrdinalIgnoreCase))
        {
            throw Validation("Automatic editor uploads currently accept images only.");
        }
        if (request.SizeBytes <= 0)
            throw Validation("The upload is empty.");

        var maximum = kind switch
        {
            "image" => library.MaxImageBytes,
            "video" => library.MaxVideoBytes,
            _ => library.MaxFileBytes
        };
        maximum = Math.Min(maximum, _settings.MaxUploadBytes);
        if (request.SizeBytes > maximum)
            throw Validation($"{DisplayKind(kind)} resources must be {maximum / 1024 / 1024}MB or smaller.");
        if (!UploadSecurityPolicy.IsAllowedManagedResourceUpload(fileName, request.ContentType, kind, library))
            throw Validation(UploadSecurityPolicy.UnsupportedManagedResourceUploadMessage);

        ManagedResource? replacement = null;
        if (!string.IsNullOrWhiteSpace(request.ReplaceResourceId))
        {
            if (!string.Equals(uploadContext, ResourceUploadContextCodes.LibraryManual, StringComparison.Ordinal))
                throw Validation("Editor uploads create a new Resource Library item and cannot replace an existing resource.");
            if (!ObjectId.TryParse(request.ReplaceResourceId, out _)) throw Validation("Resource not found.");
            replacement = await _resources.GetByIdAsync(request.ReplaceResourceId);
            if (replacement is null) throw new ResourceUploadException("resource_not_found", "Resource not found.", StatusCodes.Status404NotFound);
            if (string.Equals(replacement.Purpose, "custom-icon", StringComparison.OrdinalIgnoreCase))
                throw Validation("Custom Icons must be replaced through the protected Custom Icon upload flow.");
            if (!string.Equals(replacement.Kind, kind, StringComparison.OrdinalIgnoreCase))
                throw Validation($"Replacement file must be a {replacement.Kind} resource.");

            var replacementUploadActive = await _context.ResourceUploadSessions.Find(item =>
                    item.ReplaceResourceId == replacement.Id &&
                    new[] { "initiated", "verifying", "cancelling" }.Contains(item.Status) &&
                    item.ExpiresAtUtc > DateTime.UtcNow)
                .AnyAsync(cancellationToken);
            if (replacementUploadActive)
                throw new ResourceUploadException(
                    "replacement_in_progress",
                    "A replacement upload for this resource is already active.",
                    StatusCodes.Status409Conflict);
        }

        var now = DateTime.UtcNow;
        var activeStatuses = new[] { "initiated", "verifying", "cancelling" };
        var activeSessions = await _context.ResourceUploadSessions.CountDocumentsAsync(
            item => item.ActorId == actorId && activeStatuses.Contains(item.Status) && item.ExpiresAtUtc > now,
            cancellationToken: cancellationToken);
        if (activeSessions >= Math.Clamp(_settings.MaxActiveUploadSessionsPerAdmin, 2, 100))
            throw new ResourceUploadException(
                "too_many_active_uploads",
                "Too many uploads are already active. Wait for an upload to finish or cancel one before retrying.",
                StatusCodes.Status429TooManyRequests);

        string? requestedAlbumId;
        if (ResourceUploadContextCodes.IsAutomatic(uploadContext))
        {
            if (!string.IsNullOrWhiteSpace(request.AlbumId))
                throw Validation("The album for this upload context is assigned automatically.");
            requestedAlbumId = (await _albums.ResolveSystemRootAsync(uploadContext, "system", cancellationToken)).Id;
        }
        else
        {
            requestedAlbumId = replacement?.AlbumId ?? request.AlbumId;
        }

        if (!string.IsNullOrWhiteSpace(requestedAlbumId))
        {
            if (!ObjectId.TryParse(requestedAlbumId, out _)) throw Validation("Album not found.");
            var album = await _albums.GetByIdAsync(requestedAlbumId);
            if (album is null) throw Validation("Album not found.");
            if (!string.Equals(album.Scope, ManagedResourceAlbumService.ScopeForKind(kind), StringComparison.OrdinalIgnoreCase))
                throw Validation("The selected album does not accept this resource type.");
        }

        var capabilities = await GetCapabilitiesAsync();
        var lifetime = TimeSpan.FromMinutes(Math.Clamp(_settings.PresignedUrlMinutes, 5, capabilities.PendingLifetimeMinutes));
        var uploadUrlExpiresAtUtc = now.Add(lifetime);
        var resourceId = replacement?.Id ?? ObjectId.GenerateNewId().ToString();
        var assetId = ObjectId.GenerateNewId().ToString();
        var assetVersion = replacement is null ? 1 : Math.Max(1, replacement.AssetVersion) + 1;
        var resourceName = replacement?.Name.GetValueOrDefault("en") ?? CleanResourceName(request.ResourceName, fileName);
        var session = new ResourceUploadSession
        {
            Id = ObjectId.GenerateNewId().ToString(),
            ActorId = actorId,
            Kind = kind,
            FileName = fileName,
            ResourceName = resourceName,
            UploadContext = uploadContext,
            ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType.Trim(),
            SizeBytes = request.SizeBytes,
            AlbumId = string.IsNullOrWhiteSpace(requestedAlbumId) ? null : requestedAlbumId.Trim(),
            ReplaceResourceId = replacement?.Id,
            ReservedResourceId = resourceId,
            AssetId = assetId,
            AssetVersion = assetVersion,
            StorageSchemaVersion = Math.Max(1, _settings.StorageSchemaVersion),
            Mode = request.SizeBytes >= capabilities.MultipartThresholdBytes ? "multipart" : "single",
            Status = "initiated",
            PresignedExpiresAtUtc = uploadUrlExpiresAtUtc,
            CleanupAfterUtc = uploadUrlExpiresAtUtc.AddMinutes(1),
            CreatedAt = now,
            UpdatedAt = now,
            ExpiresAtUtc = now.AddMinutes(capabilities.PendingLifetimeMinutes)
        };
        session.PendingStorageKey = _storage.CreatePendingKey(session.Id, session.FileName);
        session.FinalStorageKey = _storage.CreateResourceFinalKey(
            resourceId,
            assetVersion,
            kind,
            resourceName,
            fileName);
        await _context.ResourceUploadSessions.InsertOneAsync(session, cancellationToken: cancellationToken);

        try
        {
            if (session.Mode == "multipart")
            {
                session.MultipartUploadId = await _storage.InitiateMultipartUploadAsync(
                    session.PendingStorageKey,
                    session.ContentType,
                    cancellationToken);
                session.UpdatedAt = DateTime.UtcNow;
                await SaveAsync(session, cancellationToken);
            }

            var partSize = capabilities.MultipartPartSizeBytes;
            var partCount = session.Mode == "multipart" ? checked((int)Math.Ceiling(session.SizeBytes / (double)partSize)) : 1;
            if (partCount > 10_000)
                throw Validation("The upload would require too many parts.");

            return new ResourceUploadInitiateResponse
            {
                SessionId = session.Id,
                Mode = session.Mode,
                UploadUrl = session.Mode == "single"
                    ? _storage.CreatePresignedPutUrl(
                        session.PendingStorageKey,
                        lifetime,
                        contentType: session.ContentType).ToString()
                    : null,
                RequiredHeaders = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["Content-Type"] = session.ContentType
                },
                ExpiresAtUtc = session.ExpiresAtUtc,
                UploadUrlExpiresAtUtc = uploadUrlExpiresAtUtc,
                PartSizeBytes = partSize,
                PartCount = partCount
            };
        }
        catch (Exception exception)
        {
            if (!string.IsNullOrWhiteSpace(session.MultipartUploadId))
            {
                try { await _storage.AbortMultipartUploadAsync(session.PendingStorageKey, session.MultipartUploadId, cancellationToken); }
                catch (Exception cleanupException) { _logger.LogWarning(cleanupException, "Could not abort failed multipart initiation {SessionId}", session.Id); }
            }
            await SafeDeleteAsync(session.PendingStorageKey, cancellationToken);
            _logger.LogError(exception, "Resource upload initiation failed for Session {SessionId}", session.Id);
            await FailAsync(session, "initiation_failed", "Upload initialization failed: storage is unavailable.", cancellationToken);
            throw;
        }
    }

    public async Task<ResourceUploadPartUrlsResponse> CreatePartUrlsAsync(
        string sessionId,
        ResourceUploadPartUrlsRequest request,
        string actorId,
        CancellationToken cancellationToken)
    {
        var session = await GetOwnedAsync(sessionId, actorId, cancellationToken);
        EnsureActive(session);
        if (session.Mode != "multipart" || string.IsNullOrWhiteSpace(session.MultipartUploadId))
            throw Validation("This upload does not use multipart transfer.");

        var capabilities = await GetCapabilitiesAsync();
        var partCount = checked((int)Math.Ceiling(session.SizeBytes / (double)capabilities.MultipartPartSizeBytes));
        var numbers = request.PartNumbers.Distinct().Order().ToList();
        if (numbers.Count == 0 || numbers.Count > 100 || numbers.Any(number => number < 1 || number > partCount))
            throw Validation("One or more requested upload parts are invalid.");

        var lifetime = TimeSpan.FromMinutes(Math.Clamp(_settings.PresignedUrlMinutes, 5, capabilities.PendingLifetimeMinutes));
        var urlExpiresAtUtc = DateTime.UtcNow.Add(lifetime);
        if (session.PresignedExpiresAtUtc is null || session.PresignedExpiresAtUtc < urlExpiresAtUtc)
        {
            session.PresignedExpiresAtUtc = urlExpiresAtUtc;
            session.CleanupAfterUtc = urlExpiresAtUtc.AddMinutes(1);
            session.UpdatedAt = DateTime.UtcNow;
            await SaveAsync(session, cancellationToken);
        }
        return new ResourceUploadPartUrlsResponse
        {
            ExpiresAtUtc = urlExpiresAtUtc,
            Parts = numbers.Select(number => new ResourceUploadPartUrlDto
            {
                PartNumber = number,
                UploadUrl = _storage.CreatePresignedPartUrl(
                    session.PendingStorageKey,
                    session.MultipartUploadId,
                    number,
                    lifetime,
                    session.ContentType).ToString()
            }).ToList()
        };
    }

    public async Task<ResourceUploadSessionDto> CompleteAsync(
        string sessionId,
        ResourceUploadCompleteRequest request,
        string actorId,
        CancellationToken cancellationToken)
    {
        var session = await GetOwnedAsync(sessionId, actorId, cancellationToken);
        if (string.Equals(session.Status, "reconciliation-required", StringComparison.OrdinalIgnoreCase))
            return await ReconcileStoredAssetRegistryAsync(session, actorId, cancellationToken);
        if (session.Status == "ready")
            return await ReconcileStoredAssetRegistryAsync(session, actorId, cancellationToken);
        if (string.Equals(session.Status, "verifying", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(session.FinalStorageKey))
        {
            var persistedResource = await _context.ManagedResources
                .Find(resource => resource.StorageKey == session.FinalStorageKey)
                .FirstOrDefaultAsync(cancellationToken);
            if (persistedResource is not null)
            {
                session.ResourceId = persistedResource.Id;
                return await ReconcileStoredAssetRegistryAsync(session, actorId, cancellationToken);
            }
        }
        EnsureActive(session);

        session = await _context.ResourceUploadSessions.FindOneAndUpdateAsync<ResourceUploadSession>(
            item => item.Id == sessionId && item.ActorId == actorId && item.Status == "initiated",
            Builders<ResourceUploadSession>.Update
                .Set(item => item.Status, "verifying")
                .Set(item => item.UpdatedAt, DateTime.UtcNow),
            new FindOneAndUpdateOptions<ResourceUploadSession, ResourceUploadSession> { ReturnDocument = ReturnDocument.After },
            cancellationToken) ?? throw new ResourceUploadException(
                "upload_in_progress",
                "This upload is already being completed by another request.",
                StatusCodes.Status409Conflict);

        var previousStorageKey = session.PreviousStorageKey;
        try
        {
            if (session.Mode == "multipart")
            {
                var capabilities = await GetCapabilitiesAsync();
                var expectedParts = checked((int)Math.Ceiling(session.SizeBytes / (double)capabilities.MultipartPartSizeBytes));
                var parts = request.Parts
                    .Where(part => part.PartNumber > 0 && !string.IsNullOrWhiteSpace(part.ETag))
                    .GroupBy(part => part.PartNumber)
                    .Select(group => group.First())
                    .OrderBy(part => part.PartNumber)
                    .ToList();
                if (parts.Count != expectedParts || parts.Select(part => part.PartNumber).Where((number, index) => number != index + 1).Any())
                    throw Validation("The multipart upload is incomplete.");
                await _storage.CompleteMultipartUploadAsync(
                    session.PendingStorageKey,
                    session.MultipartUploadId!,
                    parts.Select(part => new R2CompletedPart(part.PartNumber, part.ETag)).ToList(),
                    cancellationToken);
            }

            var metadata = await _storage.GetMetadataAsync(session.PendingStorageKey, cancellationToken)
                ?? throw new ResourceUploadException("object_missing", "The uploaded object could not be found.", StatusCodes.Status422UnprocessableEntity);
            if (metadata.SizeBytes != session.SizeBytes)
                throw new ResourceUploadException("size_mismatch", "The uploaded object size does not match the selected file.", StatusCodes.Status422UnprocessableEntity);
            if (!ContentTypesMatch(metadata.ContentType, session.ContentType))
                throw new ResourceUploadException("content_type_mismatch", "The uploaded object content type does not match the selected file.", StatusCodes.Status422UnprocessableEntity);

            var sample = await _storage.ReadPrefixAsync(
                session.PendingStorageKey,
                session.Kind == "image" ? 512 * 1024 : 512,
                cancellationToken);
            if (!UploadSecurityPolicy.HasAllowedManagedResourceSignature(sample, session.FileName, session.ContentType, session.Kind))
                throw new ResourceUploadException("invalid_signature", UploadSecurityPolicy.InvalidSignatureMessage, StatusCodes.Status422UnprocessableEntity);
            if (session.Kind == "image" && !ResourceUploadInspection.HasSafeImageDimensions(sample, session.FileName, out var dimensionError))
                throw new ResourceUploadException("unsafe_image_dimensions", dimensionError!, StatusCodes.Status422UnprocessableEntity);

            if (string.IsNullOrWhiteSpace(session.FinalStorageKey))
                throw new ResourceUploadException("storage_key_missing", "The final storage key was not reserved.", StatusCodes.Status500InternalServerError);
            session.UpdatedAt = DateTime.UtcNow;
            await SaveAsync(session, cancellationToken);
            await _storage.CopyAsync(session.PendingStorageKey, session.FinalStorageKey, cancellationToken);

            ManagedResource? resource;
            List<string> errors;
            if (!string.IsNullOrWhiteSpace(session.ReplaceResourceId))
            {
                var previousResource = await _resources.GetByIdAsync(session.ReplaceResourceId);
                previousStorageKey = previousResource?.StorageKey;
                session.PreviousStorageKey = previousStorageKey;
                session.PreviousResourceUrl = previousResource?.Url;
                session.UpdatedAt = DateTime.UtcNow;
                await SaveAsync(session, cancellationToken);
                (resource, _, errors) = await _resources.ReplaceUploadAsync(
                    session.ReplaceResourceId,
                    _storage.PublicUrl(session.FinalStorageKey),
                    session.FinalStorageKey,
                    session.Kind,
                    session.FileName,
                    session.ContentType,
                    session.SizeBytes,
                    actorId,
                    session.AssetId,
                    session.AssetVersion,
                    session.StorageSchemaVersion,
                    deferOldAssetCleanup: true);
            }
            else
            {
                var create = _resources.BuildUploadCreateDto(
                    _storage.PublicUrl(session.FinalStorageKey),
                    session.FinalStorageKey,
                    session.Kind,
                    session.FileName,
                    session.ContentType,
                    session.SizeBytes,
                    session.AlbumId);
                create.Name["en"] = session.ResourceName;
                (resource, errors) = await _resources.CreateUploadedAsync(
                    create,
                    actorId,
                    session.ReservedResourceId!,
                    session.AssetId!,
                    session.AssetVersion,
                    session.StorageSchemaVersion,
                    session.UploadContext);
            }
            if (resource is null)
                throw new ResourceUploadException("resource_creation_failed", string.Join(" ", errors), StatusCodes.Status422UnprocessableEntity);

            await EnsureStoredAssetRegistryAsync(session, resource, actorId, cancellationToken);

            session.ResourceId = resource.Id;
            session.Status = "ready";
            session.ErrorCode = null;
            session.ErrorMessage = null;
            session.UpdatedAt = DateTime.UtcNow;
            SetTerminalRetention(session, session.UpdatedAt);
            await SaveAsync(session, cancellationToken);
            await SafeDeleteAsync(session.PendingStorageKey, cancellationToken);
            var usageCount = (await _resources.GetUsageAsync(resource.Id)).UsageCount;
            return Map(session, resource, usageCount);
        }
        catch (Exception exception)
        {
            // Once completion owns the session, always reconcile its durable state even
            // when the browser disconnects and the request cancellation token is signalled.
            var recoveryToken = CancellationToken.None;
            if (!string.IsNullOrWhiteSpace(session.FinalStorageKey))
            {
                var persistedResource = await _context.ManagedResources
                    .Find(resource => resource.StorageKey == session.FinalStorageKey)
                    .FirstOrDefaultAsync(recoveryToken);
                if (persistedResource is not null)
                {
                    try
                    {
                        await EnsureStoredAssetRegistryAsync(session, persistedResource, actorId, recoveryToken);
                    }
                    catch (Exception reconciliationException)
                    {
                        _logger.LogError(
                            reconciliationException,
                            "Stored asset registry reconciliation failed for upload Session {SessionId} and Resource {ResourceId}.",
                            session.Id,
                            persistedResource.Id);
                        session.ResourceId = persistedResource.Id;
                        session.Status = "reconciliation-required";
                        session.ErrorCode = "registry_reconciliation_required";
                        session.ErrorMessage = "Upload registration is still being completed. Retry shortly.";
                        session.UpdatedAt = DateTime.UtcNow;
                        session.CleanupAfterUtc = session.UpdatedAt.AddMinutes(5);
                        session.DeleteAfterUtc = null;
                        await SaveAsync(session, recoveryToken);
                        await SafeDeleteAsync(session.PendingStorageKey, recoveryToken);
                        throw new ResourceUploadException(
                            "registry_reconciliation_required",
                            "Upload registration is still being completed. Retry shortly.",
                            StatusCodes.Status503ServiceUnavailable);
                    }

                    session.ResourceId = persistedResource.Id;
                    session.Status = "ready";
                    session.ErrorCode = null;
                    session.ErrorMessage = null;
                    session.UpdatedAt = DateTime.UtcNow;
                    SetTerminalRetention(session, session.UpdatedAt);
                    await SaveAsync(session, recoveryToken);
                    await SafeDeleteAsync(session.PendingStorageKey, recoveryToken);
                    var usageCount = (await _resources.GetUsageAsync(persistedResource.Id)).UsageCount;
                    return Map(session, persistedResource, usageCount);
                }

                await SafeDeleteAsync(session.FinalStorageKey, recoveryToken);
            }
            await SafeDeleteAsync(session.PendingStorageKey, recoveryToken);
            var uploadException = exception as ResourceUploadException;
            var code = uploadException?.Code ?? "verification_failed";
            var userMessage = uploadException?.UserMessage ?? "Upload verification failed: the file could not be accepted.";
            if (uploadException is null)
                _logger.LogError(exception, "Resource upload verification failed for Session {SessionId}", session.Id);
            await FailAsync(session, code, userMessage, recoveryToken);
            throw;
        }
    }

    private async Task<ResourceUploadSessionDto> ReconcileStoredAssetRegistryAsync(
        ResourceUploadSession session,
        string actorId,
        CancellationToken cancellationToken)
    {
        var resource = !string.IsNullOrWhiteSpace(session.ResourceId)
            ? await _resources.GetByIdAsync(session.ResourceId)
            : null;
        if (resource is null && !string.IsNullOrWhiteSpace(session.FinalStorageKey))
        {
            resource = await _context.ManagedResources
                .Find(item => item.StorageKey == session.FinalStorageKey)
                .FirstOrDefaultAsync(cancellationToken);
        }
        if (resource is null)
        {
            throw new ResourceUploadException(
                "resource_reconciliation_failed",
                "Upload registration could not find the Resource Library item.",
                StatusCodes.Status409Conflict);
        }

        try
        {
            await EnsureStoredAssetRegistryAsync(session, resource, actorId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Stored asset registry reconciliation failed for upload Session {SessionId} and Resource {ResourceId}.",
                session.Id,
                resource.Id);
            session.ResourceId = resource.Id;
            session.Status = "reconciliation-required";
            session.ErrorCode = "registry_reconciliation_required";
            session.ErrorMessage = "Upload registration is still being completed. Retry shortly.";
            session.UpdatedAt = DateTime.UtcNow;
            session.CleanupAfterUtc = session.UpdatedAt.AddMinutes(5);
            session.DeleteAfterUtc = null;
            await SaveAsync(session, CancellationToken.None);
            throw new ResourceUploadException(
                "registry_reconciliation_required",
                "Upload registration is still being completed. Retry shortly.",
                StatusCodes.Status503ServiceUnavailable);
        }

        session.ResourceId = resource.Id;
        session.Status = "ready";
        session.ErrorCode = null;
        session.ErrorMessage = null;
        session.UpdatedAt = DateTime.UtcNow;
        SetTerminalRetention(session, session.UpdatedAt);
        await SaveAsync(session, cancellationToken);
        await SafeDeleteAsync(session.PendingStorageKey, cancellationToken);
        var usageCount = (await _resources.GetUsageAsync(resource.Id)).UsageCount;
        return Map(session, resource, usageCount);
    }

    private async Task EnsureStoredAssetRegistryAsync(
        ResourceUploadSession session,
        ManagedResource resource,
        string actorId,
        CancellationToken cancellationToken)
    {
        var assetId = !string.IsNullOrWhiteSpace(session.AssetId)
            ? session.AssetId
            : resource.AssetId;
        var storageKey = !string.IsNullOrWhiteSpace(session.FinalStorageKey)
            ? session.FinalStorageKey
            : resource.StorageKey;
        if (!ObjectId.TryParse(assetId, out _) || string.IsNullOrWhiteSpace(storageKey))
            throw new InvalidOperationException("The completed upload is missing its asset identity or storage key.");

        var registered = await _context.StoredAssets.Find(asset =>
                asset.Id == assetId &&
                asset.ResourceId == resource.Id &&
                asset.StorageKey == storageKey &&
                asset.LifecycleStatus == "ready")
            .AnyAsync(cancellationToken);
        if (!registered)
        {
            var metadata = await _storage.GetMetadataAsync(storageKey, cancellationToken)
                ?? throw new InvalidOperationException("The completed upload object could not be found in storage.");
            await _storedAssets.RecordReadyAsync(
                assetId!,
                Math.Max(1, session.StorageSchemaVersion),
                new AssetStorageOwner("resource-library", "managed-resource", resource.Id, resource.Kind),
                storageKey,
                resource.Url,
                session.FileName,
                session.ContentType,
                session.SizeBytes,
                actorId,
                Math.Max(1, session.AssetVersion),
                resource.Id,
                metadata.ETag,
                session.PreviousStorageKey,
                cancellationToken);
        }

        var previousUrl = session.PreviousResourceUrl;
        if (string.IsNullOrWhiteSpace(previousUrl) &&
            !string.IsNullOrWhiteSpace(session.PreviousStorageKey))
        {
            previousUrl = _storage.PublicUrl(session.PreviousStorageKey);
        }
        await _resources.ReconcileUploadReplacementAsync(resource, previousUrl);

        if (!string.IsNullOrWhiteSpace(session.PreviousStorageKey) &&
            !string.Equals(session.PreviousStorageKey, storageKey, StringComparison.Ordinal))
        {
            await _storedAssets.MarkSupersededAsync(
                session.PreviousStorageKey,
                DateTime.UtcNow.AddDays(Math.Max(1, _settings.LegacyObjectRetentionDays)),
                cancellationToken);
        }
    }

    public async Task<ResourceUploadSessionDto> GetAsync(string sessionId, string actorId, CancellationToken cancellationToken)
    {
        var session = await GetOwnedAsync(sessionId, actorId, cancellationToken);
        var resource = string.IsNullOrWhiteSpace(session.ResourceId)
            ? null
            : await _resources.GetByIdAsync(session.ResourceId);
        var usageCount = resource is null ? 0 : (await _resources.GetUsageAsync(resource.Id)).UsageCount;
        return Map(session, resource, usageCount);
    }

    public async Task<string> GetUploadContextAsync(
        string sessionId,
        string actorId,
        CancellationToken cancellationToken)
    {
        var session = await GetOwnedAsync(sessionId, actorId, cancellationToken);
        return ResourceUploadContextCodes.Normalize(session.UploadContext)
            ?? throw new ResourceUploadException(
                "invalid_upload_context",
                "The upload session has an invalid context.",
                StatusCodes.Status409Conflict);
    }

    public async Task<ResourceUploadSessionDto> AbortAsync(string sessionId, string actorId, CancellationToken cancellationToken)
    {
        var session = await GetOwnedAsync(sessionId, actorId, cancellationToken);
        if (TerminalStatuses.Contains(session.Status)) return Map(session);
        EnsureActive(session);
        session = await _context.ResourceUploadSessions.FindOneAndUpdateAsync<ResourceUploadSession>(
            item => item.Id == sessionId && item.ActorId == actorId && item.Status == "initiated",
            Builders<ResourceUploadSession>.Update
                .Set(item => item.Status, "cancelling")
                .Set(item => item.UpdatedAt, DateTime.UtcNow),
            new FindOneAndUpdateOptions<ResourceUploadSession, ResourceUploadSession> { ReturnDocument = ReturnDocument.After },
            cancellationToken) ?? throw new ResourceUploadException(
                "upload_in_progress",
                "The upload is already being completed.",
                StatusCodes.Status409Conflict);
        if (session.Mode == "multipart" && !string.IsNullOrWhiteSpace(session.MultipartUploadId))
        {
            try { await _storage.AbortMultipartUploadAsync(session.PendingStorageKey, session.MultipartUploadId, cancellationToken); }
            catch (Exception exception) { _logger.LogWarning(exception, "Could not abort R2 multipart upload {SessionId}", session.Id); }
        }
        await SafeDeleteAsync(session.PendingStorageKey, cancellationToken);
        session.Status = "cancelled";
        session.UpdatedAt = DateTime.UtcNow;
        SetTerminalRetention(session, session.UpdatedAt);
        await SaveAsync(session, cancellationToken);
        return Map(session);
    }

    public async Task<int> CleanupExpiredAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var filters = Builders<ResourceUploadSession>.Filter;
        var activeExpired = filters.And(
            filters.Lte(item => item.ExpiresAtUtc, now),
            filters.In(item => item.Status, new[] { "initiated", "verifying", "cancelling" }));
        var terminalNeedsCleanup = filters.And(
            filters.In(item => item.Status, TerminalStatuses),
            filters.Eq(item => item.StorageCleanupCompletedAtUtc, null),
            filters.Or(
                filters.Lte(item => item.CleanupAfterUtc, now),
                filters.Eq(item => item.CleanupAfterUtc, null),
                filters.Exists(item => item.CleanupAfterUtc, false)));
        var needsReconciliation = filters.And(
            filters.Eq(item => item.Status, "reconciliation-required"),
            filters.Or(
                filters.Lte(item => item.CleanupAfterUtc, now),
                filters.Eq(item => item.CleanupAfterUtc, null),
                filters.Exists(item => item.CleanupAfterUtc, false)));
        var sessions = await _context.ResourceUploadSessions.Find(filters.Or(activeExpired, terminalNeedsCleanup, needsReconciliation))
            .SortBy(item => item.CleanupAfterUtc)
            .ThenBy(item => item.UpdatedAt)
            .Limit(100)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            if (string.Equals(session.Status, "reconciliation-required", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    await ReconcileStoredAssetRegistryAsync(session, session.ActorId, cancellationToken);
                }
                catch (ResourceUploadException)
                {
                    // Reconciliation records its own safe status and retry time.
                }
                continue;
            }

            var cleanupAfter = session.CleanupAfterUtc
                ?? session.PresignedExpiresAtUtc?.AddMinutes(1)
                ?? session.UpdatedAt.AddMinutes(Math.Max(5, _settings.PresignedUrlMinutes) + 1);
            if (cleanupAfter > now)
            {
                session.CleanupAfterUtc = cleanupAfter;
                await SaveAsync(session, cancellationToken);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(session.FinalStorageKey) && string.IsNullOrWhiteSpace(session.ResourceId))
            {
                var existingResource = await _context.ManagedResources
                    .Find(resource => resource.StorageKey == session.FinalStorageKey)
                    .FirstOrDefaultAsync(cancellationToken);
                if (existingResource is not null)
                {
                    session.ResourceId = existingResource.Id;
                    try
                    {
                        await ReconcileStoredAssetRegistryAsync(
                            session,
                            session.ActorId,
                            cancellationToken);
                    }
                    catch (ResourceUploadException)
                    {
                        // Reconciliation records its own safe status and retry time.
                    }
                    continue;
                }
            }

            var cleanupSucceeded = true;
            if (session.Mode == "multipart" &&
                !string.IsNullOrWhiteSpace(session.MultipartUploadId) &&
                !string.Equals(session.Status, "ready", StringComparison.OrdinalIgnoreCase))
            {
                try { await _storage.AbortMultipartUploadAsync(session.PendingStorageKey, session.MultipartUploadId, cancellationToken); }
                catch (Exception exception)
                {
                    cleanupSucceeded = false;
                    _logger.LogWarning(exception, "Could not abort expired multipart upload {SessionId}", session.Id);
                }
            }

            cleanupSucceeded &= await SafeDeleteAsync(session.PendingStorageKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(session.FinalStorageKey) && string.IsNullOrWhiteSpace(session.ResourceId))
                cleanupSucceeded &= await SafeDeleteAsync(session.FinalStorageKey, cancellationToken);

            if (!TerminalStatuses.Contains(session.Status))
            {
                session.Status = "expired";
                session.ErrorCode = "upload_expired";
                session.ErrorMessage = "The upload session expired before it was completed.";
                SetTerminalRetention(session, now);
            }

            if (cleanupSucceeded)
            {
                session.StorageCleanupCompletedAtUtc = now;
                session.DeleteAfterUtc = now.AddDays(Math.Clamp(_settings.UploadSessionRetentionDays, 1, 90));
            }
            session.UpdatedAt = now;
            await SaveAsync(session, cancellationToken);
        }
        return sessions.Count;
    }

    private async Task<ResourceUploadSession> GetOwnedAsync(string sessionId, string actorId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(sessionId, out _)) throw NotFound();
        var session = await _context.ResourceUploadSessions
            .Find(item => item.Id == sessionId && item.ActorId == actorId)
            .FirstOrDefaultAsync(cancellationToken);
        return session ?? throw NotFound();
    }

    private static void EnsureActive(ResourceUploadSession session)
    {
        if (session.ExpiresAtUtc <= DateTime.UtcNow)
            throw new ResourceUploadException("upload_expired", "The upload session has expired.", StatusCodes.Status410Gone);
        if (TerminalStatuses.Contains(session.Status))
            throw new ResourceUploadException("invalid_upload_state", $"The upload is already {session.Status}.", StatusCodes.Status409Conflict);
        if (!string.Equals(session.Status, "initiated", StringComparison.OrdinalIgnoreCase))
            throw new ResourceUploadException("upload_in_progress", "The upload is already being completed.", StatusCodes.Status409Conflict);
    }

    private Task SaveAsync(ResourceUploadSession session, CancellationToken cancellationToken) =>
        _context.ResourceUploadSessions.ReplaceOneAsync(item => item.Id == session.Id, session, cancellationToken: cancellationToken);

    private async Task FailAsync(ResourceUploadSession session, string code, string message, CancellationToken cancellationToken)
    {
        session.Status = "failed";
        session.ErrorCode = code;
        session.ErrorMessage = message.Length > 1000 ? message[..1000] : message;
        session.UpdatedAt = DateTime.UtcNow;
        SetTerminalRetention(session, session.UpdatedAt);
        await SaveAsync(session, cancellationToken);
    }

    private async Task<bool> SafeDeleteAsync(string? key, CancellationToken cancellationToken)
    {
        try { return await _storage.DeleteKeyAsync(key, cancellationToken); }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not remove upload object {StorageKey}", key);
            return false;
        }
    }

    private void SetTerminalRetention(ResourceUploadSession session, DateTime now)
    {
        var cleanupAfter = session.PresignedExpiresAtUtc?.AddMinutes(1) ?? now;
        if (cleanupAfter < now) cleanupAfter = now;
        if (session.CleanupAfterUtc is null || session.CleanupAfterUtc < cleanupAfter)
            session.CleanupAfterUtc = cleanupAfter;
        session.DeleteAfterUtc = null;
    }

    private static bool ContentTypesMatch(string? stored, string? expected) =>
        string.Equals(NormalizeContentType(stored), NormalizeContentType(expected), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeContentType(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "application/octet-stream"
            : value.Split(';', 2)[0].Trim().ToLowerInvariant();

    private static ResourceUploadSessionDto Map(
        ResourceUploadSession session,
        ManagedResource? resource = null,
        int usageCount = 0) => new()
    {
        Id = session.Id,
        Status = session.Status,
        Mode = session.Mode,
        Kind = session.Kind,
        FileName = session.FileName,
        ResourceName = session.ResourceName,
        UploadContext = ResourceUploadContextCodes.Normalize(session.UploadContext)
            ?? ResourceUploadContextCodes.LibraryManual,
        SizeBytes = session.SizeBytes,
        ExpiresAtUtc = session.ExpiresAtUtc,
        ErrorCode = session.ErrorCode,
        ErrorMessage = session.ErrorMessage,
        ResourceId = session.ResourceId,
        ReplaceResourceId = session.ReplaceResourceId,
        Resource = resource is null ? null : new ResourceUploadResourceDto
        {
            Id = resource.Id,
            AssetId = resource.AssetId,
            AssetVersion = resource.AssetVersion,
            StorageSchemaVersion = resource.StorageSchemaVersion,
            Kind = resource.Kind,
            Purpose = resource.Purpose,
            OriginContext = resource.OriginContext,
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
            Active = resource.Active,
            UsageCount = usageCount,
            IsInUse = usageCount > 0,
            CreatedById = resource.CreatedById,
            UpdatedById = resource.UpdatedById,
            CreatedAt = resource.CreatedAt,
            UpdatedAt = resource.UpdatedAt
        }
    };

    private static string CleanResourceName(string? value, string fileName)
    {
        var name = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name)) name = Path.GetFileNameWithoutExtension(fileName);
        return name.Length > 200 ? name[..200] : name;
    }

    private static string DisplayKind(string kind) => kind switch { "image" => "Image", "video" => "Video", _ => "File" };
    private static ResourceUploadException Validation(string message) => new("validation_failed", message, StatusCodes.Status422UnprocessableEntity);
    private static ResourceUploadException NotFound() => new("upload_not_found", "Upload session not found.", StatusCodes.Status404NotFound);
}

public sealed class ResourceUploadException : Exception
{
    public ResourceUploadException(string code, string message, int statusCode) : base(message)
    {
        Code = code;
        UserMessage = message;
        StatusCode = statusCode;
    }

    public string Code { get; }
    public string UserMessage { get; }
    public int StatusCode { get; }
}

public sealed class ResourceUploadCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ResourceUploadCleanupService> _logger;

    public ResourceUploadCleanupService(IServiceScopeFactory scopeFactory, ILogger<ResourceUploadCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var cleaned = await scope.ServiceProvider.GetRequiredService<ResourceUploadSessionService>()
                    .CleanupExpiredAsync(stoppingToken);
                if (cleaned > 0) _logger.LogInformation("Cleaned {Count} expired Resource Library uploads.", cleaned);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception) { _logger.LogError(exception, "Resource upload cleanup failed."); }

            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }
}
