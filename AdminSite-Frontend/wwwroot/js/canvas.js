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
        const formatElement = element.closest?.("h2,h3,p") || element;
        const blockFormat = ["H2", "H3"].includes(formatElement.tagName) ? formatElement.tagName.toLowerCase() : "p";
        entry.dotnet.invokeMethodAsync("OnFormatChanged", fontSize, color, blockFormat).catch(() => {});
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
            const onPaste = (event) => {
                event.preventDefault();
                const text = event.clipboardData?.getData("text/plain") || "";
                restoreSelection(editors[id]);
                document.execCommand("insertText", false, text);
                onInput();
            };
            element.addEventListener("input", onInput);
            element.addEventListener("mouseup", onSelectionChange);
            element.addEventListener("keyup", onSelectionChange);
            element.addEventListener("focus", onSelectionChange);
            element.addEventListener("paste", onPaste);
            editors[id] = { el: element, dotnet, onInput, onSelectionChange, onPaste, range: null };
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
        setBlockFormat(id, format) {
            const entry = editors[id];
            if (!entry) return;
            const safeFormat = ["p", "h2", "h3"].includes(String(format || "").toLowerCase())
                ? String(format).toLowerCase()
                : "p";
            restoreSelection(entry);
            document.execCommand("formatBlock", false, safeFormat);
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
            entry.el.removeEventListener("paste", entry.onPaste);
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
        const rect = root.getBoundingClientRect();
        const availableHeight = Math.max(360, window.innerHeight - Math.max(rect.top, 0) - 24);
        const measuredHeight = root.clientHeight || availableHeight;
        const viewportHeight = Math.max(360, Math.min(measuredHeight, availableHeight));
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

window.initBlockListSortable = (key, container, dotnet) => {
    if (!key || !container || !dotnet || typeof Sortable === "undefined") return;

    window.__ezAdminSortables = window.__ezAdminSortables || {};
    if (window.__ezAdminSortables[key]) {
        window.__ezAdminSortables[key].destroy();
        window.__ezAdminSortables[key] = null;
    }

    let originalOrder = [];
    const sortable = Sortable.create(container, {
        handle: ".block-card-drag",
        draggable: ".block-sort-item",
        dataIdAttr: "data-block-sort-id",
        animation: 150,
        onStart: () => {
            originalOrder = sortable.toArray();
        },
        onMove: event => event.dragged?.dataset.blockSortLocked !== "true",
        onEnd: event => {
            if (event.oldIndex === event.newIndex) return;
            const moved = event.item;
            const parentId = moved.dataset.blockParent || "";
            const zone = moved.dataset.blockZone || "default";
            const slotId = moved.dataset.blockSlot || "";
            const peerIds = [...container.children]
                .filter(item =>
                    item.dataset.blockParent === parentId &&
                    (item.dataset.blockZone || "default") === zone &&
                    (item.dataset.blockSlot || "") === slotId)
                .map(item => item.dataset.blockSortId)
                .filter(Boolean);

            dotnet.invokeMethodAsync("OnBlocksReordered", parentId, zone, slotId, peerIds)
                .then(saved => {
                    if (saved !== false) return;
                    sortable.sort(originalOrder);
                })
                .catch(() => sortable.sort(originalOrder));
        }
    });

    window.__ezAdminSortables[key] = sortable;
};

window.destroyBlockSelectorSortables = root => {
    if (!root || !Array.isArray(root.__blockSelectorSortables)) return;
    root.__blockSelectorSortables.forEach(sortable => sortable?.destroy());
    root.__blockSelectorSortables = [];
};

window.initBlockSelectorSortables = (root, dotnet) => {
    if (!root || !dotnet || typeof Sortable === "undefined") return;
    window.destroyBlockSelectorSortables(root);

    const sortables = [];
    root.querySelectorAll(".ez-block-selector__section").forEach(section => {
        let originalChildren = [];
        let originalPeerIds = [];
        let activePeerKey = "";

        const sortable = Sortable.create(section, {
            handle: ".ez-block-selector__drag",
            draggable: '.ez-block-selector__row[data-can-reorder="true"]',
            dataIdAttr: "data-block-selector-id",
            animation: 150,
            chosenClass: "sortable-chosen",
            ghostClass: "sortable-ghost",
            dragClass: "sortable-drag",
            onStart: event => {
                originalChildren = [...section.children];
                activePeerKey = event.item?.dataset.blockSelectorPeer || "";
                originalPeerIds = [...section.querySelectorAll(".ez-block-selector__row")]
                    .filter(item => item.dataset.blockSelectorPeer === activePeerKey)
                    .map(item => item.dataset.blockSelectorId)
                    .filter(Boolean);
            },
            onMove: event => {
                const draggedPeer = event.dragged?.dataset.blockSelectorPeer || "";
                const relatedPeer = event.related?.dataset.blockSelectorPeer || "";
                return !!draggedPeer && draggedPeer === relatedPeer;
            },
            onEnd: event => {
                const peerKey = event.item?.dataset.blockSelectorPeer || activePeerKey;
                const sectionId = event.item?.dataset.blockSelectorSectionId || "";
                const frontToBackIds = [...section.querySelectorAll(".ez-block-selector__row")]
                    .filter(item => item.dataset.blockSelectorPeer === peerKey)
                    .map(item => item.dataset.blockSelectorId)
                    .filter(Boolean);
                const changed = frontToBackIds.length === originalPeerIds.length &&
                    frontToBackIds.some((id, index) => id !== originalPeerIds[index]);

                if (!changed || !sectionId || !peerKey) {
                    originalChildren.forEach(child => section.appendChild(child));
                    return;
                }

                dotnet.invokeMethodAsync("OnBlockSelectorReordered", sectionId, peerKey, frontToBackIds)
                    .then(saved => {
                        if (saved === true) return;
                        originalChildren.forEach(child => section.appendChild(child));
                    })
                    .catch(() => originalChildren.forEach(child => section.appendChild(child)));
            }
        });
        sortables.push(sortable);
    });

    root.__blockSelectorSortables = sortables;
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
    if (window.__ezCanvasAuthoringKeyHandler) {
        window.removeEventListener("keydown", window.__ezCanvasAuthoringKeyHandler);
        window.__ezCanvasAuthoringKeyHandler = null;
    }
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

window.replayPreviewBlockAnimations = function (blockIds) {
    const iframe = document.getElementById("ez-preview-iframe");
    if (!iframe || !iframe.contentWindow) return;

    const ids = Array.isArray(blockIds)
        ? blockIds
        : (blockIds ? [blockIds] : []);
    if (!ids.length) return;

    const api = iframe.contentWindow.scBlockAnimations;
    if (api && typeof api.replayBlock === "function") {
        ids.forEach(id => api.replayBlock(id));
    }
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

window.patchPreviewBlockLayout = function (blockId, x, y, w, h, leftPercent, topPx, widthPercent, heightPx, zIndex, requestPositions = true) {
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
    block.style.height = `${exactHeight}px`;
    block.style.minHeight = "0px";
    block.style.overflow = "hidden";

    block.querySelectorAll([
        ".sc-block-motion",
        ".sc-block-rotation",
        ".sc-block-visual",
        ".sc-block",
        ".sc-design-block",
        ".sc-block-starter",
        ".sc-card-block",
        ".sc-metric-block",
        ".sc-bullet-list-block",
        ".sc-step-block",
        ".sc-icon-block",
        ".sc-container-block"
    ].join(",")).forEach(element => {
        element.style.height = "100%";
        element.style.minHeight = "0px";
        element.style.boxSizing = "border-box";
    });

    block.querySelectorAll([
        ".sc-block-starter__tile",
        ".sc-block-starter__form",
        ".sc-block-starter__step",
        ".sc-block-starter__media",
        ".sc-block-starter__map",
        ".sc-block-starter__container"
    ].join(",")).forEach(element => {
        element.style.minHeight = "0px";
    });

    if ((block.getAttribute("data-block-type") || "").toLowerCase() === "container") {
        const containerBlock = block.querySelector(".sc-container-block");
        if (containerBlock) {
            containerBlock.style.height = "100%";
            Array.from(containerBlock.children).forEach(child => {
                if (!child.classList || !child.classList.contains("sc-section-blocks")) return;
                child.style.minHeight = "0px";
                child.style.removeProperty("height");
                if (child.classList.contains("sc-section-blocks--freeform")) {
                    child.style.setProperty("--sc-freeform-min-height", "0px");
                    child.style.setProperty("--sc-mobile-freeform-min-height", "0px");
                }
            });
        }

        const starter = block.querySelector(".sc-block-starter--container");
        if (starter) {
            starter.style.height = "100%";
            starter.style.minHeight = "0px";
            starter.style.padding = "0px";
        }

        const starterBoundary = block.querySelector(".sc-block-starter__container");
        if (starterBoundary) {
            starterBoundary.style.height = "100%";
            starterBoundary.style.minHeight = "0px";
        }
    }
    if (zIndex !== null && zIndex !== undefined && Number.isFinite(Number(zIndex))) {
        block.style.zIndex = `${Math.min(Math.max(Number.parseInt(zIndex, 10), 0), 1000)}`;
    }
    if (requestPositions) window.__ezCanvasRequestPositions?.();
};

window.patchPreviewBlockVisual = function (blockId, appearance, animation, layout, requestPositions = true) {
    const iframe = document.getElementById("ez-preview-iframe");
    if (!iframe || !iframe.contentDocument || !blockId) return;

    const frame = [...iframe.contentDocument.querySelectorAll("[data-block-id]")]
        .find(item => item.getAttribute("data-block-id") === blockId);
    if (!frame) return;

    const visual = frame.querySelector(".sc-block-visual");
    const motion = frame.querySelector(".sc-block-motion");
    const overlay = [...document.querySelectorAll(".ez-freeform-block-overlay[data-block-id]")]
        .find(item => item.getAttribute("data-block-id") === blockId);
    const replaceClass = (element, prefix, value) => {
        if (!element) return;
        [...element.classList].filter(name => name.startsWith(prefix)).forEach(name => element.classList.remove(name));
        if (value) element.classList.add(`${prefix}${value}`);
    };
    const setOrRemove = (element, property, value) => {
        if (!element) return;
        if (value === null || value === undefined || value === "") element.style.removeProperty(property);
        else element.style.setProperty(property, String(value));
    };

    appearance = appearance || {};
    animation = animation || {};
    layout = layout || {};
    const shape = appearance.shape || "rectangle";
    const aspectRatio = appearance.aspectRatio || "auto";
    const backgroundMode = appearance.backgroundMode || "none";
    const borderRadius = appearance.borderRadius || "none";

    replaceClass(frame, "sc-block-frame--shape-", shape);
    replaceClass(frame, "sc-block-frame--aspect-", aspectRatio);
    frame.dataset.blockShape = shape;
    if (visual) {
        replaceClass(visual, "sc-block-visual--shape-", shape);
        replaceClass(visual, "sc-block-visual--background-", backgroundMode);
        replaceClass(visual, "sc-block-visual--radius-", borderRadius);
        replaceClass(visual, "sc-block-visual--shadow-", appearance.shadow || "none");
        replaceClass(visual, "sc-block-visual--text-", appearance.textAlign || "inherit");
        replaceClass(visual, "sc-block-visual--pad-", appearance.padding || "none");
        setOrRemove(visual, "background-color", backgroundMode === "color" ? appearance.backgroundColor : null);
        setOrRemove(visual, "color", appearance.textColor);
        setOrRemove(visual, "opacity", Number(appearance.opacity) < 1 ? appearance.opacity : null);
        const borderWidth = Math.max(0, Math.min(20, Number.parseInt(appearance.borderWidth, 10) || 0));
        const borderStyle = appearance.borderStyle || "solid";
        if (borderWidth > 0 && borderStyle !== "none") {
            setOrRemove(visual, "border-width", `${borderWidth}px`);
            setOrRemove(visual, "border-style", borderStyle);
            setOrRemove(visual, "border-color", appearance.borderColor || "currentColor");
        } else {
            setOrRemove(visual, "border-width", null);
            setOrRemove(visual, "border-style", null);
            setOrRemove(visual, "border-color", null);
        }
    }
    const rotation = Number.isFinite(Number(appearance.rotationDeg)) ? Number(appearance.rotationDeg) : 0;
    const rotationLayer = frame.querySelector(".sc-block-rotation");
    if (rotationLayer) rotationLayer.style.setProperty("--sc-block-rotation", `${rotation}deg`);
    if (motion) {
        replaceClass(motion, "sc-block-motion--", animation.effect || "none");
        replaceClass(motion, "sc-block-motion--continuous-", animation.continuousEffect || "none");
        motion.dataset.scAnimation = animation.effect || "none";
        motion.dataset.scAnimationTrigger = animation.trigger || "enter-viewport";
        motion.dataset.scAnimationOnce = String(animation.playOnce !== false);
        motion.dataset.scAnimationReduced = "true";
        motion.dataset.scContinuousMotion = animation.continuousEffect || "none";
        setOrRemove(motion, "--sc-motion-duration", `${Math.max(0, Number.parseInt(animation.durationMs, 10) || 0)}ms`);
        setOrRemove(motion, "--sc-motion-delay", `${Math.max(0, Number.parseInt(animation.delayMs, 10) || 0)}ms`);
        setOrRemove(motion, "--sc-motion-easing", animation.easing || "ease-out");
    }
    if (overlay) {
        replaceClass(overlay, "ez-freeform-block-overlay--shape-", shape);
        replaceClass(overlay, "ez-freeform-block-overlay--radius-", borderRadius);
        overlay.dataset.blockShape = shape;
        overlay.dataset.blockRadius = borderRadius;
        overlay.dataset.blockRotation = String(rotation);
        overlay.style.setProperty("--ez-block-rotation", `${rotation}deg`);
    }

    window.patchPreviewBlockLayout(
        blockId,
        layout.x,
        layout.y,
        layout.w,
        layout.h,
        layout.leftPercent,
        layout.topPx,
        layout.widthPercent,
        layout.heightPx,
        layout.zIndex,
        false);
    if (requestPositions) window.__ezCanvasRequestPositions?.();
};

window.previewBlockShape = function (blockIds, shape) {
    const iframe = document.getElementById("ez-preview-iframe");
    if (!iframe || !iframe.contentDocument) return;
    const ids = new Set(Array.isArray(blockIds) ? blockIds : [blockIds]);
    const safeShape = ["rectangle", "rounded", "pill", "circle", "ellipse"].includes(shape) ? shape : "rectangle";
    const replaceClass = (element, prefix, value) => {
        if (!element) return;
        [...element.classList].filter(name => name.startsWith(prefix)).forEach(name => element.classList.remove(name));
        element.classList.add(`${prefix}${value}`);
    };
    [...iframe.contentDocument.querySelectorAll("[data-block-id]")]
        .filter(frame => ids.has(frame.getAttribute("data-block-id")))
        .forEach(frame => {
            replaceClass(frame, "sc-block-frame--shape-", safeShape);
            if (safeShape === "circle") replaceClass(frame, "sc-block-frame--aspect-", "square");
            frame.dataset.blockShape = safeShape;
            replaceClass(frame.querySelector(".sc-block-visual"), "sc-block-visual--shape-", safeShape);
        });
    [...document.querySelectorAll(".ez-freeform-block-overlay[data-block-id]")]
        .filter(overlay => ids.has(overlay.getAttribute("data-block-id")))
        .forEach(overlay => {
            replaceClass(overlay, "ez-freeform-block-overlay--shape-", safeShape);
            overlay.dataset.blockShape = safeShape;
        });
    window.__ezCanvasRequestPositions?.();
};

window.previewBlockRotation = function (items) {
    const iframe = document.getElementById("ez-preview-iframe");
    if (!iframe || !iframe.contentDocument || !Array.isArray(items)) return;
    const rotations = new Map(items.map(item => [item.id, Number.isFinite(Number(item.rotationDeg)) ? Number(item.rotationDeg) : 0]));
    [...iframe.contentDocument.querySelectorAll("[data-block-id]")].forEach(frame => {
        const id = frame.getAttribute("data-block-id");
        if (!rotations.has(id)) return;
        frame.querySelector(".sc-block-rotation")?.style.setProperty("--sc-block-rotation", `${rotations.get(id)}deg`);
    });
    [...document.querySelectorAll(".ez-freeform-block-overlay[data-block-id]")].forEach(overlay => {
        const id = overlay.getAttribute("data-block-id");
        if (!rotations.has(id)) return;
        overlay.dataset.blockRotation = String(rotations.get(id));
        overlay.style.setProperty("--ez-block-rotation", `${rotations.get(id)}deg`);
    });
};

window.initFreeformBlockEditor = function (container, dotnet, scale) {
    if (!container || !dotnet) return;

    container.__ezFreeformDotNet = dotnet;
    container.__ezFreeformScale = scale || 1;

    if (window.__ezCanvasAuthoringKeyHandler) {
        window.removeEventListener("keydown", window.__ezCanvasAuthoringKeyHandler);
    }
    window.__ezCanvasAuthoringKeyHandler = function (event) {
        const target = event.target;
        const isEditing = target && (target.matches?.("input,textarea,select") || target.isContentEditable);
        if (event.key === "Escape") {
            container.__ezFreeformDotNet?.invokeMethodAsync("ClearArrangeSelection").catch(() => {});
            return;
        }
        if (isEditing || (!event.ctrlKey && !event.metaKey)) return;
        const key = String(event.key || "").toLowerCase();
        if (key !== "z" && key !== "y") return;
        event.preventDefault();
        const undo = key === "z" && !event.shiftKey;
        container.__ezFreeformDotNet?.invokeMethodAsync("RunCanvasHistory", undo).catch(() => {});
    };
    window.addEventListener("keydown", window.__ezCanvasAuthoringKeyHandler);

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
        const constrainToCircle = block.dataset.blockShape === "circle";
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

        const groupBlocks = mode === "drag" && block.classList.contains("ez-freeform-block-overlay--selected")
            ? [...container.querySelectorAll(".ez-freeform-block-overlay--selected")]
                .filter(item =>
                    item.dataset.sectionId === block.dataset.sectionId &&
                    item.dataset.geometryLocked !== "true" &&
                    item.dataset.sectionLeft === block.dataset.sectionLeft &&
                    item.dataset.sectionTop === block.dataset.sectionTop &&
                    item.dataset.sectionWidth === block.dataset.sectionWidth &&
                    item.dataset.sectionHeight === block.dataset.sectionHeight)
            : [block];
        const groupStarts = groupBlocks.map(item => ({
            element: item,
            blockId: item.dataset.blockId,
            sectionId: item.dataset.sectionId,
            left: parseFloat(item.style.left || "0"),
            top: parseFloat(item.style.top || "0"),
            width: parseFloat(item.style.width || "0"),
            height: parseFloat(item.style.height || "0")
        }));

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

        function syncPreviewBlock(blockId, left, top, width, height) {
            if (!blockId) return;
            const leftPercent = clamp((left - start.sectionLeft) / start.sectionWidth * 100, 0, 100);
            const topPx = clamp(top - start.sectionTop, 0, Math.max(0, start.sectionHeight - height));
            const widthPercent = clamp(width / start.sectionWidth * 100, 1, 100);
            const heightPx = clamp(height, 24, start.sectionHeight);
            window.patchPreviewBlockLayout(
                blockId,
                0,
                0,
                1,
                1,
                leftPercent,
                topPx,
                widthPercent,
                heightPx,
                null,
                false);
        }

        function restorePreviewBlocks() {
            groupStarts.forEach(item =>
                syncPreviewBlock(item.blockId, item.left, item.top, item.width, item.height));
        }

        function move(e) {
            const screenDx = (e.clientX - start.x) / scale;
            const screenDy = (e.clientY - start.y) / scale;
            const rotationRad = (Number.parseFloat(block.dataset.blockRotation || "0") || 0) * Math.PI / 180;
            const dx = mode === "resize"
                ? screenDx * Math.cos(rotationRad) + screenDy * Math.sin(rotationRad)
                : screenDx;
            const dy = mode === "resize"
                ? -screenDx * Math.sin(rotationRad) + screenDy * Math.cos(rotationRad)
                : screenDy;

            if (mode === "resize") {
                const availableWidth = Math.max(start.sectionWidth / 12, start.sectionWidth - (start.left - start.sectionLeft));
                const availableHeight = Math.max(48, start.sectionHeight - (start.top - start.sectionTop));
                const maximumCircleSize = Math.min(availableWidth, availableHeight);
                const minimumCircleSize = Math.min(Math.max(48, start.sectionWidth / 12), maximumCircleSize);
                const circleDelta = Math.abs(dx) >= Math.abs(dy) ? dx : dy;
                let width = constrainToCircle
                    ? clamp((start.width + start.height) / 2 + circleDelta, minimumCircleSize, maximumCircleSize)
                    : clamp(start.width + dx, start.sectionWidth / 12, availableWidth);
                let height = constrainToCircle
                    ? width
                    : clamp(start.height + dy, 48, availableHeight);

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

                if (constrainToCircle && (snapX || snapY)) {
                    const snapXSize = snapX ? clamp(snapX.value - start.left, minimumCircleSize, maximumCircleSize) : null;
                    const snapYSize = snapY ? clamp(snapY.value - start.top, minimumCircleSize, maximumCircleSize) : null;
                    const useX = snapXSize !== null && (snapYSize === null || Math.abs(snapXSize - width) <= Math.abs(snapYSize - width));
                    const size = useX ? snapXSize : snapYSize;
                    width = size;
                    height = size;
                    useX && snapX ? showGuide("x", snapX.guide, bounds) : hideGuide("x");
                    !useX && snapY ? showGuide("y", snapY.guide, bounds) : hideGuide("y");
                } else {
                    if (snapX) width = clamp(snapX.value - start.left, start.sectionWidth / 12, availableWidth);
                    if (snapY) height = clamp(snapY.value - start.top, 48, availableHeight);
                    snapX ? showGuide("x", snapX.guide, bounds) : hideGuide("x");
                    snapY ? showGuide("y", snapY.guide, bounds) : hideGuide("y");
                }

                block.style.width = `${width}px`;
                block.style.height = `${height}px`;
                syncPreviewBlock(block.dataset.blockId, start.left, start.top, width, height);
                return;
            }

            if (groupStarts.length > 1) {
                const groupLeft = Math.min(...groupStarts.map(item => item.left));
                const groupTop = Math.min(...groupStarts.map(item => item.top));
                const groupRight = Math.max(...groupStarts.map(item => item.left + item.width));
                const groupBottom = Math.max(...groupStarts.map(item => item.top + item.height));
                const boundedDx = clamp(dx, bounds.left - groupLeft, bounds.right - groupRight);
                const boundedDy = clamp(dy, bounds.top - groupTop, bounds.bottom - groupBottom);
                groupStarts.forEach(item => {
                    const left = item.left + boundedDx;
                    const top = item.top + boundedDy;
                    item.element.style.left = `${left}px`;
                    item.element.style.top = `${top}px`;
                    syncPreviewBlock(item.blockId, left, top, item.width, item.height);
                });
                hideGuide("x");
                hideGuide("y");
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
            syncPreviewBlock(block.dataset.blockId, left, top, width, height);
        }

        function finish() {
            window.removeEventListener("pointermove", move);
            window.removeEventListener("pointerup", finish);
            window.removeEventListener("pointercancel", finish);
            block.classList.remove("ez-freeform-block-overlay--editing");
            document.body.classList.remove("ez-arranging-block");
            hideGuide("x");
            hideGuide("y");

            if (groupStarts.length > 1) {
                const mutations = groupStarts.map(item => {
                    const left = parseFloat(item.element.style.left || `${item.left}`);
                    const top = parseFloat(item.element.style.top || `${item.top}`);
                    return {
                        blockId: item.blockId,
                        sectionId: item.sectionId,
                        x: clamp(Math.round((left - start.sectionLeft) / start.sectionWidth * 12), 0, 11),
                        y: clamp(Math.round((top - start.sectionTop) / 48), 0, 60),
                        w: clamp(Math.round(item.width / start.sectionWidth * 12), 1, 12),
                        h: clamp(Math.round(item.height / 48), 1, 40),
                        leftPercent: clamp((left - start.sectionLeft) / start.sectionWidth * 100, 0, 100),
                        topPx: clamp(top - start.sectionTop, 0, start.sectionHeight - item.height),
                        widthPercent: clamp(item.width / start.sectionWidth * 100, 1, 100),
                        heightPx: clamp(item.height, 24, start.sectionHeight)
                    };
                });
                const changed = groupStarts.some(item =>
                    Math.abs(parseFloat(item.element.style.left || `${item.left}`) - item.left) >= 0.5 ||
                    Math.abs(parseFloat(item.element.style.top || `${item.top}`) - item.top) >= 0.5);
                if (!changed) return;
                container.__ezFreeformDotNet.invokeMethodAsync("SaveFreeformBlockLayouts", mutations)
                    .then(saved => {
                        if (saved !== false) return;
                        groupStarts.forEach(item => {
                            item.element.style.left = `${item.left}px`;
                            item.element.style.top = `${item.top}px`;
                        });
                        restorePreviewBlocks();
                    })
                    .catch(() => {
                        groupStarts.forEach(item => {
                            item.element.style.left = `${item.left}px`;
                            item.element.style.top = `${item.top}px`;
                        });
                        restorePreviewBlocks();
                    });
                return;
            }

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
                    restorePreviewBlocks();
                })
                .catch(() => {
                    block.style.left = `${start.left}px`;
                    block.style.top = `${start.top}px`;
                    block.style.width = `${start.width}px`;
                    block.style.height = `${start.height}px`;
                    restorePreviewBlocks();
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

window.revealAdminPageTab = (container, pageId) => {
    if (!container || !pageId) return;

    const reveal = () => {
        const id = String(pageId);
        const target = Array.from(container.querySelectorAll("[data-root-page-id]"))
            .find(tab => tab.getAttribute("data-root-page-id") === id);
        if (!target) return;

        const containerRect = container.getBoundingClientRect();
        const targetRect = target.getBoundingClientRect();
        const edgePadding = 8;
        let nextScrollLeft = container.scrollLeft;

        if (targetRect.left < containerRect.left + edgePadding) {
            nextScrollLeft -= (containerRect.left + edgePadding) - targetRect.left;
        } else if (targetRect.right > containerRect.right - edgePadding) {
            nextScrollLeft += targetRect.right - (containerRect.right - edgePadding);
        } else {
            return;
        }

        const maxScrollLeft = Math.max(0, container.scrollWidth - container.clientWidth);
        nextScrollLeft = Math.max(0, Math.min(maxScrollLeft, nextScrollLeft));
        if (Math.abs(nextScrollLeft - container.scrollLeft) < 1) return;

        container.scrollTo({
            left: nextScrollLeft,
            behavior: "smooth"
        });
    };

    requestAnimationFrame(() => requestAnimationFrame(reveal));
};
