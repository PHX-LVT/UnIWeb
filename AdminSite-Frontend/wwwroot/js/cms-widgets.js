window.cmsWidgets = window.cmsWidgets || {};

window.cmsWidgets.init = () => {
    initCounters();
    initCarousels();
    initLibraryVideos();
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
    document.querySelectorAll("[data-sc-library-video]:not([data-bound])").forEach(trigger => {
        trigger.dataset.bound = "true";
        trigger.addEventListener("click", e => {
            e.preventDefault();
            const embedUrl = resolveVideoEmbedUrl(trigger.dataset.videoUrl || "");
            if (!embedUrl) return;
            openLibraryVideoModal(embedUrl, trigger.dataset.videoTitle || "Video");
        });
    });
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
    const modal = ensurePublicModal();
    document.querySelectorAll("a[href^='modal:'],a[href='#modal'],a[href='#quote']").forEach(link => {
        if (link.dataset.modalBound === "true") return;
        link.dataset.modalBound = "true";
        link.addEventListener("click", e => {
            e.preventDefault();
            document.body.classList.add("sc-modal-open");
            modal.classList.add("open");
        });
    });
}

function ensurePublicModal() {
    let modal = document.querySelector(".sc-public-modal");
    if (modal) return modal;

    modal = document.createElement("div");
    modal.className = "sc-public-modal";
    modal.setAttribute("role", "dialog");
    modal.setAttribute("aria-modal", "true");
    modal.appendChild(buildPublicModalDialog());
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

function closePublicModal(modal) {
    modal.classList.remove("open");
    document.body.classList.remove("sc-modal-open");
}

function openLibraryVideoModal(embedUrl, titleText) {
    const modal = ensureLibraryVideoModal();
    const title = modal.querySelector(".sc-video-modal__title");
    const frame = modal.querySelector(".sc-video-modal__frame");

    if (title) title.textContent = titleText;
    if (frame) {
        frame.replaceChildren();
        const iframe = document.createElement("iframe");
        iframe.src = embedUrl;
        iframe.title = titleText || "Video";
        iframe.allow = "accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share";
        iframe.allowFullscreen = true;
        frame.appendChild(iframe);
    }

    document.body.classList.add("sc-modal-open");
    modal.classList.add("open");
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

    header.append(title, close);
    dialog.append(header, frame);
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
    modal.querySelector(".sc-video-modal__frame")?.replaceChildren();
    document.body.classList.remove("sc-modal-open");
}

function resolveVideoEmbedUrl(rawUrl) {
    try {
        const url = new URL(rawUrl, window.location.origin);
        const host = url.hostname.toLowerCase().replace(/^www\./, "");

        if (host === "youtu.be") {
            return buildYouTubeEmbed(url.pathname.split("/").filter(Boolean)[0]);
        }

        if (host === "youtube.com" || host.endsWith(".youtube.com")) {
            const path = url.pathname.split("/").filter(Boolean);
            return buildYouTubeEmbed(
                url.searchParams.get("v") ||
                (path[0] === "embed" ? path[1] : "") ||
                (path[0] === "shorts" ? path[1] : "") ||
                (path[0] === "live" ? path[1] : "")
            );
        }

        if (host === "vimeo.com" || host.endsWith(".vimeo.com")) {
            const id = url.pathname.split("/").find(part => /^\d+$/.test(part));
            return id ? `https://player.vimeo.com/video/${id}?autoplay=1` : null;
        }
    } catch {
        return null;
    }

    return null;
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

function buildPublicModalDialog() {
    const dialog = document.createElement("div");
    dialog.className = "sc-public-modal__dialog";

    const header = document.createElement("div");
    header.className = "sc-public-modal__header";

    const close = document.createElement("button");
    close.type = "button";
    close.className = "sc-public-modal__close";
    close.setAttribute("aria-label", "Close");
    close.textContent = "x";

    const title = document.createElement("h2");
    title.textContent = "Contact Us";
    header.append(title, close);

    const text = document.createElement("p");
    text.className = "sc-public-modal__intro";
    text.textContent = "Send your request and our team will respond shortly.";

    const form = document.createElement("form");
    form.className = "sc-public-modal__form";
    form.addEventListener("submit", e => {
        e.preventDefault();
    });

    const name = document.createElement("input");
    name.placeholder = "Name";
    name.autocomplete = "name";

    const email = document.createElement("input");
    email.placeholder = "Email";
    email.type = "email";
    email.autocomplete = "email";

    const message = document.createElement("textarea");
    message.placeholder = "Message";

    const submit = document.createElement("button");
    submit.type = "submit";
    submit.className = "sc-btn sc-btn--filled";
    submit.textContent = "Submit";

    form.append(name, email, message, submit);
    dialog.append(header, text, form);
    return dialog;
}

function isSafeHref(href) {
    try {
        const url = new URL(href, window.location.origin);
        return ["http:", "https:", "mailto:", "tel:"].includes(url.protocol);
    } catch {
        return false;
    }
}
