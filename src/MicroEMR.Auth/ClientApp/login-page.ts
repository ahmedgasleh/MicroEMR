export {};

const passwordInput = document.getElementById("Password") as HTMLInputElement | null;
const toggleButton = document.getElementById("btnTogglePassword") as HTMLButtonElement | null;
const loginForm = document.querySelector<HTMLFormElement>(".auth-form");
const submitButton = document.getElementById("btnSignIn") as HTMLButtonElement | null;

toggleButton?.addEventListener("click", () => {
    if (!passwordInput) return;

    const showPassword = passwordInput.type === "password";
    passwordInput.type = showPassword ? "text" : "password";
    toggleButton.setAttribute("aria-label", showPassword ? "Hide password" : "Show password");
    toggleButton.setAttribute("aria-pressed", showPassword.toString());
    toggleButton.classList.toggle("password-toggle--visible", showPassword);
});

loginForm?.addEventListener("submit", (event: SubmitEvent) => {
    if (!loginForm.checkValidity() || !submitButton) return;

    // Wait until unobtrusive validation has had an opportunity to cancel submit.
    window.setTimeout(() => {
        if (event.defaultPrevented) return;

        submitButton.disabled = true;
        submitButton.setAttribute("aria-busy", "true");
        submitButton.classList.add("auth-submit--busy");
    }, 0);
});
