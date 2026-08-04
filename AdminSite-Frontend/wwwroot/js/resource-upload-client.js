(function () {
    const files = new Map();
    const requests = new Map();

    function token() {
        return self.crypto && self.crypto.randomUUID
            ? self.crypto.randomUUID()
            : `${Date.now()}-${Math.random().toString(16).slice(2)}`;
    }

    function releaseFile(id) {
        const entry = files.get(id);
        if (!entry) return;
        if (entry.previewUrl) URL.revokeObjectURL(entry.previewUrl);
        files.delete(id);
    }

    function updateProgressElement(itemId, percent) {
        const root = document.querySelector(`[data-resource-upload-progress="${CSS.escape(itemId)}"]`);
        if (!root) return;
        const normalized = Math.max(0, Math.min(100, Number(percent) || 0));
        const bar = root.querySelector("[data-resource-upload-progress-bar]");
        const label = root.querySelector("[data-resource-upload-progress-label]");
        if (bar) bar.style.width = `${normalized}%`;
        if (label) label.textContent = `${Math.round(normalized)}%`;
    }

    async function createImagePreview(file) {
        if (typeof createImageBitmap !== "function") return URL.createObjectURL(file);
        let bitmap;
        try {
            bitmap = await createImageBitmap(file);
            const scale = Math.min(1, 160 / Math.max(1, bitmap.width), 120 / Math.max(1, bitmap.height));
            const width = Math.max(1, Math.round(bitmap.width * scale));
            const height = Math.max(1, Math.round(bitmap.height * scale));
            const canvas = document.createElement("canvas");
            canvas.width = width;
            canvas.height = height;
            const context = canvas.getContext("2d", { alpha: true });
            if (!context) return URL.createObjectURL(file);
            context.drawImage(bitmap, 0, 0, width, height);
            const thumbnail = await new Promise(resolve => canvas.toBlob(resolve, "image/webp", .78));
            return thumbnail ? URL.createObjectURL(thumbnail) : URL.createObjectURL(file);
        } catch {
            return URL.createObjectURL(file);
        } finally {
            if (bitmap && typeof bitmap.close === "function") bitmap.close();
        }
    }

    window.resourceUploadClient = {
        captureFiles: async function (inputId, maxFiles) {
            const input = document.getElementById(inputId);
            if (!input || !input.files) return { selectedCount: 0, files: [] };

            const selected = Array.from(input.files);
            const capacity = Number.isFinite(maxFiles)
                ? Math.max(0, Math.floor(maxFiles))
                : selected.length;
            const captured = [];
            const createdTokens = [];

            try {
                for (const file of selected.slice(0, capacity)) {
                    const id = token();
                    const previewKind = file.type.startsWith("image/")
                        ? "image"
                        : file.type.startsWith("video/") ? "video" : "file";
                    const previewUrl = previewKind === "image"
                        ? await createImagePreview(file)
                        : previewKind === "video" ? URL.createObjectURL(file) : null;
                    files.set(id, { file, previewUrl });
                    createdTokens.push(id);
                    captured.push({
                        token: id,
                        name: file.name,
                        size: file.size,
                        contentType: file.type || "application/octet-stream",
                        previewKind,
                        previewUrl
                    });
                }

                return {
                    selectedCount: selected.length,
                    files: captured
                };
            } catch (error) {
                createdTokens.forEach(releaseFile);
                throw error;
            } finally {
                // A persistent file input otherwise suppresses a later change event
                // when the user removes and selects the same file again.
                input.value = "";
            }
        },

        openReadStream: function (fileToken) {
            const entry = files.get(fileToken);
            if (!entry) throw new Error("The selected browser file is no longer available.");

            const source = entry.file;
            if (!source || typeof source.arrayBuffer !== "function" || !Number.isFinite(source.size)) {
                throw new Error("The selected browser file cannot be streamed.");
            }

            // The .NET caller requests IJSStreamReference, so Blazor converts this
            // raw Blob into the stream reference. Do not call createJSStreamReference
            // here or the framework will try to wrap the descriptor a second time.
            const blob = new Blob([source], {
                type: source.type || "application/octet-stream"
            });
            if (blob.size !== source.size) {
                throw new Error("The selected browser file could not be prepared for streaming.");
            }
            return blob;
        },

        upload: function (fileToken, url, headers, itemId, dotNetRef, start, end, timeoutMs) {
            const entry = files.get(fileToken);
            if (!entry) return Promise.reject(new Error("The selected browser file is no longer available."));
            const source = entry.file;
            const body = Number.isFinite(start) && Number.isFinite(end)
                ? source.slice(start, end)
                : source;

            return new Promise((resolve, reject) => {
                const xhr = new XMLHttpRequest();
                requests.set(itemId, xhr);
                xhr.open("PUT", url, true);
                xhr.timeout = Math.max(30000, timeoutMs || 180000);
                Object.entries(headers || {}).forEach(([name, value]) => {
                    if (value) xhr.setRequestHeader(name, value);
                });

                let lastProgress = 0;
                xhr.upload.onprogress = event => {
                    if (!event.lengthComputable) return;
                    const offset = Number.isFinite(start) ? start : 0;
                    const percent = source.size > 0
                        ? Math.min(99, (offset + event.loaded) * 100 / source.size)
                        : 0;
                    updateProgressElement(itemId, percent);
                    if (!dotNetRef) return;
                    const now = performance.now();
                    if (event.loaded !== event.total && now - lastProgress < 400) return;
                    lastProgress = now;
                    dotNetRef.invokeMethodAsync("UpdateResourceUploadProgress", itemId, event.loaded, event.total)
                        .catch(() => { });
                };
                xhr.onload = () => {
                    requests.delete(itemId);
                    if (xhr.status >= 200 && xhr.status < 300) {
                        updateProgressElement(itemId, 99);
                        resolve({ status: xhr.status, etag: xhr.getResponseHeader("ETag") || "" });
                    } else {
                        reject(new Error(`Storage rejected the upload (${xhr.status}).`));
                    }
                };
                xhr.onerror = () => {
                    requests.delete(itemId);
                    reject(new Error("Storage upload failed. Check the R2 CORS policy and network connection."));
                };
                xhr.ontimeout = () => {
                    requests.delete(itemId);
                    reject(new Error("Storage upload timed out."));
                };
                xhr.onabort = () => {
                    requests.delete(itemId);
                    reject(new DOMException("Upload cancelled.", "AbortError"));
                };
                xhr.send(body);
            });
        },

        abort: function (itemId) {
            const xhr = requests.get(itemId);
            if (xhr) xhr.abort();
        },

        setProgress: updateProgressElement,

        release: releaseFile,
        releaseMany: function (tokens) { (tokens || []).forEach(releaseFile); }
    };
})();
