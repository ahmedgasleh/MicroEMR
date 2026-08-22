"use strict";
document.querySelectorAll("[data-security-audit-form]").forEach(form => {
    form.addEventListener("submit", () => {
        form.querySelectorAll("[data-security-audit-submit]")
            .forEach(button => button.disabled = true);
        document.querySelectorAll("[data-security-audit-loading]")
            .forEach(status => status.classList.remove("d-none"));
    });
});
//# sourceMappingURL=index.js.map