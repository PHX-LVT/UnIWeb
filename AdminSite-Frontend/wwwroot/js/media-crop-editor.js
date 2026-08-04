(function () {
    "use strict";

    const instances = new Map();

    function clamp(value, min, max) {
        const number = Number(value);
        return Number.isFinite(number) ? Math.min(max, Math.max(min, number)) : min;
    }

    function dimensions(instance) {
        return {
            width: instance.image ? instance.image.naturalWidth || 0 : 0,
            height: instance.image ? instance.image.naturalHeight || 0 : 0
        };
    }

    function normalizeState(state) {
        state.x = clamp(state.x, 0, 100);
        state.y = clamp(state.y, 0, 100);
        state.maxZoom = clamp(state.maxZoom, 1, 20);
        state.zoom = clamp(state.zoom, 1, state.maxZoom);
        state.aspect = clamp(state.aspect, .2, 8);
        state.fit = state.fit === "contain" ? "contain" : "cover";
        return state;
    }

    function cropMetrics(instance, zoom) {
        const size = dimensions(instance);
        if (!size.width || !size.height) {
            return { left: 0, top: 0, width: 0, height: 0, availableX: 0, availableY: 0 };
        }

        const aspect = clamp(instance.state.aspect, .2, 8);
        const baseWidth = Math.min(size.width, size.height * aspect);
        const baseHeight = baseWidth / aspect;
        const cropWidth = baseWidth / clamp(zoom, 1, instance.state.maxZoom);
        const cropHeight = baseHeight / clamp(zoom, 1, instance.state.maxZoom);
        const availableX = Math.max(0, size.width - cropWidth);
        const availableY = Math.max(0, size.height - cropHeight);

        return {
            left: availableX * instance.state.x / 100,
            top: availableY * instance.state.y / 100,
            width: cropWidth,
            height: cropHeight,
            availableX: availableX,
            availableY: availableY
        };
    }

    function layoutMetrics(instance) {
        const size = dimensions(instance);
        const canvasRect = instance.element.getBoundingClientRect();
        if (!size.width || !size.height || !canvasRect.width || !canvasRect.height) return null;

        const aspect = clamp(instance.state.aspect, .2, 8);
        const padding = Math.min(24, Math.max(12, Math.min(canvasRect.width, canvasRect.height) * .04));
        const availableWidth = Math.max(1, canvasRect.width - padding * 2);
        const availableHeight = Math.max(1, canvasRect.height - padding * 2);
        const frameWidth = Math.min(availableWidth, availableHeight * aspect);
        const frameHeight = frameWidth / aspect;
        const frameLeft = (canvasRect.width - frameWidth) / 2;
        const frameTop = (canvasRect.height - frameHeight) / 2;
        const crop = cropMetrics(instance, instance.state.zoom);
        if (!crop.width || !crop.height) return null;

        // The frame represents the selected source rectangle. Scaling that
        // rectangle to the fixed frame makes zoom enlarge the image beneath it.
        const imageScale = frameWidth / crop.width;
        return {
            crop: crop,
            frameLeft: frameLeft,
            frameTop: frameTop,
            frameWidth: frameWidth,
            frameHeight: frameHeight,
            imageScale: imageScale,
            sourceLeft: frameLeft - crop.left * imageScale,
            sourceTop: frameTop - crop.top * imageScale,
            sourceWidth: Math.max(1, size.width * imageScale),
            sourceHeight: Math.max(1, size.height * imageScale)
        };
    }

    function apply(instance) {
        if (!instance || !instance.element) return;
        normalizeState(instance.state);
        const layout = layoutMetrics(instance);
        if (!layout) return;

        instance.source.style.left = `${layout.sourceLeft}px`;
        instance.source.style.top = `${layout.sourceTop}px`;
        instance.source.style.width = `${layout.sourceWidth}px`;
        instance.source.style.height = `${layout.sourceHeight}px`;
        instance.selection.style.left = `${layout.frameLeft}px`;
        instance.selection.style.top = `${layout.frameTop}px`;
        instance.selection.style.width = `${layout.frameWidth}px`;
        instance.selection.style.height = `${layout.frameHeight}px`;
        instance.element.style.setProperty("--media-crop-x", `${instance.state.x}%`);
        instance.element.style.setProperty("--media-crop-y", `${instance.state.y}%`);
        instance.element.style.setProperty("--media-crop-zoom", String(instance.state.zoom));
    }

    function notify(instance) {
        if (!instance || !instance.dotnet) return;
        instance.dotnet
            .invokeMethodAsync("CommitInteraction", instance.state.x, instance.state.y, instance.state.zoom)
            .catch(function () { });
    }

    function scheduleNotify(instance) {
        if (instance.notifyTimer !== null) window.clearTimeout(instance.notifyTimer);
        instance.notifyTimer = window.setTimeout(function () {
            instance.notifyTimer = null;
            notify(instance);
        }, 80);
    }

    function zoomPreservingCenter(instance, nextZoom) {
        const size = dimensions(instance);
        if (!size.width || !size.height) {
            instance.state.zoom = clamp(nextZoom, 1, instance.state.maxZoom);
            return;
        }

        const previous = cropMetrics(instance, instance.state.zoom);
        const centerX = previous.left + previous.width / 2;
        const centerY = previous.top + previous.height / 2;
        const normalizedZoom = clamp(nextZoom, 1, instance.state.maxZoom);
        const next = cropMetrics(instance, normalizedZoom);
        instance.state.x = next.availableX <= .001
            ? 50
            : clamp((centerX - next.width / 2) / next.availableX * 100, 0, 100);
        instance.state.y = next.availableY <= .001
            ? 50
            : clamp((centerY - next.height / 2) / next.availableY * 100, 0, 100);
        instance.state.zoom = normalizedZoom;
    }

    function destroy(id) {
        const instance = instances.get(id);
        if (!instance) return;

        instance.selection.removeEventListener("pointerdown", instance.onPointerDown);
        instance.element.removeEventListener("keydown", instance.onKeyDown);
        instance.element.removeEventListener("wheel", instance.onWheel);
        if (instance.image && instance.onImageReady) {
            instance.image.removeEventListener("load", instance.onImageReady);
            instance.image.removeEventListener("error", instance.onImageReady);
        }
        if (instance.pointerId !== null) {
            instance.selection.removeEventListener("pointermove", instance.onPointerMove);
            instance.selection.removeEventListener("pointerup", instance.onPointerUp);
            instance.selection.removeEventListener("pointercancel", instance.onPointerUp);
        }
        if (instance.resizeObserver) instance.resizeObserver.disconnect();
        if (instance.notifyTimer !== null) window.clearTimeout(instance.notifyTimer);
        instances.delete(id);
    }

    window.mediaCropEditor = {
        init: function (id, dotnet) {
            const element = document.getElementById(id);
            if (!element) return { width: 0, height: 0, resolved: true };
            destroy(id);

            const source = element.querySelector("[data-media-crop-source]");
            const selection = element.querySelector("[data-media-crop-selection]");
            const image = source ? source.querySelector("img") : null;
            if (!source || !selection || !image) return { width: 0, height: 0, resolved: true };

            const instance = {
                element: element,
                source: source,
                selection: selection,
                image: image,
                dotnet: dotnet,
                state: { x: 50, y: 50, zoom: 1, maxZoom: 1, aspect: 4 / 3, fit: "cover" },
                pointerId: null,
                startClientX: 0,
                startClientY: 0,
                startLeft: 0,
                startTop: 0,
                cropWidth: 0,
                cropHeight: 0,
                imageScale: 1,
                notifyTimer: null,
                onImageReady: null,
                resizeObserver: null
            };

            instance.onPointerDown = function (event) {
                if (event.button !== undefined && event.button !== 0) return;
                const layout = layoutMetrics(instance);
                if (!layout || !layout.crop.width || !layout.crop.height || !layout.imageScale) return;
                instance.pointerId = event.pointerId;
                instance.startClientX = event.clientX;
                instance.startClientY = event.clientY;
                instance.startLeft = layout.crop.left;
                instance.startTop = layout.crop.top;
                instance.cropWidth = layout.crop.width;
                instance.cropHeight = layout.crop.height;
                instance.imageScale = layout.imageScale;
                selection.setPointerCapture(event.pointerId);
                selection.classList.add("is-dragging");
                selection.addEventListener("pointermove", instance.onPointerMove);
                selection.addEventListener("pointerup", instance.onPointerUp);
                selection.addEventListener("pointercancel", instance.onPointerUp);
                event.preventDefault();
            };

            instance.onPointerMove = function (event) {
                if (event.pointerId !== instance.pointerId) return;
                const size = dimensions(instance);
                if (!size.width || !size.height || !instance.imageScale) return;

                // Drag the image, not the frame. Moving the pointer right reveals
                // source pixels to the left, which is the conventional cropper interaction.
                const deltaX = (event.clientX - instance.startClientX) / instance.imageScale;
                const deltaY = (event.clientY - instance.startClientY) / instance.imageScale;
                const availableX = Math.max(0, size.width - instance.cropWidth);
                const availableY = Math.max(0, size.height - instance.cropHeight);
                const left = clamp(instance.startLeft - deltaX, 0, availableX);
                const top = clamp(instance.startTop - deltaY, 0, availableY);
                instance.state.x = availableX <= .001 ? 50 : left / availableX * 100;
                instance.state.y = availableY <= .001 ? 50 : top / availableY * 100;
                apply(instance);
                event.preventDefault();
            };

            instance.onPointerUp = function (event) {
                if (event.pointerId !== instance.pointerId) return;
                try { selection.releasePointerCapture(event.pointerId); } catch { }
                instance.pointerId = null;
                selection.classList.remove("is-dragging");
                selection.removeEventListener("pointermove", instance.onPointerMove);
                selection.removeEventListener("pointerup", instance.onPointerUp);
                selection.removeEventListener("pointercancel", instance.onPointerUp);
                notify(instance);
            };

            instance.onWheel = function (event) {
                if (instance.state.maxZoom <= 1.001) return;
                const factor = Math.exp(-event.deltaY * .0004);
                const nextZoom = clamp(instance.state.zoom * factor, 1, instance.state.maxZoom);
                if (Math.abs(nextZoom - instance.state.zoom) < .0001) return;
                zoomPreservingCenter(instance, nextZoom);
                apply(instance);
                scheduleNotify(instance);
                event.preventDefault();
            };

            instance.onKeyDown = function (event) {
                if (event.key === "Escape") {
                    instance.dotnet.invokeMethodAsync("RequestClose").catch(function () { });
                    event.preventDefault();
                    return;
                }

                const step = event.shiftKey ? 5 : 1;
                let handled = true;
                if (event.key === "ArrowLeft") instance.state.x += step;
                else if (event.key === "ArrowRight") instance.state.x -= step;
                else if (event.key === "ArrowUp") instance.state.y += step;
                else if (event.key === "ArrowDown") instance.state.y -= step;
                else if (event.key === "+" || event.key === "=") zoomPreservingCenter(instance, instance.state.zoom + .05);
                else if (event.key === "-" || event.key === "_") zoomPreservingCenter(instance, instance.state.zoom - .05);
                else handled = false;
                if (!handled) return;

                normalizeState(instance.state);
                apply(instance);
                notify(instance);
                event.preventDefault();
            };

            instance.onImageReady = function () {
                apply(instance);
                dotnet
                    .invokeMethodAsync("SetImageSize", image.naturalWidth || 0, image.naturalHeight || 0)
                    .catch(function () { });
            };

            selection.addEventListener("pointerdown", instance.onPointerDown);
            element.addEventListener("keydown", instance.onKeyDown);
            element.addEventListener("wheel", instance.onWheel, { passive: false });
            instance.resizeObserver = new ResizeObserver(function () { apply(instance); });
            instance.resizeObserver.observe(element);
            instances.set(id, instance);

            if (image.complete) {
                apply(instance);
                return { width: image.naturalWidth || 0, height: image.naturalHeight || 0, resolved: true };
            }

            image.addEventListener("load", instance.onImageReady, { once: true });
            image.addEventListener("error", instance.onImageReady, { once: true });
            return { width: 0, height: 0, resolved: false };
        },

        setState: function (id, state) {
            const instance = instances.get(id);
            if (!instance) return;
            instance.state = Object.assign(instance.state, state || {});
            apply(instance);
        },

        destroy: destroy
    };
})();
