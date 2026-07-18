(function () {
    "use strict";

    const ensureFocusAnchor = () => {
        let anchor = document.getElementById("microemrModalFocusAnchor");

        if (anchor) {
            return anchor;
        }

        anchor = document.createElement("button");
        anchor.type = "button";
        anchor.id = "microemrModalFocusAnchor";
        anchor.className = "visually-hidden";
        anchor.tabIndex = -1;
        anchor.textContent = "Focus anchor";
        document.body.prepend(anchor);

        return anchor;
    };

    const moveFocusOutside = modalElement => {
        if (!modalElement || !modalElement.contains(document.activeElement)) {
            return;
        }

        const focusedElement = document.activeElement;
        const anchor = ensureFocusAnchor();

        if (focusedElement && typeof focusedElement.blur === "function") {
            focusedElement.blur();
        }

        if (anchor && typeof anchor.focus === "function") {
            queueMicrotask(() => anchor.focus({ preventScroll: true }));
        }
    };

    const attachFocusGuard = modalElement => {
        if (!modalElement || modalElement.dataset.focusGuardAttached === "true") {
            return;
        }

        modalElement.dataset.focusGuardAttached = "true";

        modalElement.addEventListener("hide.bs.modal", () => {
            moveFocusOutside(modalElement);
        });

        modalElement.addEventListener("hidden.bs.modal", () => {
            moveFocusOutside(modalElement);
        });
    };

    const attachAll = () => {
        document.querySelectorAll(".modal").forEach(attachFocusGuard);
    };

    document.addEventListener("DOMContentLoaded", attachAll);

    document.addEventListener("shown.bs.modal", event => {
        attachFocusGuard(event.target);
    });
})();
