const splash = document.getElementById("microEmrSplash");
const loginForm = document.querySelector("[data-microemr-login-form]");
if (splash && loginForm) {
    const showSplash = () => {
        splash.hidden = false;
        splash.setAttribute("aria-hidden", "false");
        window.requestAnimationFrame(() => {
            splash.classList.remove("microemr-splash--hidden");
            splash.classList.add("microemr-splash--active");
        });
    };
    loginForm.addEventListener("submit", (event) => {
        if (!loginForm.checkValidity())
            return;
        // Unobtrusive validation may cancel submission after this listener runs.
        window.setTimeout(() => {
            if (!event.defaultPrevented)
                showSplash();
        }, 0);
    });
}
export {};
//# sourceMappingURL=splash-screen.js.map