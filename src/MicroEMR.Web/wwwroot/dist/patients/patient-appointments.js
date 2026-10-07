function escapeHistory(value) {
    return (value?.trim() || "Not recorded").replace(/[&<>"']/g, character => ({
        "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
    })[character]);
}
function renderAppointmentHistory(items, now = Date.now()) {
    if (!items.length)
        return '<p class="text-body-secondary">No appointments found.</p>';
    const upcoming = items.filter(item => Date.parse(item.startDateTimeUtc) >= now)
        .sort((a, b) => Date.parse(a.startDateTimeUtc) - Date.parse(b.startDateTimeUtc) || a.appointmentUid.localeCompare(b.appointmentUid));
    const past = items.filter(item => Date.parse(item.startDateTimeUtc) < now)
        .sort((a, b) => Date.parse(b.startDateTimeUtc) - Date.parse(a.startDateTimeUtc) || a.appointmentUid.localeCompare(b.appointmentUid));
    function group(title, rows) {
        if (!rows.length)
            return `<h6>${title}</h6><p class="text-body-secondary">No ${title.toLowerCase()} appointments.</p>`;
        return `<h6>${title}</h6><div class="table-responsive"><table class="table table-hover align-middle"><thead><tr>`
            + '<th>Date</th><th>Time</th><th>Status</th><th>Type</th><th>Provider / Resource</th><th>Reason</th></tr></thead><tbody>'
            + rows.map(item => {
                const start = new Date(item.startDateTimeUtc), end = new Date(item.endDateTimeUtc);
                const time = (date) => date.toLocaleTimeString([], { hour: "numeric", minute: "2-digit" });
                return `<tr><td>${escapeHistory(start.toLocaleDateString())}</td><td>${escapeHistory(`${time(start)}–${time(end)}`)}</td>`
                    + `<td>${escapeHistory(item.status)}</td><td>${escapeHistory(item.appointmentType)}</td>`
                    + `<td>${escapeHistory(item.primaryResourceName)}</td><td>${escapeHistory(item.reason)}</td></tr>`;
            }).join("") + '</tbody></table></div>';
    }
    return group("Upcoming", upcoming) + group("Past", past);
}
document.addEventListener("DOMContentLoaded", () => {
    const root = document.querySelector("#patientAppointmentHistory");
    if (!root)
        return;
    let loaded = false, loading = false;
    async function load() {
        if (!root || loaded || loading)
            return;
        loading = true;
        root.textContent = "Loading appointments…";
        try {
            const response = await fetch(root.dataset.listUrl, { headers: { Accept: "application/json" }, cache: "no-store" });
            const result = await response.json();
            if (!response.ok || !result.success)
                throw new Error("Appointment history could not be loaded.");
            root.innerHTML = renderAppointmentHistory(result.items);
            loaded = true;
        }
        catch {
            root.textContent = "Appointment history could not be loaded. Reopen this tab to retry.";
        }
        finally {
            loading = false;
        }
    }
    document.querySelector('[data-bs-target="#appointments"]')?.addEventListener("shown.bs.tab", () => void load());
    if (document.querySelector("#appointments")?.classList.contains("active"))
        void load();
});
export {};
//# sourceMappingURL=patient-appointments.js.map