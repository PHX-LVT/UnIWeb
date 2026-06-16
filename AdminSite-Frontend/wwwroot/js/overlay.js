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

            const freeformContainers = el.querySelectorAll('.sc-section-blocks--freeform');
            freeformContainers.forEach(function (container) {
                const blocks = container.querySelectorAll('[data-block-id]');
                blocks.forEach(function (block) {
                    const blockRect = block.getBoundingClientRect();
                    blockPositions.push({
                        id: block.getAttribute('data-block-id'),
                        sectionId: block.getAttribute('data-block-section-id') || sectionId,
                        top: blockRect.top + window.scrollY,
                        left: blockRect.left + window.scrollX,
                        width: blockRect.width,
                        height: blockRect.height,
                        sectionTop: rect.top + window.scrollY,
                        sectionLeft: rect.left + window.scrollX,
                        sectionWidth: rect.width,
                        sectionHeight: rect.height
                    });
                });
            });
        });

        if (window.parent === window) return;

        window.parent.postMessage({
            type: 'ez-section-positions',
            positions: positions,
            blockPositions: blockPositions,
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

