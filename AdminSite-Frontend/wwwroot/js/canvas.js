window.initSortable = (container, dotnet) => {
    if (!container || typeof Sortable === "undefined") return;

    if (window.__ezSectionSortable) {
        window.__ezSectionSortable.destroy();
        window.__ezSectionSortable = null;
    }

    window.__ezSectionSortable = Sortable.create(container, {
        handle: ".ez-section-handle",
        animation: 150,
        onEnd: () => {
            const ids = [...container.querySelectorAll("[data-section-id]")]
                .map(el => el.getAttribute("data-section-id"))
                .filter(Boolean);
            dotnet.invokeMethodAsync("OnReordered", ids).catch(() => {});
        }
    });
};

window.contentRichTextEditor = (() => {
    const editors = {};

    const focus = (entry) => {
        if (entry && entry.el) entry.el.focus();
    };

    const rememberSelection = (entry) => {
        const selection = window.getSelection();
        if (!entry || !selection || selection.rangeCount === 0) return;
        const range = selection.getRangeAt(0);
        if (entry.el.contains(range.commonAncestorContainer)) {
            entry.range = range.cloneRange();
        }
    };

    const restoreSelection = (entry) => {
        if (!entry || !entry.range) {
            focus(entry);
            return;
        }

        const selection = window.getSelection();
        selection.removeAllRanges();
        selection.addRange(entry.range);
    };

    const useCssFormatting = () => {
        document.execCommand("styleWithCSS", false, true);
    };

    const sanitizeLink = (value) => {
        if (!value) return null;
        const trimmed = value.trim();
        if (/^(https?:\/\/|mailto:|\/)/i.test(trimmed)) return trimmed;
        return null;
    };

    const rgbToHex = (value) => {
        const match = String(value || "").match(/rgba?\((\d+),\s*(\d+),\s*(\d+)/i);
        if (!match) return /^#[0-9a-f]{6}$/i.test(value || "") ? value : null;
        return "#" + [match[1], match[2], match[3]]
            .map(part => Math.max(0, Math.min(255, Number.parseInt(part, 10) || 0)).toString(16).padStart(2, "0"))
            .join("");
    };

    const getFormatElement = (entry) => {
        const selection = window.getSelection();
        if (selection && selection.rangeCount > 0) {
            const range = selection.getRangeAt(0);
            if (entry.el.contains(range.commonAncestorContainer)) {
                let node = range.startContainer;
                if (node.nodeType === Node.TEXT_NODE) node = node.parentElement;
                if (node && node.nodeType === Node.ELEMENT_NODE) return node;
            }
        }

        return entry.el.querySelector("[style*='font-size'],[style*='color'],font[size],font[color],span,strong,b,em,i,u,a") ||
            entry.el.firstElementChild ||
            entry.el;
    };

    const reportFormat = (entry) => {
        if (!entry || !entry.el || !entry.dotnet) return;
        const element = getFormatElement(entry);
        if (!element) return;

        const style = window.getComputedStyle(element);
        const fontSize = Math.round(Number.parseFloat(style.fontSize || "16")) || 16;
        const color = rgbToHex(style.color) || "#0f3460";
        entry.dotnet.invokeMethodAsync("OnFormatChanged", fontSize, color).catch(() => {});
    };

    return {
        init(id, element, dotnet, value) {
            if (!id || !element || !dotnet) return;
            if (editors[id]) this.dispose(id);

            element.innerHTML = value || "";
            const onInput = () => {
                dotnet.invokeMethodAsync("OnEditorInput", element.innerHTML || "").catch(() => {});
                reportFormat(editors[id]);
            };
            const onSelectionChange = () => {
                const entry = editors[id];
                rememberSelection(entry);
                reportFormat(entry);
            };
            element.addEventListener("input", onInput);
            element.addEventListener("mouseup", onSelectionChange);
            element.addEventListener("keyup", onSelectionChange);
            element.addEventListener("focus", onSelectionChange);
            editors[id] = { el: element, dotnet, onInput, onSelectionChange, range: null };
            setTimeout(() => reportFormat(editors[id]), 0);
        },
        setValue(id, value) {
            const entry = editors[id];
            if (!entry || document.activeElement === entry.el) return;
            if ((entry.el.innerHTML || "") !== (value || "")) entry.el.innerHTML = value || "";
            reportFormat(entry);
        },
        async flushAll() {
            const updates = Object.values(editors)
                .filter(entry => entry && entry.el && entry.dotnet)
                .map(entry => entry.dotnet.invokeMethodAsync("OnEditorInput", entry.el.innerHTML || "").catch(() => {}));
            await Promise.all(updates);
        },
        exec(id, command) {
            const entry = editors[id];
            if (!entry || !command) return;
            restoreSelection(entry);
            useCssFormatting();
            document.execCommand(command, false, null);
            entry.onInput();
            rememberSelection(entry);
            reportFormat(entry);
        },
        setFontSize(id, size) {
            const entry = editors[id];
            if (!entry) return;

            const px = Math.min(72, Math.max(10, Number.parseInt(size, 10) || 16));
            restoreSelection(entry);
            useCssFormatting();
            document.execCommand("fontSize", false, "7");
            entry.el.querySelectorAll("span[style], font[size='7']").forEach(node => {
                if (node.tagName === "FONT") {
                    const span = document.createElement("span");
                    span.style.fontSize = `${px}px`;
                    while (node.firstChild) span.appendChild(node.firstChild);
                    node.replaceWith(span);
                    return;
                }

                if (node.style.fontSize === "xxx-large" || node.style.fontSize === "-webkit-xxx-large") {
                    node.style.fontSize = `${px}px`;
                }
            });
            entry.onInput();
            rememberSelection(entry);
            reportFormat(entry);
        },
        setColor(id, color) {
            const entry = editors[id];
            if (!entry || !/^#[0-9a-f]{6}$/i.test(color || "")) return;
            restoreSelection(entry);
            useCssFormatting();
            document.execCommand("foreColor", false, color);
            entry.onInput();
            rememberSelection(entry);
            reportFormat(entry);
        },
        createLink(id) {
            const entry = editors[id];
            if (!entry) return;
            const href = sanitizeLink(window.prompt("Enter link URL"));
            if (!href) return;
            restoreSelection(entry);
            document.execCommand("createLink", false, href);
            entry.el.querySelectorAll("a[href]").forEach(a => {
                a.setAttribute("target", "_blank");
                a.setAttribute("rel", "noopener");
            });
            entry.onInput();
            rememberSelection(entry);
            reportFormat(entry);
        },
        dispose(id) {
            const entry = editors[id];
            if (!entry) return;
            entry.el.removeEventListener("input", entry.onInput);
            entry.el.removeEventListener("mouseup", entry.onSelectionChange);
            entry.el.removeEventListener("keyup", entry.onSelectionChange);
            entry.el.removeEventListener("focus", entry.onSelectionChange);
            delete editors[id];
        }
    };
})();

window.contentPreviewViewport = (() => {
    const entries = {};
    const desktopWidth = 1440;

    const update = (root, width) => {
        if (!root) return;
        const stage = root.querySelector("[data-content-preview-stage]");
        const iframe = root.querySelector("iframe");
        if (!stage || !iframe) return;

        const availableWidth = Math.max(root.clientWidth - 2, 320);
        const scale = Math.min(1, availableWidth / width);
        const viewportHeight = Math.max(root.clientHeight || 900, window.innerHeight * 0.72);
        const iframeHeight = Math.max(1100, Math.ceil(viewportHeight / Math.max(scale, 0.1)));

        stage.style.width = `${Math.ceil(width * scale)}px`;
        stage.style.height = `${Math.ceil(iframeHeight * scale)}px`;
        iframe.style.width = `${width}px`;
        iframe.style.height = `${iframeHeight}px`;
        iframe.style.transform = `scale(${scale})`;
        iframe.style.transformOrigin = "top left";
    };

    return {
        init(key, root, width) {
            if (!key || !root) return;
            this.dispose(key);

            const previewWidth = Number(width) > 0 ? Number(width) : desktopWidth;
            const resize = () => update(root, previewWidth);
            const observer = typeof ResizeObserver !== "undefined"
                ? new ResizeObserver(resize)
                : null;

            observer?.observe(root);
            window.addEventListener("resize", resize);
            entries[key] = { root, resize, observer };

            resize();
            setTimeout(resize, 50);
            setTimeout(resize, 250);
        },
        refresh(key) {
            const entry = entries[key];
            if (entry) entry.resize();
        },
        dispose(key) {
            const entry = entries[key];
            if (!entry) return;
            entry.observer?.disconnect();
            window.removeEventListener("resize", entry.resize);
            delete entries[key];
        }
    };
})();

window.initSlotSortable = (slotContainer, dotnet, slotId) => {
    if (!slotContainer || typeof Sortable === "undefined") return;

    Sortable.create(slotContainer, {
        handle: ".ez-block-handle",
        group: "blocks",
        animation: 150,
        onEnd: () => {
            const ids = [...slotContainer.querySelectorAll("[data-block-id]")]
                .map(el => el.getAttribute("data-block-id"))
                .filter(Boolean);
            dotnet.invokeMethodAsync("OnBlockReordered", slotId, ids).catch(() => {});
        }
    });
};

window.initAdminSortable = (key, container, dotnet, methodName, idAttribute, handleSelector, context, filterSelector) => {
    if (!key || !container || !dotnet || !methodName || !idAttribute || typeof Sortable === "undefined") return;

    window.__ezAdminSortables = window.__ezAdminSortables || {};
    if (window.__ezAdminSortables[key]) {
        window.__ezAdminSortables[key].destroy();
        window.__ezAdminSortables[key] = null;
    }

    window.__ezAdminSortables[key] = Sortable.create(container, {
        handle: handleSelector || undefined,
        filter: filterSelector || undefined,
        preventOnFilter: false,
        draggable: `[${idAttribute}]`,
        animation: 150,
        onEnd: (event) => {
            if (event.oldIndex === event.newIndex) return;

            const ids = [...container.children]
                .filter(el => el.hasAttribute(idAttribute))
                .map(el => el.getAttribute(idAttribute))
                .filter(Boolean);
            if (context === undefined || context === null) {
                dotnet.invokeMethodAsync(methodName, ids).catch(() => {});
            } else {
                dotnet.invokeMethodAsync(methodName, context, ids).catch(() => {});
            }
        }
    });
};

window.initFooterLinkSortables = (root, dotnet) => {
    if (!root || !dotnet || typeof Sortable === "undefined") return;

    root.querySelectorAll("[data-footer-links-group-id]").forEach(container => {
        const groupId = container.getAttribute("data-footer-links-group-id");
        if (!groupId) return;

        const key = `footer-links-${groupId}`;
        window.__ezAdminSortables = window.__ezAdminSortables || {};
        if (window.__ezAdminSortables[key]) {
            window.__ezAdminSortables[key].destroy();
            window.__ezAdminSortables[key] = null;
        }

        window.__ezAdminSortables[key] = Sortable.create(container, {
            handle: ".footer-link-drag",
            draggable: ".footer-link-row",
            group: key,
            animation: 150,
            direction: "vertical",
            onEnd: (event) => {
                if (event.oldIndex === event.newIndex) return;

                const ids = [...container.children]
                    .filter(el => el.hasAttribute("data-footer-link-id"))
                    .map(el => el.getAttribute("data-footer-link-id"))
                    .filter(Boolean);
                dotnet.invokeMethodAsync("OnFooterLinksReordered", groupId, ids).catch(() => {});
            }
        });
    });
};

window.disposeFooterLinkSortables = () => {
    if (!window.__ezAdminSortables) return;

    Object.keys(window.__ezAdminSortables)
        .filter(key => key.startsWith("footer-links-"))
        .forEach(key => window.disposeAdminSortable(key));
};

window.disposeAdminSortable = (key) => {
    if (!window.__ezAdminSortables) return;
    if (key && window.__ezAdminSortables[key]) {
        window.__ezAdminSortables[key].destroy();
        window.__ezAdminSortables[key] = null;
        return;
    }

    if (!key) {
        Object.keys(window.__ezAdminSortables).forEach(sortableKey => {
            if (window.__ezAdminSortables[sortableKey]) {
                window.__ezAdminSortables[sortableKey].destroy();
                window.__ezAdminSortables[sortableKey] = null;
            }
        });
    }
};

window.initCanvasOverlay = function (dotNetRef) {
    if (window.__ezCanvasOverlayTimeouts) {
        window.__ezCanvasOverlayTimeouts.forEach(clearTimeout);
    }
    window.__ezCanvasOverlayTimeouts = [];

    const requestPositions = function () {
        const iframe = document.getElementById("ez-preview-iframe");
        if (!iframe || !iframe.contentWindow) return;

        try {
            if (typeof iframe.contentWindow.reportSectionPositions === "function") {
                iframe.contentWindow.reportSectionPositions();
            } else {
                iframe.contentWindow.postMessage({ type: "ez-request-section-positions" }, window.location.origin);
            }
        } catch {
            try {
                iframe.contentWindow.postMessage({ type: "ez-request-section-positions" }, window.location.origin);
            } catch {
            }
        }
    };

    if (window.__ezCanvasOverlayHandler) {
        window.removeEventListener("message", window.__ezCanvasOverlayHandler);
    }

    window.__ezCanvasOverlayHandler = function (e) {
        const iframe = document.getElementById("ez-preview-iframe");
        if (!iframe || e.source !== iframe.contentWindow || e.origin !== window.location.origin) return;
        if (e.data && e.data.type === "ez-section-positions") {
            dotNetRef.invokeMethodAsync(
                "UpdateSectionPositions",
                e.data.positions,
                e.data.documentHeight ?? 2000,
                e.data.blockPositions || [],
                e.data.zonePositions || []).catch(() => {});
        }
    };

    window.addEventListener("message", window.__ezCanvasOverlayHandler);

    const iframe = document.getElementById("ez-preview-iframe");
    if (iframe) {
        iframe.removeEventListener("load", window.__ezCanvasRequestPositions);
        window.__ezCanvasRequestPositions = requestPositions;
        iframe.addEventListener("load", window.__ezCanvasRequestPositions);
    }

    window.__ezCanvasOverlayTimeouts.push(setTimeout(requestPositions, 0));
    window.__ezCanvasOverlayTimeouts.push(setTimeout(requestPositions, 250));
    window.__ezCanvasOverlayTimeouts.push(setTimeout(requestPositions, 1000));
};

window.disposeCanvasOverlay = function () {
    if (window.__ezSectionSortable) {
        window.__ezSectionSortable.destroy();
        window.__ezSectionSortable = null;
    }

    if (window.__ezCanvasOverlayTimeouts) {
        window.__ezCanvasOverlayTimeouts.forEach(clearTimeout);
        window.__ezCanvasOverlayTimeouts = [];
    }

    if (window.__ezCanvasOverlayHandler) {
        window.removeEventListener("message", window.__ezCanvasOverlayHandler);
        window.__ezCanvasOverlayHandler = null;
    }

    const iframe = document.getElementById("ez-preview-iframe");
    if (iframe && window.__ezCanvasRequestPositions) {
        iframe.removeEventListener("load", window.__ezCanvasRequestPositions);
    }
    window.__ezCanvasRequestPositions = null;
};

window.reloadPreviewIframe = function () {
    const iframe = document.getElementById("ez-preview-iframe");
    if (iframe) iframe.src = iframe.src;
};

window.applyPreviewThemeCss = function (css) {
    const iframe = document.getElementById("ez-preview-iframe");
    if (!iframe || !iframe.contentDocument || !css) return;

    const doc = iframe.contentDocument;
    let style = iframe.contentDocument.getElementById("ez-live-theme-css");
    if (!style) {
        style = doc.createElement("style");
        style.id = "ez-live-theme-css";
        doc.head.appendChild(style);
    }

    style.textContent = css;

    const root = doc.documentElement;
    const declarations = css.match(/--[\w-]+\s*:\s*[^;]+;/g) || [];
    declarations.forEach(declaration => {
        const separator = declaration.indexOf(":");
        if (separator < 0) return;

        const name = declaration.slice(0, separator).trim();
        const value = declaration.slice(separator + 1).replace(/;$/, "").trim();
        if (name && value) root.style.setProperty(name, value);
    });

    window.__ezCanvasRequestPositions?.();
};

window.patchPreviewBlockLayout = function (blockId, x, y, w, h, leftPercent, topPx, widthPercent, heightPx, zIndex) {
    const iframe = document.getElementById("ez-preview-iframe");
    if (!iframe || !iframe.contentDocument || !blockId) return;

    const block = [...iframe.contentDocument.querySelectorAll("[data-block-id]")]
        .find(item => item.getAttribute("data-block-id") === blockId);
    if (!block) return;

    const safeX = Math.min(Math.max(Number.parseInt(x, 10) || 0, 0), 11);
    const safeY = Math.min(Math.max(Number.parseInt(y, 10) || 0, 0), 60);
    const safeW = Math.min(Math.max(Number.parseInt(w, 10) || 1, 1), 12);
    const safeH = Math.min(Math.max(Number.parseInt(h, 10) || 1, 1), 40);
    const exactLeft = Number.isFinite(Number(leftPercent)) ? Math.min(Math.max(Number(leftPercent), 0), 100) : safeX / 12 * 100;
    const exactTop = Number.isFinite(Number(topPx)) ? Math.min(Math.max(Number(topPx), 0), 10000) : safeY * 48;
    const exactWidth = Number.isFinite(Number(widthPercent)) ? Math.min(Math.max(Number(widthPercent), 1), 100) : safeW / 12 * 100;
    const exactHeight = Number.isFinite(Number(heightPx)) ? Math.min(Math.max(Number(heightPx), 24), 10000) : safeH * 48;

    block.style.setProperty("--sc-block-left", `${exactLeft}%`);
    block.style.setProperty("--sc-block-width", `${exactWidth}%`);
    block.style.setProperty("--sc-block-top", `${exactTop}px`);
    block.style.setProperty("--sc-block-min-height", `${exactHeight}px`);
    if (Number.isFinite(Number(zIndex))) block.style.zIndex = `${Math.min(Math.max(Number.parseInt(zIndex, 10), 0), 1000)}`;
    window.__ezCanvasRequestPositions?.();
};

window.initFreeformBlockEditor = function (container, dotnet, scale) {
    if (!container || !dotnet) return;

    container.__ezFreeformDotNet = dotnet;
    container.__ezFreeformScale = scale || 1;

    if (container.__ezFreeformInitialized) return;
    container.__ezFreeformInitialized = true;

    const snapThreshold = 8;

    function ensureGuide(axis) {
        const key = axis === "x" ? "__ezVerticalGuide" : "__ezHorizontalGuide";
        if (container[key]) return container[key];

        const guide = document.createElement("div");
        guide.className = axis === "x"
            ? "ez-arrange-guide ez-arrange-guide--vertical"
            : "ez-arrange-guide ez-arrange-guide--horizontal";
        container.appendChild(guide);
        container[key] = guide;
        return guide;
    }

    function showGuide(axis, position, bounds) {
        const guide = ensureGuide(axis);
        guide.style.display = "block";
        if (axis === "x") {
            guide.style.left = `${position}px`;
            guide.style.top = `${bounds.top}px`;
            guide.style.height = `${bounds.height}px`;
            return;
        }

        guide.style.top = `${position}px`;
        guide.style.left = `${bounds.left}px`;
        guide.style.width = `${bounds.width}px`;
    }

    function hideGuide(axis) {
        const guide = axis === "x" ? container.__ezVerticalGuide : container.__ezHorizontalGuide;
        if (guide) guide.style.display = "none";
    }

    function nearestSnap(value, candidates) {
        let best = null;
        candidates.forEach(candidate => {
            const distance = Math.abs(value - candidate.value);
            if (distance > snapThreshold || (best && distance >= best.distance)) return;
            best = { ...candidate, distance };
        });
        return best;
    }

    container.addEventListener("pointerdown", function (event) {
        const handle = event.target.closest(".ez-freeform-block-handle, .ez-freeform-block-resize");
        if (!handle) return;

        const block = handle.closest(".ez-freeform-block-overlay");
        if (!block) return;

        event.preventDefault();
        event.stopPropagation();
        block.setPointerCapture?.(event.pointerId);

        const mode = handle.classList.contains("ez-freeform-block-resize") ? "resize" : "drag";
        const containerRect = container.getBoundingClientRect();
        const renderedScale = container.offsetWidth > 0
            ? containerRect.width / container.offsetWidth
            : 0;
        const scale = renderedScale > 0 ? renderedScale : (container.__ezFreeformScale || 1);
        const start = {
            x: event.clientX,
            y: event.clientY,
            left: parseFloat(block.style.left || "0"),
            top: parseFloat(block.style.top || "0"),
            width: parseFloat(block.style.width || "0"),
            height: parseFloat(block.style.height || "0"),
            sectionLeft: parseFloat(block.dataset.sectionLeft || "0"),
            sectionTop: parseFloat(block.dataset.sectionTop || "0"),
            sectionWidth: Math.max(parseFloat(block.dataset.sectionWidth || "1"), 1),
            sectionHeight: Math.max(parseFloat(block.dataset.sectionHeight || "1"), 1)
        };

        if (start.width <= 0 || start.height <= 0 || start.sectionWidth <= 1 || start.sectionHeight <= 1) return;

        block.classList.add("ez-freeform-block-overlay--editing");
        document.body.classList.add("ez-arranging-block");

        const bounds = {
            left: start.sectionLeft,
            top: start.sectionTop,
            width: start.sectionWidth,
            height: start.sectionHeight,
            right: start.sectionLeft + start.sectionWidth,
            bottom: start.sectionTop + start.sectionHeight,
            centerX: start.sectionLeft + start.sectionWidth / 2,
            centerY: start.sectionTop + start.sectionHeight / 2
        };

        const siblings = [...container.querySelectorAll(".ez-freeform-block-overlay")]
            .filter(item => item !== block &&
                item.dataset.sectionId === block.dataset.sectionId &&
                item.dataset.sectionLeft === block.dataset.sectionLeft &&
                item.dataset.sectionTop === block.dataset.sectionTop &&
                item.dataset.sectionWidth === block.dataset.sectionWidth &&
                item.dataset.sectionHeight === block.dataset.sectionHeight)
            .map(item => {
                const left = parseFloat(item.style.left || "0");
                const top = parseFloat(item.style.top || "0");
                const width = parseFloat(item.style.width || "0");
                const height = parseFloat(item.style.height || "0");
                return {
                    left,
                    top,
                    width,
                    height,
                    right: left + width,
                    bottom: top + height,
                    centerX: left + width / 2,
                    centerY: top + height / 2
                };
            });

        function clamp(value, min, max) {
            return Math.min(Math.max(value, min), max);
        }

        function move(e) {
            const dx = (e.clientX - start.x) / scale;
            const dy = (e.clientY - start.y) / scale;

            if (mode === "resize") {
                const availableWidth = Math.max(start.sectionWidth / 12, start.sectionWidth - (start.left - start.sectionLeft));
                const availableHeight = Math.max(48, start.sectionHeight - (start.top - start.sectionTop));
                let width = clamp(start.width + dx, start.sectionWidth / 12, availableWidth);
                let height = clamp(start.height + dy, 48, availableHeight);

                const rightCandidates = [
                    { value: bounds.right, guide: bounds.right },
                    { value: bounds.centerX, guide: bounds.centerX },
                    ...siblings.flatMap(item => [
                        { value: item.left, guide: item.left },
                        { value: item.right, guide: item.right },
                        { value: item.centerX, guide: item.centerX }
                    ])
                ];
                const bottomCandidates = [
                    { value: bounds.bottom, guide: bounds.bottom },
                    { value: bounds.centerY, guide: bounds.centerY },
                    ...siblings.flatMap(item => [
                        { value: item.top, guide: item.top },
                        { value: item.bottom, guide: item.bottom },
                        { value: item.centerY, guide: item.centerY }
                    ])
                ];
                const snapX = nearestSnap(start.left + width, rightCandidates);
                const snapY = nearestSnap(start.top + height, bottomCandidates);

                if (snapX) width = clamp(snapX.value - start.left, start.sectionWidth / 12, availableWidth);
                if (snapY) height = clamp(snapY.value - start.top, 48, availableHeight);
                snapX ? showGuide("x", snapX.guide, bounds) : hideGuide("x");
                snapY ? showGuide("y", snapY.guide, bounds) : hideGuide("y");

                block.style.width = `${width}px`;
                block.style.height = `${height}px`;
                return;
            }

            const width = parseFloat(block.style.width || `${start.width}`);
            const height = parseFloat(block.style.height || `${start.height}`);
            const maxLeft = Math.max(start.sectionLeft, start.sectionLeft + start.sectionWidth - width);
            const maxTop = Math.max(start.sectionTop, start.sectionTop + start.sectionHeight - height);
            let left = clamp(start.left + dx, start.sectionLeft, maxLeft);
            let top = clamp(start.top + dy, start.sectionTop, maxTop);
            const xCandidates = [
                { value: bounds.left, guide: bounds.left },
                { value: bounds.right - width, guide: bounds.right },
                { value: bounds.centerX - width / 2, guide: bounds.centerX },
                ...siblings.flatMap(item => [
                    { value: item.left, guide: item.left },
                    { value: item.right, guide: item.right },
                    { value: item.left - width, guide: item.left },
                    { value: item.right - width, guide: item.right },
                    { value: item.centerX - width / 2, guide: item.centerX }
                ])
            ];
            const yCandidates = [
                { value: bounds.top, guide: bounds.top },
                { value: bounds.bottom - height, guide: bounds.bottom },
                { value: bounds.centerY - height / 2, guide: bounds.centerY },
                ...siblings.flatMap(item => [
                    { value: item.top, guide: item.top },
                    { value: item.bottom, guide: item.bottom },
                    { value: item.top - height, guide: item.top },
                    { value: item.bottom - height, guide: item.bottom },
                    { value: item.centerY - height / 2, guide: item.centerY }
                ])
            ];
            const snapX = nearestSnap(left, xCandidates);
            const snapY = nearestSnap(top, yCandidates);

            if (snapX) left = clamp(snapX.value, start.sectionLeft, maxLeft);
            if (snapY) top = clamp(snapY.value, start.sectionTop, maxTop);
            snapX ? showGuide("x", snapX.guide, bounds) : hideGuide("x");
            snapY ? showGuide("y", snapY.guide, bounds) : hideGuide("y");

            block.style.left = `${left}px`;
            block.style.top = `${top}px`;
        }

        function finish() {
            window.removeEventListener("pointermove", move);
            window.removeEventListener("pointerup", finish);
            window.removeEventListener("pointercancel", finish);
            block.classList.remove("ez-freeform-block-overlay--editing");
            document.body.classList.remove("ez-arranging-block");
            hideGuide("x");
            hideGuide("y");

            const left = parseFloat(block.style.left || `${start.left}`);
            const top = parseFloat(block.style.top || `${start.top}`);
            const width = parseFloat(block.style.width || `${start.width}`);
            const height = parseFloat(block.style.height || `${start.height}`);

            const x = clamp(Math.round((left - start.sectionLeft) / start.sectionWidth * 12), 0, 11);
            const y = clamp(Math.round((top - start.sectionTop) / 48), 0, 60);
            const w = clamp(Math.round(width / start.sectionWidth * 12), 1, 12);
            const h = clamp(Math.round(height / 48), 1, 40);
            const leftPercent = clamp((left - start.sectionLeft) / start.sectionWidth * 100, 0, 100);
            const topPx = clamp(top - start.sectionTop, 0, start.sectionHeight - height);
            const widthPercent = clamp(width / start.sectionWidth * 100, 1, 100);
            const heightPx = clamp(height, 24, start.sectionHeight);

            if (Math.abs(left - start.left) < 0.5 &&
                Math.abs(top - start.top) < 0.5 &&
                Math.abs(width - start.width) < 0.5 &&
                Math.abs(height - start.height) < 0.5) {
                block.style.left = `${start.left}px`;
                block.style.top = `${start.top}px`;
                block.style.width = `${start.width}px`;
                block.style.height = `${start.height}px`;
                return;
            }

            container.__ezFreeformDotNet.invokeMethodAsync(
                "SaveFreeformBlockLayout",
                block.dataset.sectionId,
                block.dataset.blockId,
                x,
                y,
                w,
                h,
                leftPercent,
                topPx,
                widthPercent,
                heightPx)
                .then(saved => {
                    if (saved !== false) return;

                    block.style.left = `${start.left}px`;
                    block.style.top = `${start.top}px`;
                    block.style.width = `${start.width}px`;
                    block.style.height = `${start.height}px`;
                })
                .catch(() => {
                    block.style.left = `${start.left}px`;
                    block.style.top = `${start.top}px`;
                    block.style.width = `${start.width}px`;
                    block.style.height = `${start.height}px`;
                });
        }

        window.addEventListener("pointermove", move);
        window.addEventListener("pointerup", finish);
        window.addEventListener("pointercancel", finish);
    });
};

window.scrollAdminPageTabs = (container, direction) => {
    if (!container) return;
    const amount = Math.max(240, container.clientWidth * 0.7);
    container.scrollBy({
        left: (direction < 0 ? -1 : 1) * amount,
        behavior: "smooth"
    });
};
