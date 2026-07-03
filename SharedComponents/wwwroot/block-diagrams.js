(function () {
    "use strict";

    const observed = new WeakSet();
    const resizeObserver = "ResizeObserver" in window
        ? new ResizeObserver(entries => entries.forEach(entry => update(entry.target)))
        : null;

    function number(value, fallback) {
        const parsed = Number.parseFloat(value);
        return Number.isFinite(parsed) ? parsed : fallback;
    }

    function pointFor(container, anchor) {
        const bounds = container.getBoundingClientRect();
        if (!anchor || anchor === "center") {
            return { x: bounds.width / 2, y: bounds.height / 2 };
        }

        const target = Array.from(container.children).find(child =>
            child.classList &&
            child.classList.contains("sc-block-frame") &&
            child.dataset.blockStableId === anchor);
        if (!target) return null;

        const targetBounds = target.getBoundingClientRect();
        return {
            x: targetBounds.left - bounds.left + targetBounds.width / 2,
            y: targetBounds.top - bounds.top + targetBounds.height / 2
        };
    }

    function arcPath(center, radius, startDegrees, endDegrees) {
        let sweep = endDegrees - startDegrees;
        if (Math.abs(sweep) >= 359.999) {
            const start = polar(center, radius, startDegrees);
            const opposite = polar(center, radius, startDegrees + (sweep < 0 ? -180 : 180));
            const flag = sweep < 0 ? 0 : 1;
            return `M ${start.x} ${start.y} A ${radius} ${radius} 0 1 ${flag} ${opposite.x} ${opposite.y} A ${radius} ${radius} 0 1 ${flag} ${start.x} ${start.y}`;
        }

        if (Math.abs(sweep) < 0.001) sweep = 0.001;
        const start = polar(center, radius, startDegrees);
        const end = polar(center, radius, startDegrees + sweep);
        return `M ${start.x} ${start.y} A ${radius} ${radius} 0 ${Math.abs(sweep) > 180 ? 1 : 0} ${sweep < 0 ? 0 : 1} ${end.x} ${end.y}`;
    }

    function polar(center, radius, degrees) {
        const angle = degrees * Math.PI / 180;
        return {
            x: center.x + radius * Math.cos(angle),
            y: center.y + radius * Math.sin(angle)
        };
    }

    function connectorPath(from, to, routing) {
        if (routing !== "curve") return `M ${from.x} ${from.y} L ${to.x} ${to.y}`;
        const dx = to.x - from.x;
        const dy = to.y - from.y;
        const length = Math.max(Math.hypot(dx, dy), 1);
        const bend = Math.min(length * 0.18, 56);
        const middleX = (from.x + to.x) / 2 - dy / length * bend;
        const middleY = (from.y + to.y) / 2 + dx / length * bend;
        return `M ${from.x} ${from.y} Q ${middleX} ${middleY} ${to.x} ${to.y}`;
    }

    function update(container) {
        const svg = container.querySelector(":scope > .sc-container-diagram");
        if (!svg) return;
        const bounds = container.getBoundingClientRect();
        if (bounds.width <= 0 || bounds.height <= 0) return;

        svg.setAttribute("viewBox", `0 0 ${bounds.width} ${bounds.height}`);
        svg.querySelectorAll("[data-sc-diagram-role]").forEach(path => {
            const role = path.dataset.scDiagramRole;
            const kind = path.dataset.scDiagramKind || "straight";
            const from = pointFor(container, path.dataset.scFromAnchor);
            const to = pointFor(container, path.dataset.scToAnchor);
            let data = "";

            if (role === "decoration" && (kind === "ring" || kind === "orbit" || kind === "arc")) {
                const center = from || { x: bounds.width / 2, y: bounds.height / 2 };
                const radius = Math.min(bounds.width, bounds.height) * number(path.dataset.scRadius, 35) / 100;
                const start = kind === "ring" || kind === "orbit" ? 0 : number(path.dataset.scStartAngle, 0);
                const end = kind === "ring" || kind === "orbit" ? 360 : number(path.dataset.scEndAngle, 360);
                data = arcPath(center, Math.max(radius, 1), start, end);
            } else if (role === "decoration" && kind === "divider") {
                const y = (from || { y: bounds.height / 2 }).y;
                data = `M ${bounds.width * 0.08} ${y} L ${bounds.width * 0.92} ${y}`;
            } else if (from && to) {
                data = connectorPath(from, to, role === "connector" ? kind : "straight");
            }

            path.setAttribute("d", data);
            path.style.display = data ? "" : "none";
        });
    }

    function scan(root) {
        const scope = root && root.querySelectorAll ? root : document;
        const diagrams = [];
        if (scope.matches && scope.matches(".sc-container-diagram")) diagrams.push(scope);
        scope.querySelectorAll(".sc-container-diagram").forEach(svg => diagrams.push(svg));
        diagrams.forEach(svg => {
            const container = svg.parentElement;
            if (!container) return;
            update(container);
            if (resizeObserver && !observed.has(container)) {
                observed.add(container);
                resizeObserver.observe(container);
            }
        });
    }

    const mutationObserver = new MutationObserver(records => {
        records.forEach(record => {
            record.addedNodes.forEach(node => {
                if (node.nodeType === Node.ELEMENT_NODE) scan(node);
            });
            record.removedNodes.forEach(node => {
                if (!resizeObserver || node.nodeType !== Node.ELEMENT_NODE) return;
                const containers = [];
                if (node.matches && node.matches(".sc-section-blocks")) containers.push(node);
                node.querySelectorAll(".sc-section-blocks").forEach(container => containers.push(container));
                containers.forEach(container => {
                    resizeObserver.unobserve(container);
                    observed.delete(container);
                });
            });
        });
    });

    function initialize() {
        scan(document);
        mutationObserver.observe(document.body, { childList: true, subtree: true });
        window.addEventListener("resize", () => document.querySelectorAll(".sc-container-diagram").forEach(svg => update(svg.parentElement)));
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", initialize, { once: true });
    else initialize();

    window.scBlockDiagrams = { refresh: () => scan(document) };
})();
