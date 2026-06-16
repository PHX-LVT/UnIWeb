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
    document.querySelectorAll("a[href^='modal:'],a[href='#modal'],a[href='#quote']").forEach(link => {
        if (link.dataset.modalBound === "true") return;
        link.dataset.modalBound = "true";
        link.addEventListener("click", e => {
            e.preventDefault();
            openPublicModal(resolveModalType(link));
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

function resolveModalType(link) {
    const href = (link.getAttribute("href") || "").toLowerCase();
    const label = (link.textContent || "").toLowerCase();

    if (href.includes("sync") || label.includes("sync")) return "sync";
    if (href.includes("quote") || label.includes("quote")) return "quote";
    if (href.includes("expert") || href === "#modal" || label.includes("expert")) return "expert";

    return "expert";
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

function buildPublicModalDialog(type) {
    const config = getPublicModalConfig(type);
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
    title.textContent = config.title;
    header.append(title, close);

    const text = document.createElement("p");
    text.className = "sc-public-modal__intro";
    text.textContent = config.intro;

    const form = document.createElement("form");
    form.className = "sc-public-modal__form";
    form.dataset.modalType = config.type;
    form.addEventListener("submit", async e => {
        e.preventDefault();
        await submitPublicModalForm(form, submit, status);
    });

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

    const input = field.type === "select"
        ? document.createElement("select")
        : field.multiline
            ? document.createElement("textarea")
            : document.createElement("input");

    input.name = field.name;
    input.required = !!field.required;

    if (field.type === "select") {
        const placeholder = document.createElement("option");
        placeholder.value = "";
        placeholder.textContent = field.label;
        placeholder.disabled = true;
        placeholder.selected = true;
        input.appendChild(placeholder);

        (field.options || []).forEach(optionText => {
            const option = document.createElement("option");
            option.value = optionText;
            option.textContent = optionText;
            input.appendChild(option);
        });
    } else if (!field.multiline) {
        input.type = field.type || "text";
    }

    if (field.type !== "select") {
        input.placeholder = field.label;
    }

    if (field.autocomplete) {
        input.autocomplete = field.autocomplete;
    }

    if (field.value) {
        input.value = field.value;
    }

    wrapper.append(input);
    return wrapper;
}

async function submitPublicModalForm(form, submit, status) {
    const type = form.dataset.modalType || "expert";
    const data = { PageUrl: window.location.href };

    form.querySelectorAll("input, textarea, select").forEach(field => {
        if (field.name) {
            data[field.name] = field.value.trim();
        }
    });

    status.textContent = "";
    status.className = "sc-public-modal__status";

    const missingRequired = Array.from(form.querySelectorAll("input, textarea, select"))
        .some(field => field.required && !field.value.trim());

    if (missingRequired) {
        status.textContent = publicUiText("RequiredFields", getPublicUiLanguage());
        status.classList.add("sc-public-modal__status--error");
        return;
    }

    submit.disabled = true;
    submit.textContent = publicUiText("Submitting", getPublicUiLanguage());

    try {
        const response = await fetch(buildPublicApiUrl(`api/public/forms/modal/${encodeURIComponent(type)}`), {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ data })
        });

        const result = await response.json().catch(() => null);

        if (!response.ok) {
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
        const lang = window.localStorage.getItem("lang");
        return lang === "vi" || lang === "cn" ? lang : "en";
    } catch {
        return "en";
    }
}

function publicUiText(key, lang) {
    const text = {
        QuoteTitle: { en: "Get a Quote", vi: "Nhận báo giá" },
        QuoteIntro: { en: "Fill out the form below and our sales team will contact you shortly.", vi: "Điền thông tin bên dưới và đội ngũ tư vấn sẽ liên hệ với bạn sớm." },
        ExpertTitle: { en: "Talk to an Expert", vi: "Trao đổi với chuyên gia" },
        ExpertIntro: { en: "Our specialists are here to answer your questions and help you find the best solution.", vi: "Chuyên gia của chúng tôi sẽ hỗ trợ câu hỏi và đề xuất giải pháp phù hợp." },
        SyncTitle: { en: "Login to SyncHub", vi: "Đăng nhập SyncHub" },
        SyncIntro: { en: "Access your dashboard and start managing shipments seamlessly.", vi: "Truy cập bảng điều khiển để quản lý lô hàng liền mạch." },
        Submit: { en: "Submit", vi: "Gửi" },
        SubmitRequest: { en: "Submit Request", vi: "Gửi yêu cầu" },
        Login: { en: "Login", vi: "Đăng nhập" },
        SelectService: { en: "Select Service", vi: "Chọn dịch vụ" },
        Route: { en: "Route / Volume / Duration", vi: "Tuyến / Khối lượng / Thời gian" },
        Email: { en: "Email", vi: "Email" },
        EmailAddress: { en: "Email Address", vi: "Địa chỉ email" },
        Phone: { en: "Phone Number", vi: "Số điện thoại" },
        FullName: { en: "Full Name", vi: "Họ và tên" },
        CompanyName: { en: "Company Name", vi: "Tên công ty" },
        YourMessage: { en: "Your Message", vi: "Nội dung" },
        Username: { en: "Email / Username", vi: "Email / Tên đăng nhập" },
        Password: { en: "Password", vi: "Mật khẩu" },
        ForgotPassword: { en: "Forgot password?", vi: "Quên mật khẩu?" },
        RequiredFields: { en: "Please complete all required fields.", vi: "Vui lòng điền đầy đủ các trường bắt buộc." },
        Submitting: { en: "Submitting...", vi: "Đang gửi..." },
        SubmitSuccess: { en: "Thank you, your message has been sent.", vi: "Cảm ơn bạn, thông tin đã được gửi." },
        SubmitError: { en: "Something went wrong. Please try again.", vi: "Đã xảy ra lỗi. Vui lòng thử lại." },
        LoginRedirecting: { en: "Login successful. Redirecting...", vi: "Đăng nhập thành công. Đang chuyển hướng..." }
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

