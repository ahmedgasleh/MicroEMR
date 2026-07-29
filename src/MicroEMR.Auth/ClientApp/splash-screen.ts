const splash = document.getElementById("microEmrSplash");

if (splash) {
    const minimumVisibleDuration = 650;
    const fallbackDuration = 2000;
    const fadeDuration = 250;
    const startedAt = Date.now();
    let isHidden = false;

    const hideSplash = (): void => {
        if (isHidden) return;

        isHidden = true;
        splash.classList.add("microemr-splash--hidden");
        splash.setAttribute("aria-hidden", "true");
        document.body.classList.add("microemr-app-ready");

        window.setTimeout(() => {
            splash.hidden = true;
        }, fadeDuration);
    };

    const hideAfterMinimumDuration = (): void => {
        const elapsed = Date.now() - startedAt;
        window.setTimeout(hideSplash, Math.max(0, minimumVisibleDuration - elapsed));
    };

    if (document.readyState === "complete") {
        hideAfterMinimumDuration();
    } else {
        window.addEventListener("load", hideAfterMinimumDuration, { once: true });
    }

    window.setTimeout(hideSplash, fallbackDuration);
}
