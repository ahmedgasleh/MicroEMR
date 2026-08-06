interface OverdueCountResponse {
    count: number;
}

const indicator = document.getElementById("overdueTaskIndicator");
const countElement = document.getElementById("overdueTaskCount");

const loadOverdueTaskCount = async (): Promise<void> => {
    if (!(indicator instanceof HTMLAnchorElement) || !countElement) return;

    try {
        const response = await fetch("/PatientTasks/OverdueCount", {
            method: "GET",
            credentials: "same-origin",
            headers: { Accept: "application/json" }
        });

        if (!response.ok) return;

        const value = await response.json() as OverdueCountResponse;
        if (!Number.isInteger(value.count) || value.count <= 0) return;

        const accessibleCount = value.count === 1 ? "1 overdue task" : `${value.count} overdue tasks`;
        countElement.textContent = value.count > 99 ? "99+" : value.count.toString();
        countElement.setAttribute("aria-hidden", "true");
        indicator.setAttribute("aria-label", accessibleCount);
        indicator.setAttribute("title", accessibleCount);
        indicator.classList.remove("d-none");
    } catch {
        // The indicator is optional; normal navigation remains available on failure.
    }
};

void loadOverdueTaskCount();
