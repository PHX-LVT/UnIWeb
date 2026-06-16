window.userSiteShell = window.userSiteShell || {};

window.userSiteShell.setThemeVars = (css) => {
    const target = document.getElementById("theme-vars");
    if (target) {
        target.textContent = css || "";
    }
};

window.userSiteShell.initHeaderScroll = (dotNetRef) => {
    window.userSiteShell.disposeHeaderScroll();

    let scrolled = window.scrollY > 40;
    let disposed = false;

    const notify = () => {
        if (disposed) {
            return;
        }

        const next = window.scrollY > 40;
        if (next === scrolled) {
            return;
        }

        scrolled = next;
        dotNetRef.invokeMethodAsync("SetHeaderScrolled", scrolled);
    };

    window.addEventListener("scroll", notify, { passive: true });
    dotNetRef.invokeMethodAsync("SetHeaderScrolled", scrolled);

    window.userSiteShell.disposeHeaderScroll = () => {
        disposed = true;
        window.removeEventListener("scroll", notify);
    };
};

window.userSiteShell.disposeHeaderScroll = window.userSiteShell.disposeHeaderScroll || (() => {});
