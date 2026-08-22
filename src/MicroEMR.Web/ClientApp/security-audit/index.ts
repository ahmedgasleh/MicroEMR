document.querySelectorAll<HTMLFormElement>("[data-security-audit-form]").forEach(form => {
    form.addEventListener("submit", () => {
        form.querySelectorAll<HTMLButtonElement>("[data-security-audit-submit]")
            .forEach(button => button.disabled = true);
        document.querySelectorAll<HTMLElement>("[data-security-audit-loading]")
            .forEach(status => status.classList.remove("d-none"));
    });
});
