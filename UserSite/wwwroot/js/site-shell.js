window.userSiteShell = window.userSiteShell || {};

window.userSiteShell.setThemeVars = (css) => {
    const target = document.getElementById("theme-vars");
    if (target) {
        target.textContent = css || "";
    }
};

window.userSiteShell.setLanguage = (lang, fallbackLanguage) => {
    const normalized = (lang || "en").toString().trim().toLowerCase() || "en";
    const normalizedFallback = (fallbackLanguage || "en").toString().trim().toLowerCase() || "en";
    window.cmsPublicLanguage = normalized;
    window.cmsPublicFallbackLanguage = normalizedFallback;
    document.documentElement.lang = normalized;
};

window.userSiteShell.initHeaderScroll = (dotNetRef) => {
    window.userSiteShell.disposeHeaderScroll();

    const topThreshold = 2;
    const hideTravelThreshold = 12;
    const showTravelThreshold = 8;

    let disposed = false;
    let animationFrame = 0;
    let direction = 0;
    let directionTravel = 0;
    let publishQueue = Promise.resolve();

    const getScrollY = () => {
        const documentHeight = Math.max(
            document.documentElement?.scrollHeight || 0,
            document.body?.scrollHeight || 0);
        const maximum = Math.max(0, documentHeight - window.innerHeight);
        return Math.min(maximum, Math.max(0, window.scrollY || window.pageYOffset || 0));
    };

    let lastY = getScrollY();
    let scrolled = lastY > topThreshold;
    let hidden = false;

    const publish = () => {
        const nextScrolled = scrolled;
        const nextHidden = hidden;

        // Preserve state order during quick direction changes without sending
        // a SignalR call for every scroll event.
        publishQueue = publishQueue
            .then(() => {
                if (!disposed) {
                    return dotNetRef.invokeMethodAsync("SetHeaderState", nextScrolled, nextHidden);
                }
            })
            .catch(() => { });
    };

    const evaluate = () => {
        animationFrame = 0;

        if (disposed) {
            return;
        }

        const currentY = getScrollY();
        const delta = currentY - lastY;
        const nextScrolled = currentY > topThreshold;
        let nextHidden = hidden;

        if (!nextScrolled) {
            // The absolute top state always wins: visible controls over a
            // transparent header surface.
            nextHidden = false;
            direction = 0;
            directionTravel = 0;
        } else if (Math.abs(delta) >= 0.5) {
            const nextDirection = delta > 0 ? 1 : -1;
            if (nextDirection !== direction) {
                direction = nextDirection;
                directionTravel = 0;
            }

            directionTravel += Math.abs(delta);

            if (direction > 0 && directionTravel >= hideTravelThreshold) {
                nextHidden = true;
                directionTravel = 0;
            } else if (direction < 0 && directionTravel >= showTravelThreshold) {
                nextHidden = false;
                directionTravel = 0;
            }
        }

        lastY = currentY;

        if (scrolled !== nextScrolled || hidden !== nextHidden) {
            scrolled = nextScrolled;
            hidden = nextHidden;
            publish();
        }
    };

    const scheduleEvaluation = () => {
        if (!disposed && animationFrame === 0) {
            animationFrame = window.requestAnimationFrame(evaluate);
        }
    };

    const reset = () => {
        if (disposed) {
            return;
        }

        lastY = getScrollY();
        direction = 0;
        directionTravel = 0;
        scrolled = lastY > topThreshold;
        hidden = false;
        publish();
    };

    window.addEventListener("scroll", scheduleEvaluation, { passive: true });
    window.addEventListener("resize", reset, { passive: true });
    window.userSiteShell.resetHeaderScroll = reset;
    publish();

    window.userSiteShell.disposeHeaderScroll = () => {
        disposed = true;
        window.removeEventListener("scroll", scheduleEvaluation);
        window.removeEventListener("resize", reset);

        if (animationFrame !== 0) {
            window.cancelAnimationFrame(animationFrame);
            animationFrame = 0;
        }

        window.userSiteShell.resetHeaderScroll = () => { };
        window.userSiteShell.disposeHeaderScroll = () => { };
    };
};

window.userSiteShell.disposeHeaderScroll = window.userSiteShell.disposeHeaderScroll || (() => {});
window.userSiteShell.resetHeaderScroll = window.userSiteShell.resetHeaderScroll || (() => {});
