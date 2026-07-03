(function () {
    "use strict";

    const registered = new WeakSet();
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
    const observer = "IntersectionObserver" in window
        ? new IntersectionObserver(onIntersection, { rootMargin: "0px 0px -8%", threshold: 0.08 })
        : null;

    function activate(element) {
        element.dataset.scAnimationState = "active";
    }

    function deactivate(element) {
        element.dataset.scAnimationState = "waiting";
    }

    function themeMotionDisabled() {
        const value = getComputedStyle(document.documentElement)
            .getPropertyValue("--theme-motion-duration")
            .trim()
            .toLowerCase();
        return value === "0s" || value === "0ms";
    }

    function onIntersection(entries) {
        entries.forEach(entry => {
            const element = entry.target;
            if (entry.isIntersecting) {
                activate(element);
                if (element.dataset.scAnimationOnce !== "false") observer.unobserve(element);
            } else if (element.dataset.scAnimationOnce === "false") {
                deactivate(element);
            }
        });
    }

    function register(element) {
        if (registered.has(element)) return;
        registered.add(element);
        const effect = element.dataset.scAnimation || "none";
        if (effect === "none" || themeMotionDisabled() || reducedMotion.matches || !observer) {
            activate(element);
            return;
        }

        deactivate(element);
        if (element.dataset.scAnimationTrigger === "load") {
            requestAnimationFrame(() => requestAnimationFrame(() => activate(element)));
        } else {
            observer.observe(element);
        }
    }

    function scan(root) {
        const scope = root && root.querySelectorAll ? root : document;
        if (scope.matches && scope.matches("[data-sc-animation]")) register(scope);
        scope.querySelectorAll("[data-sc-animation]").forEach(register);
    }

    function resolveReducedMotion() {
        document.querySelectorAll("[data-sc-animation]").forEach(element => {
            if (reducedMotion.matches) activate(element);
        });
    }

    function applyThemeMotionPolicy() {
        const disabled = themeMotionDisabled();
        document.documentElement.classList.toggle("sc-theme-motion-off", disabled);
        if (disabled) document.querySelectorAll("[data-sc-animation]").forEach(activate);
    }

    const mutationObserver = new MutationObserver(records => {
        records.forEach(record => {
            record.addedNodes.forEach(node => {
                if (node.nodeType === Node.ELEMENT_NODE) scan(node);
            });
            record.removedNodes.forEach(node => {
                if (!observer || node.nodeType !== Node.ELEMENT_NODE) return;
                if (node.matches && node.matches("[data-sc-animation]")) observer.unobserve(node);
                node.querySelectorAll("[data-sc-animation]").forEach(element => observer.unobserve(element));
            });
        });
    });

    function initialize() {
        document.documentElement.classList.add("sc-motion-ready");
        applyThemeMotionPolicy();
        scan(document);
        mutationObserver.observe(document.body, { childList: true, subtree: true });
        reducedMotion.addEventListener("change", resolveReducedMotion);
        document.addEventListener("visibilitychange", () => {
            document.documentElement.classList.toggle("sc-motion-paused", document.hidden);
        });
        const themeStyle = document.getElementById("theme-vars");
        if (themeStyle) {
            new MutationObserver(applyThemeMotionPolicy)
                .observe(themeStyle, { childList: true, characterData: true, subtree: true });
        }
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", initialize, { once: true });
    else initialize();

    window.scBlockAnimations = {
        refresh: () => scan(document),
        replayBlock: blockId => {
            const frame = Array.from(document.querySelectorAll(".sc-block-frame[data-block-id]")).find(
                element => element.dataset.blockId === blockId);
            const element = frame && frame.querySelector(":scope > [data-sc-animation]");
            if (!element) return;
            deactivate(element);
            requestAnimationFrame(() => activate(element));
        },
        replayAll: () => document.querySelectorAll("[data-sc-animation]").forEach(element => {
            deactivate(element);
            requestAnimationFrame(() => activate(element));
        }),
        setReducedPreview: enabled => document.documentElement.classList.toggle("sc-motion-preview-reduced", !!enabled),
        replay: element => {
            if (!element) return;
            deactivate(element);
            requestAnimationFrame(() => activate(element));
        }
    };
})();
