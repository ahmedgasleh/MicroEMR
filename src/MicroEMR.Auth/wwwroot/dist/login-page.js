"use strict";
const passwordInput = document.getElementById("Password");
const toggleButton = document.getElementById("btnTogglePassword");
const loginForm = document.querySelector(".auth-form");
const submitButton = document.getElementById("btnSignIn");
toggleButton?.addEventListener("click", () => {
    if (!passwordInput)
        return;
    const showPassword = passwordInput.type === "password";
    passwordInput.type = showPassword ? "text" : "password";
    toggleButton.setAttribute("aria-label", showPassword ? "Hide password" : "Show password");
    toggleButton.setAttribute("aria-pressed", showPassword.toString());
    toggleButton.classList.toggle("password-toggle--visible", showPassword);
});
loginForm?.addEventListener("submit", () => {
    if (!loginForm.checkValidity() || !submitButton)
        return;
    submitButton.disabled = true;
    submitButton.setAttribute("aria-busy", "true");
    submitButton.classList.add("auth-submit--busy");
});
//# sourceMappingURL=login-page.js.map