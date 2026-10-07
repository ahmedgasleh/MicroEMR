function localMidnight(date) {
    const pad = (value) => String(value).padStart(2, "0");
    const offset = -date.getTimezoneOffset();
    const sign = offset >= 0 ? "+" : "-";
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T00:00:00`
        + `${sign}${pad(Math.floor(Math.abs(offset) / 60))}:${pad(Math.abs(offset) % 60)}`;
}
function buildDaySheetUrl(baseUrl, dateKey, clinicianUids) {
    if (clinicianUids?.length === 0)
        throw new Error("Select at least one provider in Day View before printing selected clinicians.");
    const start = new Date(`${dateKey}T00:00:00`);
    const end = new Date(start);
    end.setDate(end.getDate() + 1);
    const url = new URL(baseUrl, window.location.origin);
    url.searchParams.set("date", dateKey);
    // Separate midnight offsets preserve 23/25-hour daylight-saving days.
    url.searchParams.set("start", localMidnight(start));
    url.searchParams.set("end", localMidnight(end));
    clinicianUids?.forEach(uid => url.searchParams.append("clinicianUids", uid));
    return url.toString();
}
function bindDaySheet(options) {
    const button = document.querySelector("#schedulingPrintDaySheet");
    const scope = document.querySelector("#schedulingDaySheetScope");
    button?.addEventListener("click", () => {
        try {
            const uids = scope?.value === "selected" ? options.getClinicianUids() : null;
            window.open(buildDaySheetUrl(options.url, options.getDate(), uids), "_blank", "noopener");
        }
        catch (error) {
            window.alert(error instanceof Error ? error.message : "The day sheet could not be opened.");
        }
    });
}
window.MicroEmrDaySheet = { bind: bindDaySheet };
document.addEventListener("DOMContentLoaded", () => {
    if (!document.querySelector("#schedulingDaySheet"))
        return;
    document.querySelectorAll("[data-day-sheet-time]").forEach(element => {
        element.textContent = new Date(element.dateTime).toLocaleTimeString([], { hour: "numeric", minute: "2-digit" });
    });
    document.querySelectorAll("[data-day-sheet-generated]").forEach(element => {
        element.textContent = new Date(element.dateTime).toLocaleString();
    });
    document.querySelector("#printDaySheetOutput")?.addEventListener("click", () => window.print());
});
export {};
//# sourceMappingURL=day-sheet.js.map