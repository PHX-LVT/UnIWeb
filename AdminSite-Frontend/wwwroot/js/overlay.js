(function () {
    if (!window.location.pathname.toLowerCase().startsWith("/preview/")) return;

    let pending = false;

    function queueReport() {
        if (pending) return;
        pending = true;

        requestAnimationFrame(function () {
            pending = false;
            reportPositions();
        });
    }

    function reportPositions() {
        const sections = document.querySelectorAll('[data-section-id]');
        const positions = [];
        const blockPositions = [];
        const zonePositions = [];

        sections.forEach(function (el) {
            const rect = el.getBoundingClientRect();
            const sectionId = el.getAttribute('data-section-id');
            positions.push({
                id: sectionId,
                top: rect.top + window.scrollY,
                left: rect.left + window.scrollX,
                width: rect.width,
                height: rect.height
            });

            const zones = el.querySelectorAll('.sc-block-zone[data-block-zone]');
            zones.forEach(function (zone) {
                const zoneRect = zone.getBoundingClientRect();
                zonePositions.push({
                    id: zone.getAttribute('data-block-zone') || 'default',
                    sectionId: sectionId,
                    kind: zone.getAttribute('data-block-zone-kind') || 'freeform',
                    top: zoneRect.top + window.scrollY,
                    left: zoneRect.left + window.scrollX,
                    width: zoneRect.width,
                    height: zoneRect.height
                });
            });

            const measuredBlockIds = new Set();
            const blockContainers = el.querySelectorAll('.sc-block-zone[data-block-zone]');
            blockContainers.forEach(function (container) {
                const containerRect = container.getBoundingClientRect();
                const blocks = container.querySelectorAll(':scope > [data-block-id]');
                blocks.forEach(function (block) {
                    const blockId = block.getAttribute('data-block-id');
                    if (!blockId || measuredBlockIds.has(blockId)) return;
                    measuredBlockIds.add(blockId);

                    const blockRect = block.getBoundingClientRect();
                    blockPositions.push({
                        id: blockId,
                        sectionId: block.getAttribute('data-block-section-id') || sectionId,
                        positionMode: block.getAttribute('data-block-position-mode') || 'flow',
                        top: blockRect.top + window.scrollY,
                        left: blockRect.left + window.scrollX,
                        width: blockRect.width,
                        height: blockRect.height,
                        sectionTop: containerRect.top + window.scrollY,
                        sectionLeft: containerRect.left + window.scrollX,
                        sectionWidth: containerRect.width,
                        sectionHeight: containerRect.height
                    });
                });
            });
        });

        if (window.parent === window) return;

        window.parent.postMessage({
            type: 'ez-section-positions',
            positions: positions,
            blockPositions: blockPositions,
            zonePositions: zonePositions,
            documentHeight: Math.max(
                document.body.scrollHeight,
                document.documentElement.scrollHeight)
        }, window.location.origin);
    }

    window.reportSectionPositions = queueReport;

    window.addEventListener('message', function (e) {
        if (e.origin === window.location.origin && e.data && e.data.type === 'ez-request-section-positions') {
            queueReport();
        }
    });

    if (document.readyState === 'complete') {
        queueReport();
    } else {
        window.addEventListener('load', queueReport);
    }

    if (typeof ResizeObserver !== 'undefined') {
        new ResizeObserver(queueReport).observe(document.body);
    }
})();

