window.cmsWidgets = window.cmsWidgets || {};

window.cmsWidgets.init = () => {
    initCounters();
    initCarousels();
    initLibraryVideos();
    initDownloadMetrics();
    initNetworkMaps();
    initPublicModals();
};

function initCounters() {
    document.querySelectorAll("[data-sc-stats]:not([data-bound])").forEach(root => {
        root.dataset.bound = "true";
        const duration = parseInt(root.dataset.duration || "1200", 10);
        const run = () => {
            root.querySelectorAll("[data-sc-counter]").forEach(counter => {
                const target = Number(counter.dataset.value || "0");
                const start = performance.now();
                const step = now => {
                    const t = Math.min((now - start) / duration, 1);
                    counter.textContent = Math.round(target * t).toLocaleString();
                    if (t < 1) requestAnimationFrame(step);
                };
                requestAnimationFrame(step);
            });
        };
        new IntersectionObserver((entries, observer) => {
            if (!entries.some(e => e.isIntersecting)) return;
            observer.disconnect();
            run();
        }, { threshold: 0.25 }).observe(root);
    });
}

function initCarousels() {
    document.querySelectorAll("[data-sc-carousel]:not([data-bound])").forEach(root => {
        root.dataset.bound = "true";
        const track = root.querySelector(".sc-carousel__track");
        if (!track) return;
        const move = dir => track.scrollBy({ left: dir * track.clientWidth * 0.85, behavior: "smooth" });
        root.querySelector("[data-sc-carousel-prev]")?.addEventListener("click", () => move(-1));
        root.querySelector("[data-sc-carousel-next]")?.addEventListener("click", () => move(1));
        if (root.dataset.autoplay === "true") setInterval(() => move(1), 4500);
    });
}

function initLibraryVideos() {
    if (window.__scLibraryVideosDelegated) return;
    window.__scLibraryVideosDelegated = true;

    document.addEventListener("click", e => {
        const videoTrigger = e.target.closest("[data-sc-library-video]");
        if (videoTrigger) {
            e.preventDefault();
            e.stopPropagation();

            const playlist = collectLibraryVideoPlaylist(videoTrigger);
            const selected = playlist.items[playlist.activeIndex];
            if (!selected?.embedUrl) return;

            openLibraryVideoModal(selected.embedUrl, selected.title, playlist.items, playlist.activeIndex, selected.sourceType);
            return;
        }

        const imageTrigger = e.target.closest("[data-sc-library-image]");
        if (!imageTrigger) return;

        e.preventDefault();
        e.stopPropagation();

        const imageUrl = imageTrigger.dataset.imageUrl || "";
        if (!imageUrl) return;
        openLibraryImageModal(imageUrl, imageTrigger.dataset.imageTitle || "Image");
    });
}

function collectLibraryVideoPlaylist(activeTrigger) {
    const root = activeTrigger.closest("[data-sc-video-playlist]") || activeTrigger.closest("[data-section-id]");
    const triggers = root ? [...root.querySelectorAll("[data-sc-library-video]")] : [activeTrigger];
    const activeSource = resolveVideoSource(activeTrigger.dataset.videoUrl || "");
    const activeEmbedUrl = activeSource?.url || "";
    const activeSourceType = activeSource?.type || "embed";
    const activeTitle = activeTrigger.dataset.videoTitle || "Video";
    const activeKey = videoPlaylistKey(activeEmbedUrl, activeTitle, activeSourceType);
    const byKey = new Map();

    triggers.forEach(trigger => {
        const source = resolveVideoSource(trigger.dataset.videoUrl || "");
        if (!source?.url) return;

        const title = trigger.dataset.videoTitle || "Video";
        const thumb = trigger.dataset.videoThumb || trigger.querySelector("img")?.getAttribute("src") || "";
        const key = videoPlaylistKey(source.url, title, source.type);

        if (!byKey.has(key)) {
            byKey.set(key, { embedUrl: source.url, sourceType: source.type, title, thumb });
        }
    });

    if (activeEmbedUrl && !byKey.has(activeKey)) {
        byKey.set(activeKey, {
            embedUrl: activeEmbedUrl,
            sourceType: activeSourceType,
            title: activeTitle,
            thumb: activeTrigger.dataset.videoThumb || activeTrigger.querySelector("img")?.getAttribute("src") || ""
        });
    }

    const items = [...byKey.values()];
    let activeIndex = items.findIndex(item => videoPlaylistKey(item.embedUrl, item.title, item.sourceType) === activeKey);
    if (activeIndex < 0) activeIndex = 0;

    return { items, activeIndex };
}

function videoPlaylistKey(embedUrl, title, sourceType = "embed") {
    return `${sourceType || "embed"}|${embedUrl || ""}|${title || ""}`.trim().toLowerCase();
}


function initDownloadMetrics() {
    if (window.__scDownloadMetricsDelegated) return;
    window.__scDownloadMetricsDelegated = true;

    document.addEventListener("click", e => {
        const link = e.target.closest("a.sc-insight-download[href], a[download][href], a[data-sc-download][href]");
        if (!link) return;

        const href = link.getAttribute("href") || "";
        if (!href || href.startsWith("#") || href.startsWith("mailto:") || href.startsWith("tel:")) return;
        trackDownloadMetric(href);
    }, true);
}

function trackDownloadMetric(href) {
    try {
        const url = new URL(href, window.location.href);
        if (!["http:", "https:"].includes(url.protocol)) return;

        const payload = JSON.stringify({
            url: url.href,
            sourcePage: window.location.pathname
        });

        fetch(buildPublicApiUrl("api/public/metrics/download"), {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: payload,
            keepalive: true
        }).catch(() => { });
    } catch {
        // Ignore metric failures; downloads must never be blocked by tracking.
    }
}
function initNetworkMaps() {
    document.querySelectorAll("[data-sc-network-map]:not([data-bound])").forEach(root => {
        root.dataset.bound = "true";
        const pins = JSON.parse(root.dataset.pins || "[]");
        if (!window.L) {
            root.replaceChildren(buildMapPlaceholder(pins));
            return;
        }

        root.replaceChildren();
        const map = L.map(root).setView([Number(root.dataset.centerLat), Number(root.dataset.centerLng)], Number(root.dataset.zoom || 4));
        L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", { maxZoom: 18 }).addTo(map);
        pins.forEach(pin => {
            const popup = buildMapPopup(pin);
            L.marker([pin.Lat, pin.Lng]).addTo(map).bindPopup(popup);
        });
    });
}

function initPublicModals() {
    if (window.__scPublicModalsDelegated) return;
    window.__scPublicModalsDelegated = true;

    document.addEventListener("click", async e => {
        const trigger = e.target.closest("[data-sc-public-modal-trigger], a[href]");
        if (!trigger) return;

        const formId = trigger.dataset.modalFormId || "";
        const modalHref = trigger.dataset.modalHref || trigger.getAttribute("href");
        if (!formId && !isPublicModalHref(modalHref)) return;

        e.preventDefault();
        e.stopPropagation();

        if (formId) {
            await openPublicFormModalById(formId);
            return;
        }

        openPublicModal(resolveModalType(modalHref, trigger.textContent));
    });
}

function ensurePublicModal() {
    let modal = document.querySelector(".sc-public-modal");
    if (modal) return modal;

    modal = document.createElement("div");
    modal.className = "sc-public-modal";
    modal.setAttribute("role", "dialog");
    modal.setAttribute("aria-modal", "true");
    document.body.appendChild(modal);
    modal.addEventListener("click", e => {
        if (e.target === modal || e.target.closest(".sc-public-modal__close")) {
            closePublicModal(modal);
        }
    });
    document.addEventListener("keydown", e => {
        if (e.key === "Escape" && modal.classList.contains("open")) {
            closePublicModal(modal);
        }
    });
    return modal;
}

async function openPublicFormModalById(formId) {
    const modal = ensurePublicModal();
    try {
        modal.replaceChildren(buildPublicModalLoadingDialog());
        document.body.classList.add("sc-modal-open");
        modal.classList.add("open");

        const response = await fetch(buildPublicApiUrl(`api/public/forms/by-id/${encodeURIComponent(formId)}`));
        const result = await response.json().catch(() => null);
        if (!response.ok) throw new Error(result?.message || result?.Message || "Form not found");

        const definition = result?.data || result?.Data;
        if (!definition) throw new Error("Form not found");
        modal.replaceChildren(buildPublicModalDialog(definition));
    } catch (error) {
        modal.replaceChildren(buildPublicModalErrorDialog(error?.message || publicUiText("SubmitError", getPublicUiLanguage())));
    }
}

function openPublicModal(type) {
    const modal = ensurePublicModal();
    modal.replaceChildren(buildPublicModalDialog(type));
    document.body.classList.add("sc-modal-open");
    modal.classList.add("open");
}

function closePublicModal(modal) {
    modal.classList.remove("open");
    document.body.classList.remove("sc-modal-open");
    modal.replaceChildren();
}

function openLibraryVideoModal(embedUrl, titleText, playlist = null, activeIndex = 0, sourceType = null) {
    const modal = ensureLibraryVideoModal();
    const source = resolveVideoSource(embedUrl) || { url: embedUrl, type: sourceType || "embed" };
    const items = Array.isArray(playlist) && playlist.length > 0
        ? playlist
        : [{ embedUrl: source.url, sourceType: source.type, title: titleText || "Video", thumb: "" }];

    modal.__scVideoPlaylist = items;
    modal.__scVideoIndex = Math.max(0, Math.min(Number(activeIndex) || 0, items.length - 1));
    renderLibraryVideoModal(modal);

    document.body.classList.add("sc-modal-open");
    modal.classList.add("open");
}

function renderLibraryVideoModal(modal) {
    const playlist = modal.__scVideoPlaylist || [];
    const activeIndex = Math.max(0, Math.min(modal.__scVideoIndex || 0, playlist.length - 1));
    const active = playlist[activeIndex];
    if (!active) return;

    const title = modal.querySelector(".sc-video-modal__title");
    const frame = modal.querySelector(".sc-video-modal__frame");
    const playlistRoot = modal.querySelector(".sc-video-modal__playlist");

    if (title) title.textContent = active.title || "Video";
    if (frame) {
        frame.replaceChildren();
        if (active.sourceType === "file") {
            const video = document.createElement("video");
            video.src = active.embedUrl;
            video.title = active.title || "Video";
            video.controls = true;
            video.playsInline = true;
            video.preload = "metadata";
            frame.appendChild(video);
        } else {
            const iframe = document.createElement("iframe");
            iframe.src = active.embedUrl;
            iframe.title = active.title || "Video";
            iframe.allow = "accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share";
            iframe.allowFullscreen = true;
            frame.appendChild(iframe);
        }
    }

    if (!playlistRoot) return;
    playlistRoot.replaceChildren();

    if (playlist.length <= 1) {
        modal.classList.remove("sc-video-modal--playlist");
        return;
    }

    modal.classList.add("sc-video-modal--playlist");

    const heading = document.createElement("div");
    heading.className = "sc-video-modal__playlist-heading";
    heading.textContent = "More videos";
    playlistRoot.appendChild(heading);

    playlist.forEach((item, index) => {
        const button = document.createElement("button");
        button.type = "button";
        button.className = index === activeIndex
            ? "sc-video-modal__playlist-item sc-video-modal__playlist-item--active"
            : "sc-video-modal__playlist-item";

        const thumb = document.createElement("span");
        thumb.className = "sc-video-modal__playlist-thumb";
        if (item.thumb) {
            const image = document.createElement("img");
            image.src = item.thumb;
            image.alt = "";
            thumb.appendChild(image);
        } else {
            thumb.textContent = "Video";
        }

        const label = document.createElement("span");
        label.className = "sc-video-modal__playlist-title";
        label.textContent = item.title || "Video";

        button.append(thumb, label);
        button.addEventListener("click", () => {
            modal.__scVideoIndex = index;
            renderLibraryVideoModal(modal);
        });

        playlistRoot.appendChild(button);
    });
}

function ensureLibraryVideoModal() {
    let modal = document.querySelector(".sc-video-modal");
    if (modal) return modal;

    modal = document.createElement("div");
    modal.className = "sc-video-modal";
    modal.setAttribute("role", "dialog");
    modal.setAttribute("aria-modal", "true");

    const dialog = document.createElement("div");
    dialog.className = "sc-video-modal__dialog";

    const header = document.createElement("div");
    header.className = "sc-video-modal__header";

    const title = document.createElement("h2");
    title.className = "sc-video-modal__title";

    const close = document.createElement("button");
    close.type = "button";
    close.className = "sc-video-modal__close";
    close.setAttribute("aria-label", "Close video");
    close.textContent = "x";

    const frame = document.createElement("div");
    frame.className = "sc-video-modal__frame";

    const body = document.createElement("div");
    body.className = "sc-video-modal__body";

    const main = document.createElement("div");
    main.className = "sc-video-modal__main";

    const playlist = document.createElement("div");
    playlist.className = "sc-video-modal__playlist";
    playlist.setAttribute("aria-label", "More videos");

    header.append(title, close);
    main.appendChild(frame);
    body.append(main, playlist);
    dialog.append(header, body);
    modal.appendChild(dialog);
    document.body.appendChild(modal);

    modal.addEventListener("click", e => {
        if (e.target === modal || e.target.closest(".sc-video-modal__close")) {
            closeLibraryVideoModal(modal);
        }
    });
    document.addEventListener("keydown", e => {
        if (e.key === "Escape" && modal.classList.contains("open")) {
            closeLibraryVideoModal(modal);
        }
    });

    return modal;
}

function closeLibraryVideoModal(modal) {
    modal.classList.remove("open");
    modal.classList.remove("sc-video-modal--playlist");
    modal.querySelector(".sc-video-modal__frame")?.replaceChildren();
    modal.querySelector(".sc-video-modal__playlist")?.replaceChildren();
    modal.__scVideoPlaylist = [];
    modal.__scVideoIndex = 0;
    document.body.classList.remove("sc-modal-open");
}


function openLibraryImageModal(imageUrl, titleText) {
    const modal = ensureLibraryImageModal();
    const title = modal.querySelector(".sc-image-modal__title");
    const frame = modal.querySelector(".sc-image-modal__frame");

    if (title) title.textContent = titleText;
    if (frame) {
        frame.replaceChildren();
        const image = document.createElement("img");
        image.src = imageUrl;
        image.alt = titleText || "Image";
        frame.appendChild(image);
    }

    document.body.classList.add("sc-modal-open");
    modal.classList.add("open");
}

function ensureLibraryImageModal() {
    let modal = document.querySelector(".sc-image-modal");
    if (modal) return modal;

    modal = document.createElement("div");
    modal.className = "sc-image-modal";
    modal.setAttribute("role", "dialog");
    modal.setAttribute("aria-modal", "true");

    const dialog = document.createElement("div");
    dialog.className = "sc-image-modal__dialog";

    const header = document.createElement("div");
    header.className = "sc-image-modal__header";

    const title = document.createElement("h2");
    title.className = "sc-image-modal__title";

    const close = document.createElement("button");
    close.type = "button";
    close.className = "sc-image-modal__close";
    close.setAttribute("aria-label", "Close image");
    close.textContent = "x";

    const frame = document.createElement("div");
    frame.className = "sc-image-modal__frame";

    header.append(title, close);
    dialog.append(header, frame);
    modal.appendChild(dialog);
    document.body.appendChild(modal);

    modal.addEventListener("click", e => {
        if (e.target === modal || e.target.closest(".sc-image-modal__close")) {
            closeLibraryImageModal(modal);
        }
    });
    document.addEventListener("keydown", e => {
        if (e.key === "Escape" && modal.classList.contains("open")) {
            closeLibraryImageModal(modal);
        }
    });

    return modal;
}

function closeLibraryImageModal(modal) {
    modal.classList.remove("open");
    modal.querySelector(".sc-image-modal__frame")?.replaceChildren();
    document.body.classList.remove("sc-modal-open");
}
function resolveVideoEmbedUrl(rawUrl) {
    return resolveVideoSource(rawUrl)?.url || null;
}

function resolveVideoSource(rawUrl) {
    try {
        const url = new URL(rawUrl, window.location.origin);
        const host = url.hostname.toLowerCase().replace(/^www\./, "");

        if (host === "youtu.be") {
            return buildVideoSource(buildYouTubeEmbed(url.pathname.split("/").filter(Boolean)[0]), "embed");
        }

        if (host === "youtube.com" || host.endsWith(".youtube.com") || host === "youtube-nocookie.com" || host.endsWith(".youtube-nocookie.com")) {
            const path = url.pathname.split("/").filter(Boolean);
            const route = (path[0] || "").toLowerCase();
            const id = path.length === 0 || route === "watch"
                ? url.searchParams.get("v")
                : route === "embed"
                    ? path[1]
                    : route === "shorts"
                        ? path[1]
                        : route === "live"
                            ? path[1]
                            : "";
            const embedUrl = buildYouTubeEmbed(id);
            return buildVideoSource(embedUrl, "embed");
        }

        if (host === "vimeo.com" || host.endsWith(".vimeo.com")) {
            const id = url.pathname.split("/").find(part => /^\d+$/.test(part));
            return buildVideoSource(id ? `https://player.vimeo.com/video/${id}?autoplay=1` : null, "embed");
        }

        if (url.protocol === "http:" || url.protocol === "https:") {
            return { url: url.href, type: "file" };
        }
    } catch {
        return null;
    }

    return null;
}

function buildVideoSource(url, type) {
    return url ? { url, type } : null;
}

function buildYouTubeEmbed(id) {
    return /^[A-Za-z0-9_-]{6,}$/.test(id || "")
        ? `https://www.youtube.com/embed/${id}?autoplay=1&rel=0`
        : null;
}

function buildMapPlaceholder(pins) {
    const placeholder = document.createElement("div");
    placeholder.className = "sc-map-placeholder";
    pins.forEach(pin => {
        const item = document.createElement("div");
        item.textContent = pin.Label || "";
        placeholder.appendChild(item);
    });
    return placeholder;
}

function buildMapPopup(pin) {
    if (pin.Href && isSafeHref(pin.Href)) {
        const link = document.createElement("a");
        link.href = pin.Href;
        link.textContent = pin.Label || "";
        return link;
    }

    const span = document.createElement("span");
    span.textContent = pin.Label || "";
    return span;
}

function resolveModalType(rawHref, rawLabel) {
    const href = (rawHref || "").toLowerCase();
    const label = (rawLabel || "").toLowerCase();

    if (href.includes("sync") || label.includes("sync")) return "sync";
    if (href.includes("quote") || label.includes("quote")) return "quote";
    if (href.includes("expert") || href === "#modal" || label.includes("expert")) return "expert";

    return "expert";
}

function isPublicModalHref(rawHref) {
    const href = (rawHref || "").trim().toLowerCase();
    return href.startsWith("modal:")
        || href === "#modal"
        || href === "#quote"
        || href === "#expert";
}

function getPublicModalConfig(type) {
    const lang = getPublicUiLanguage();
    const configs = {
        quote: {
            type: "quote",
            title: publicUiText("QuoteTitle", lang),
            intro: publicUiText("QuoteIntro", lang),
            submit: publicUiText("Submit", lang),
            fields: [
                { name: "ServiceType", label: publicUiText("SelectService", lang), type: "select", required: true, options: ["Logistics", "Warehouse", "Transport"] },
                { name: "Route", label: publicUiText("Route", lang), required: true },
                { name: "Email", label: publicUiText("Email", lang), type: "email", autocomplete: "email", required: true },
                { name: "Phone", label: publicUiText("Phone", lang), type: "tel", autocomplete: "tel", required: true }
            ]
        },
        expert: {
            type: "expert",
            title: publicUiText("ExpertTitle", lang),
            intro: publicUiText("ExpertIntro", lang),
            submit: publicUiText("SubmitRequest", lang),
            fields: [
                { name: "Name", label: publicUiText("FullName", lang), autocomplete: "name", required: true },
                { name: "Email", label: publicUiText("EmailAddress", lang), type: "email", autocomplete: "email", required: true },
                { name: "Phone", label: publicUiText("Phone", lang), type: "tel", autocomplete: "tel", required: true },
                { name: "Company", label: publicUiText("CompanyName", lang), autocomplete: "organization" },
                { name: "Service", label: publicUiText("SelectService", lang), type: "select", required: true, options: ["Consulting", "Implementation", "Support", "Other"] },
                { name: "Message", label: publicUiText("YourMessage", lang), multiline: true }
            ]
        },
        sync: {
            type: "sync",
            title: publicUiText("SyncTitle", lang),
            intro: publicUiText("SyncIntro", lang),
            submit: publicUiText("Login", lang),
            fields: [
                { name: "Username", label: publicUiText("Username", lang), autocomplete: "username", required: true },
                { name: "Password", label: publicUiText("Password", lang), type: "password", autocomplete: "current-password", required: true }
            ]
        }
    };

    return configs[type] || configs.expert;
}

function getPublicModalConfigFromDefinition(definition) {
    const lang = getPublicUiLanguage();
    return {
        type: definition.key || definition.Key,
        title: localizeText(definition.name || definition.Name, definition.key || definition.Key || "Form"),
        intro: localizeText(definition.introduction || definition.Introduction, ""),
        submit: localizeText(definition.submitButtonLabel || definition.SubmitButtonLabel, publicUiText("Submit", lang)),
        layout: normalizePublicFormLayout(definition.layout ?? definition.Layout),
        fields: (definition.fields || definition.Fields || [])
            .slice()
            .sort((a, b) => (a.order ?? a.Order ?? 0) - (b.order ?? b.Order ?? 0))
            .map(field => {
                const type = normalizePublicFieldType(field.type || field.Type);
                return {
                    name: field.key || field.Key,
                    label: localizeText(field.label || field.Label, field.key || field.Key || ""),
                    type,
                    required: !!(field.required ?? field.Required),
                    maxLength: normalizePublicMaxLength(type, field.maxLength ?? field.MaxLength),
                    inputBoxSize: normalizePublicInputBoxSize(type, field.inputBoxSize ?? field.InputBoxSize),
                    options: field.options || field.Options || []
                };
            })
    };
}

function buildPublicModalLoadingDialog() {
    const dialog = document.createElement("div");
    dialog.className = "sc-public-modal__dialog";
    const text = document.createElement("p");
    text.className = "sc-public-modal__intro";
    text.textContent = publicUiText("Submitting", getPublicUiLanguage());
    dialog.appendChild(text);
    return dialog;
}

function buildPublicModalErrorDialog(message) {
    const dialog = document.createElement("div");
    dialog.className = "sc-public-modal__dialog";
    const header = document.createElement("div");
    header.className = "sc-public-modal__header";
    const title = document.createElement("h2");
    title.textContent = publicUiText("SubmitError", getPublicUiLanguage());
    const close = document.createElement("button");
    close.type = "button";
    close.className = "sc-public-modal__close";
    close.setAttribute("aria-label", "Close");
    close.textContent = "x";
    header.append(title, close);
    const text = document.createElement("p");
    text.className = "sc-public-modal__intro";
    text.textContent = message;
    dialog.append(header, text);
    return dialog;
}

function normalizePublicFieldType(type) {
    const value = (type || "text").toString().trim().toLowerCase();
    if (value === "phone") return "tel";
    if (value === "long-text" || value === "longtext") return "textarea";
    if (value === "dropdown") return "select";
    if (value === "short-text" || value === "shorttext") return "text";
    return ["text", "email", "tel", "url", "number", "date", "select", "textarea", "checkbox", "password"].includes(value) ? value : "text";
}

function getPublicFieldCapability(type) {
    const normalized = normalizePublicFieldType(type);
    const map = {
        text: { inputType: "text", max: true, size: false, multiline: false, options: false },
        email: { inputType: "email", max: true, size: false, multiline: false, options: false },
        tel: { inputType: "tel", max: true, size: false, multiline: false, options: false },
        url: { inputType: "url", max: true, size: false, multiline: false, options: false },
        password: { inputType: "password", max: true, size: false, multiline: false, options: false },
        textarea: { inputType: "textarea", max: true, size: true, multiline: true, options: false },
        select: { inputType: "select", max: false, size: false, multiline: false, options: true },
        checkbox: { inputType: "checkbox", max: false, size: false, multiline: false, options: false },
        date: { inputType: "date", max: false, size: false, multiline: false, options: false },
        number: { inputType: "number", max: false, size: false, multiline: false, options: false }
    };
    return map[normalized] || map.text;
}

function normalizePublicMaxLength(type, value) {
    const parsed = Number.parseInt(value, 10);
    if (!Number.isFinite(parsed) || parsed <= 0) return 0;

    return Math.min(Math.max(parsed, 1), 2000);
}

function normalizePublicInputBoxSize(type, value) {
    const capability = getPublicFieldCapability(type);
    if (!capability.size) return 1;

    const parsed = Number.parseInt(value, 10);
    if (!Number.isFinite(parsed) || parsed <= 0) return 4;
    return Math.min(Math.max(parsed, 1), 5);
}

function normalizePublicFormLayout(layout) {
    if (layout === 1 || layout === "TwoColumns" || layout === "twoColumns" || layout === "two-columns") {
        return "two-columns";
    }

    return "stacked";
}
function localizeText(value, fallback) {
    if (!value) return fallback || "";
    if (typeof value === "string") return value;
    const lang = getPublicUiLanguage();
    return value[lang] || value.en || Object.values(value).find(v => !!v) || fallback || "";
}
function buildPublicModalDialog(typeOrDefinition) {
    const config = typeof typeOrDefinition === "string"
        ? getPublicModalConfig(typeOrDefinition)
        : getPublicModalConfigFromDefinition(typeOrDefinition);
    const dialog = document.createElement("div");
    const layout = normalizePublicFormLayout(config.layout);
    dialog.className = `sc-public-modal__dialog sc-public-modal__dialog--${layout}`;

    const header = document.createElement("div");
    header.className = "sc-public-modal__header";

    const close = document.createElement("button");
    close.type = "button";
    close.className = "sc-public-modal__close";
    close.setAttribute("aria-label", "Close");
    close.textContent = "x";

    const title = document.createElement("h2");
    title.textContent = config.title;
    header.append(title, close);

    const text = document.createElement("p");
    text.className = "sc-public-modal__intro";
    text.textContent = config.intro;

    const form = document.createElement("form");
    form.className = `sc-public-modal__form sc-public-modal__form--${layout}`;
    form.dataset.modalType = config.type;
    form.noValidate = true;
    form.addEventListener("submit", async e => {
        e.preventDefault();
        await submitPublicModalForm(form, submit, status);
    });

    const honeypot = document.createElement("input");
    honeypot.type = "text";
    honeypot.name = "__website";
    honeypot.tabIndex = -1;
    honeypot.autocomplete = "off";
    honeypot.className = "sc-form-honeypot";
    honeypot.setAttribute("aria-hidden", "true");
    form.appendChild(honeypot);

    config.fields.forEach(field => form.appendChild(buildPublicModalField(field)));

    const submit = document.createElement("button");
    submit.type = "submit";
    submit.className = "sc-btn sc-btn--filled";
    submit.textContent = config.submit;
    submit.dataset.defaultText = config.submit;

    const status = document.createElement("p");
    status.className = "sc-public-modal__status";
    status.setAttribute("aria-live", "polite");

    form.append(submit, status);

    if (config.type === "sync") {
        const help = document.createElement("span");
        help.className = "sc-public-modal__help";
        help.textContent = publicUiText("ForgotPassword", getPublicUiLanguage());
        form.appendChild(help);
    }

    dialog.append(header, text, form);
    return dialog;
}

function buildPublicModalField(field) {
    const wrapper = document.createElement("label");
    wrapper.className = "sc-public-modal__field";
    const type = normalizePublicFieldType(field.multiline ? "textarea" : field.type);
    const capability = getPublicFieldCapability(type);
    const inputBoxSize = normalizePublicInputBoxSize(type, field.inputBoxSize);
    const maxLength = normalizePublicMaxLength(type, field.maxLength);

    if (capability.multiline || type === "checkbox") {
        wrapper.classList.add("sc-public-modal__field--full");
    }
    const displayLabel = field.required ? `${field.label} *` : field.label;

    const input = type === "select"
        ? document.createElement("select")
        : capability.multiline
            ? document.createElement("textarea")
            : document.createElement("input");

    input.name = field.name;
    input.required = !!field.required && type !== "checkbox";
    input.dataset.fieldType = type;

    if (type === "select") {
        const placeholder = document.createElement("option");
        placeholder.value = "";
        placeholder.textContent = displayLabel;
        placeholder.disabled = true;
        placeholder.selected = true;
        input.appendChild(placeholder);

        (field.options || []).forEach(optionItem => {
            const option = document.createElement("option");
            const optionValue = typeof optionItem === "string" ? optionItem : optionItem.value;
            option.value = optionValue || "";
            option.textContent = typeof optionItem === "string" ? optionItem : localizeText(optionItem.label, optionValue || "");
            input.appendChild(option);
        });
    } else if (type === "checkbox") {
        input.type = "checkbox";
        input.required = !!field.required;
    } else if (!capability.multiline) {
        input.type = type === "number" ? "text" : capability.inputType || "text";
        if (type === "number") input.inputMode = "decimal";
    }

    if (type !== "select" && type !== "checkbox") {
        input.placeholder = displayLabel;
    }

    if (maxLength > 0) {
        input.maxLength = maxLength;
    }

    if (capability.size) {
        input.dataset.inputSize = inputBoxSize.toString();
    }

    if (field.autocomplete) {
        input.autocomplete = field.autocomplete;
    }

    if (field.value) {
        input.value = field.value;
    }

    if (type === "checkbox") {
        const text = document.createElement("span");
        text.className = "sc-public-modal__checkbox-label";
        text.textContent = displayLabel;
        wrapper.classList.add("sc-public-modal__field--checkbox");
        wrapper.append(input, text);
    } else {
        wrapper.append(input);
    }

    const error = document.createElement("span");
    error.className = "sc-public-modal__field-error";
    error.setAttribute("role", "alert");
    error.hidden = true;
    wrapper.appendChild(error);

    input.addEventListener("blur", () => validatePublicModalField(input, true));
    input.addEventListener("input", () => {
        if (input.classList.contains("sc-public-modal__input--invalid")) {
            validatePublicModalField(input, true);
        }
    });
    input.addEventListener("change", () => validatePublicModalField(input, true));
    return wrapper;
}

function validatePublicModalField(field, showError) {
    if (!field || field.name === "__website") return true;

    const type = normalizePublicFieldType(field.dataset.fieldType || field.type);
    const value = field.type === "checkbox" ? (field.checked ? "true" : "false") : field.value.trim();
    let errorKey = "";

    if ((type === "number" || type === "date") && field.validity?.badInput) {
        errorKey = type === "number" ? "InvalidNumber" : "InvalidDate";
    } else if (field.required && (field.type === "checkbox" ? !field.checked : !value)) {
        errorKey = "FieldRequired";
    } else if (value) {
        if (type === "email" && !isValidPublicEmail(value)) errorKey = "InvalidEmail";
        else if (type === "tel" && !isValidPublicPhone(value)) errorKey = "InvalidPhone";
        else if (type === "number" && !/^[+-]?(?:\d+(?:\.\d+)?|\.\d+)$/.test(value)) errorKey = "InvalidNumber";
        else if (type === "date" && !isValidPublicDate(value)) errorKey = "InvalidDate";
        else if (type === "url" && !isValidPublicUrl(value)) errorKey = "InvalidUrl";
        else if (type === "select" && !Array.from(field.options).some(option => option.value === value && !option.disabled)) errorKey = "InvalidOption";
    }

    setPublicModalFieldError(field, errorKey ? publicUiText(errorKey, getPublicUiLanguage()) : "", showError);
    return !errorKey;
}

function setPublicModalFieldError(field, message, showError = true) {
    const wrapper = field.closest(".sc-public-modal__field");
    const error = wrapper?.querySelector(".sc-public-modal__field-error");
    const invalid = !!message;
    field.classList.toggle("sc-public-modal__input--invalid", invalid);
    field.setAttribute("aria-invalid", invalid ? "true" : "false");
    if (!error) return;
    error.textContent = showError ? message : "";
    error.hidden = !showError || !invalid;
}

function isValidPublicEmail(value) {
    if (value.length > 254 || /\s/.test(value)) return false;
    const at = value.lastIndexOf("@");
    if (at <= 0 || at > 64 || at !== value.indexOf("@")) return false;
    const local = value.slice(0, at);
    const domain = value.slice(at + 1);
    if (local.startsWith(".") || local.endsWith(".") || local.includes("..")) return false;
    return /^[A-Z0-9.!#$%&'*+/=?^_`{|}~-]+$/i.test(local) &&
        /^(?:[A-Z0-9](?:[A-Z0-9-]{0,61}[A-Z0-9])?\.)+[A-Z0-9](?:[A-Z0-9-]{0,61}[A-Z0-9])?$/i.test(domain);
}

function isValidPublicPhone(value) {
    if (value.length > 40 || !/^\+?[0-9().\-\s]+$/.test(value)) return false;
    const digitCount = (value.match(/\d/g) || []).length;
    return digitCount >= 6 && digitCount <= 15;
}

function isValidPublicDate(value) {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
    const [year, month, day] = value.split("-").map(Number);
    const date = new Date(Date.UTC(year, month - 1, day));
    return date.getUTCFullYear() === year && date.getUTCMonth() === month - 1 && date.getUTCDate() === day;
}

function isValidPublicUrl(value) {
    try {
        const url = new URL(value);
        return ["http:", "https:"].includes(url.protocol) && !!url.hostname;
    } catch {
        return false;
    }
}

function applyPublicModalServerErrors(form, errors) {
    if (!errors || typeof errors !== "object") return false;
    let applied = false;
    Object.entries(errors).forEach(([key, message]) => {
        const field = Array.from(form.elements).find(element => element.name?.toLowerCase() === key.toLowerCase());
        if (!field) return;
        setPublicModalFieldError(field, message || publicUiText("InvalidValue", getPublicUiLanguage()), true);
        applied = true;
    });
    return applied;
}

async function submitPublicModalForm(form, submit, status) {
    const type = form.dataset.modalType || "expert";
    const sourcePage = window.location.href;
    const data = {};
    const honeypot = form.querySelector("[name='__website']")?.value || "";

    form.querySelectorAll("input, textarea, select").forEach(field => {
        if (field.name && field.name !== "__website") {
            data[field.name] = field.type === "checkbox"
                ? (field.checked ? "true" : "false")
                : field.value.trim();
        }
    });

    status.textContent = "";
    status.className = "sc-public-modal__status";

    const fields = Array.from(form.querySelectorAll("input, textarea, select"))
        .filter(field => field.name !== "__website");
    const valid = fields.map(field => validatePublicModalField(field, true)).every(Boolean);

    if (!valid) {
        status.textContent = publicUiText("CorrectFields", getPublicUiLanguage());
        status.classList.add("sc-public-modal__status--error");
        form.querySelector(".sc-public-modal__input--invalid")?.focus();
        return;
    }

    submit.disabled = true;
    submit.textContent = publicUiText("Submitting", getPublicUiLanguage());

    try {
        const endpoint = type === "sync"
            ? `api/public/forms/modal/${encodeURIComponent(type)}`
            : `api/public/forms/${encodeURIComponent(type)}/submit`;
        const response = await fetch(buildPublicApiUrl(endpoint), {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ data, language: getPublicUiLanguage(), sourcePage, honeypot })
        });

        const result = await response.json().catch(() => null);

        if (!response.ok) {
            const fieldErrors = result?.fieldErrors || result?.FieldErrors || result?.data?.fieldErrors || result?.Data?.FieldErrors;
            if (applyPublicModalServerErrors(form, fieldErrors)) {
                status.textContent = publicUiText("CorrectFields", getPublicUiLanguage());
                status.classList.add("sc-public-modal__status--error");
                form.querySelector(".sc-public-modal__input--invalid")?.focus();
                return;
            }
            throw new Error(result?.message || result?.Message || "Submit failed");
        }

        form.reset();
        const redirectUrl = result?.data?.redirectUrl || result?.Data?.RedirectUrl;
        if (type === "sync" && redirectUrl) {
            status.textContent = publicUiText("LoginRedirecting", getPublicUiLanguage());
            status.classList.add("sc-public-modal__status--success");
            window.setTimeout(() => {
                window.location.href = redirectUrl;
            }, 400);
            return;
        }

        status.textContent = publicUiText("SubmitSuccess", getPublicUiLanguage());
        status.classList.add("sc-public-modal__status--success");
    } catch (error) {
        status.textContent = error?.message || publicUiText("SubmitError", getPublicUiLanguage());
        status.classList.add("sc-public-modal__status--error");
    } finally {
        submit.disabled = false;
        submit.textContent = submit.dataset.defaultText || "Submit";
    }
}

function getPublicUiLanguage() {
    try {
        const requested = normalizePublicUiLanguage(new URLSearchParams(window.location.search).get("lang"));
        if (requested) return requested;

        const active = normalizePublicUiLanguage(window.cmsPublicLanguage);
        if (active) return active;

        const documentLang = normalizePublicUiLanguage(document.documentElement?.lang);
        if (documentLang) return documentLang;

        const stored = normalizePublicUiLanguage(readStoredPublicLanguage());
        return stored || "en";
    } catch {
        return "en";
    }
}

function readStoredPublicLanguage() {
    const raw = window.localStorage.getItem("lang");
    if (!raw) return "";

    try {
        const parsed = JSON.parse(raw);
        return typeof parsed === "string" ? parsed : "";
    } catch {
        return raw;
    }
}

function normalizePublicUiLanguage(value) {
    const lang = (value || "").toString().trim().toLowerCase();
    return /^[a-z]{2,8}(-[a-z0-9]{2,8})*$/.test(lang) ? lang : "";
}

function publicUiText(key, lang) {
    const text = {
        QuoteTitle: { en: "Get a Quote", vi: "NhÃ¡ÂºÂ­n bÃƒÂ¡o giÃƒÂ¡" },
        QuoteIntro: { en: "Fill out the form below and our sales team will contact you shortly.", vi: "Ã„ÂiÃ¡Â»Ân thÃƒÂ´ng tin bÃƒÂªn dÃ†Â°Ã¡Â»â€ºi vÃƒÂ  Ã„â€˜Ã¡Â»â„¢i ngÃ…Â© tÃ†Â° vÃ¡ÂºÂ¥n sÃ¡ÂºÂ½ liÃƒÂªn hÃ¡Â»â€¡ vÃ¡Â»â€ºi bÃ¡ÂºÂ¡n sÃ¡Â»â€ºm." },
        ExpertTitle: { en: "Talk to an Expert", vi: "Trao Ã„â€˜Ã¡Â»â€¢i vÃ¡Â»â€ºi chuyÃƒÂªn gia" },
        ExpertIntro: { en: "Our specialists are here to answer your questions and help you find the best solution.", vi: "ChuyÃƒÂªn gia cÃ¡Â»Â§a chÃƒÂºng tÃƒÂ´i sÃ¡ÂºÂ½ hÃ¡Â»â€” trÃ¡Â»Â£ cÃƒÂ¢u hÃ¡Â»Âi vÃƒÂ  Ã„â€˜Ã¡Â»Â xuÃ¡ÂºÂ¥t giÃ¡ÂºÂ£i phÃƒÂ¡p phÃƒÂ¹ hÃ¡Â»Â£p." },
        SyncTitle: { en: "Login to SyncHub", vi: "Ã„ÂÃ„Æ’ng nhÃ¡ÂºÂ­p SyncHub" },
        SyncIntro: { en: "Access your dashboard and start managing shipments seamlessly.", vi: "Truy cÃ¡ÂºÂ­p bÃ¡ÂºÂ£ng Ã„â€˜iÃ¡Â»Âu khiÃ¡Â»Æ’n Ã„â€˜Ã¡Â»Æ’ quÃ¡ÂºÂ£n lÃƒÂ½ lÃƒÂ´ hÃƒÂ ng liÃ¡Â»Ân mÃ¡ÂºÂ¡ch." },
        Submit: { en: "Submit", vi: "GÃ¡Â»Â­i" },
        SubmitRequest: { en: "Submit Request", vi: "GÃ¡Â»Â­i yÃƒÂªu cÃ¡ÂºÂ§u" },
        Login: { en: "Login", vi: "Ã„ÂÃ„Æ’ng nhÃ¡ÂºÂ­p" },
        SelectService: { en: "Select Service", vi: "ChÃ¡Â»Ân dÃ¡Â»â€¹ch vÃ¡Â»Â¥" },
        Route: { en: "Route / Volume / Duration", vi: "TuyÃ¡ÂºÂ¿n / KhÃ¡Â»â€˜i lÃ†Â°Ã¡Â»Â£ng / ThÃ¡Â»Âi gian" },
        Email: { en: "Email", vi: "Email" },
        EmailAddress: { en: "Email Address", vi: "Ã„ÂÃ¡Â»â€¹a chÃ¡Â»â€° email" },
        Phone: { en: "Phone Number", vi: "SÃ¡Â»â€˜ Ã„â€˜iÃ¡Â»â€¡n thoÃ¡ÂºÂ¡i" },
        FullName: { en: "Full Name", vi: "HÃ¡Â»Â vÃƒÂ  tÃƒÂªn" },
        CompanyName: { en: "Company Name", vi: "TÃƒÂªn cÃƒÂ´ng ty" },
        YourMessage: { en: "Your Message", vi: "NÃ¡Â»â„¢i dung" },
        Username: { en: "Email / Username", vi: "Email / TÃƒÂªn Ã„â€˜Ã„Æ’ng nhÃ¡ÂºÂ­p" },
        Password: { en: "Password", vi: "MÃ¡ÂºÂ­t khÃ¡ÂºÂ©u" },
        ForgotPassword: { en: "Forgot password?", vi: "QuÃƒÂªn mÃ¡ÂºÂ­t khÃ¡ÂºÂ©u?" },
        RequiredFields: { en: "Please complete all required fields.", vi: "Vui lÃƒÂ²ng Ã„â€˜iÃ¡Â»Ân Ã„â€˜Ã¡ÂºÂ§y Ã„â€˜Ã¡Â»Â§ cÃƒÂ¡c trÃ†Â°Ã¡Â»Âng bÃ¡ÂºÂ¯t buÃ¡Â»â„¢c." },
        CorrectFields: { en: "Correct the highlighted fields.", vi: "Vui l\u00f2ng s\u1eeda c\u00e1c tr\u01b0\u1eddng \u0111\u01b0\u1ee3c \u0111\u00e1nh d\u1ea5u.", zh: "\u8bf7\u66f4\u6b63\u6807\u8bb0\u7684\u5b57\u6bb5\u3002" },
        FieldRequired: { en: "This field is required.", vi: "Tr\u01b0\u1eddng n\u00e0y l\u00e0 b\u1eaft bu\u1ed9c.", zh: "\u6b64\u5b57\u6bb5\u4e3a\u5fc5\u586b\u9879\u3002" },
        InvalidEmail: { en: "Enter a complete email address, such as name@company.com.", vi: "Nh\u1eadp email \u0111\u1ea7y \u0111\u1ee7, v\u00ed d\u1ee5 ten@congty.com.", zh: "\u8bf7\u8f93\u5165\u5b8c\u6574\u7684\u7535\u5b50\u90ae\u4ef6\u5730\u5740\u3002" },
        InvalidPhone: { en: "Enter a valid phone number containing 6 to 15 digits.", vi: "Nh\u1eadp s\u1ed1 \u0111i\u1ec7n tho\u1ea1i h\u1ee3p l\u1ec7 c\u00f3 t\u1eeb 6 \u0111\u1ebfn 15 ch\u1eef s\u1ed1.", zh: "\u8bf7\u8f93\u5165\u5305\u542b 6 \u5230 15 \u4f4d\u6570\u5b57\u7684\u6709\u6548\u7535\u8bdd\u53f7\u7801\u3002" },
        InvalidNumber: { en: "Enter a valid number.", vi: "Ch\u1ec9 nh\u1eadp m\u1ed9t s\u1ed1 h\u1ee3p l\u1ec7.", zh: "\u8bf7\u8f93\u5165\u6709\u6548\u6570\u5b57\u3002" },
        InvalidDate: { en: "Choose a valid date.", vi: "Ch\u1ecdn m\u1ed9t ng\u00e0y h\u1ee3p l\u1ec7.", zh: "\u8bf7\u9009\u62e9\u6709\u6548\u65e5\u671f\u3002" },
        InvalidUrl: { en: "Enter a complete http or https URL.", vi: "Nh\u1eadp \u0111\u1ecba ch\u1ec9 http ho\u1eb7c https \u0111\u1ea7y \u0111\u1ee7.", zh: "\u8bf7\u8f93\u5165\u5b8c\u6574\u7684 http \u6216 https \u5730\u5740\u3002" },
        InvalidOption: { en: "Choose a value from the available options.", vi: "Ch\u1ecdn m\u1ed9t gi\u00e1 tr\u1ecb c\u00f3 trong danh s\u00e1ch.", zh: "\u8bf7\u4ece\u5217\u8868\u4e2d\u9009\u62e9\u4e00\u4e2a\u503c\u3002" },
        InvalidValue: { en: "Enter a valid value.", vi: "Nh\u1eadp m\u1ed9t gi\u00e1 tr\u1ecb h\u1ee3p l\u1ec7.", zh: "\u8bf7\u8f93\u5165\u6709\u6548\u503c\u3002" },
        Submitting: { en: "Submitting...", vi: "Ã„Âang gÃ¡Â»Â­i..." },
        SubmitSuccess: { en: "Thank you, your message has been sent.", vi: "CÃ¡ÂºÂ£m Ã†Â¡n bÃ¡ÂºÂ¡n, thÃƒÂ´ng tin Ã„â€˜ÃƒÂ£ Ã„â€˜Ã†Â°Ã¡Â»Â£c gÃ¡Â»Â­i." },
        SubmitError: { en: "Something went wrong. Please try again.", vi: "Ã„ÂÃƒÂ£ xÃ¡ÂºÂ£y ra lÃ¡Â»â€”i. Vui lÃƒÂ²ng thÃ¡Â»Â­ lÃ¡ÂºÂ¡i." },
        LoginRedirecting: { en: "Login successful. Redirecting...", vi: "Ã„ÂÃ„Æ’ng nhÃ¡ÂºÂ­p thÃƒÂ nh cÃƒÂ´ng. Ã„Âang chuyÃ¡Â»Æ’n hÃ†Â°Ã¡Â»â€ºng..." }
    };

    return text[key]?.[lang] || text[key]?.en || key;
}

function buildPublicApiUrl(path) {
    const base = (window.cmsPublicApiBaseUrl || "").trim();
    if (!base) return `/${path}`;
    return `${base.replace(/\/+$/, "")}/${path.replace(/^\/+/, "")}`;
}

function isSafeHref(href) {
    try {
        const url = new URL(href, window.location.origin);
        return ["http:", "https:", "mailto:", "tel:"].includes(url.protocol);
    } catch {
        return false;
    }
}
