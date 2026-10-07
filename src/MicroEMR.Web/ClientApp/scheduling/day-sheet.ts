interface DaySheetOptions {
    url: string;
    getDate: () => string;
    getClinicianUids: () => string[];
}

function localMidnight(date: Date): string {
    const pad = (value: number) => String(value).padStart(2, "0");
    const offset = -date.getTimezoneOffset();
    const sign = offset >= 0 ? "+" : "-";
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T00:00:00`
        + `${sign}${pad(Math.floor(Math.abs(offset) / 60))}:${pad(Math.abs(offset) % 60)}`;
}

function buildDaySheetUrl(baseUrl: string, dateKey: string, clinicianUids: string[] | null, order: string = "Alphabetic"): string {
    if (order !== "Alphabetic" && order !== "Chronological") throw new Error("Select a valid day sheet order.");
    if (clinicianUids?.length === 0) throw new Error("Select at least one provider in Day View before printing selected clinicians.");
    const start = new Date(`${dateKey}T00:00:00`);
    const end = new Date(start);
    end.setDate(end.getDate() + 1);
    const url = new URL(baseUrl, window.location.origin);
    url.searchParams.set("date", dateKey);
    url.searchParams.set("order", order);
    // Separate midnight offsets preserve 23/25-hour daylight-saving days.
    url.searchParams.set("start", localMidnight(start));
    url.searchParams.set("end", localMidnight(end));
    clinicianUids?.forEach(uid => url.searchParams.append("clinicianUids", uid));
    return url.toString();
}

function bindDaySheet(options: DaySheetOptions): void {
    const button = document.querySelector<HTMLButtonElement>("#schedulingPrintDaySheet");
    const scope = document.querySelector<HTMLSelectElement>("#schedulingDaySheetScope");
    const order = document.querySelector<HTMLSelectElement>("#schedulingDaySheetOrder");
    button?.addEventListener("click", () => {
        try {
            const uids = scope?.value === "selected" ? options.getClinicianUids() : null;
            window.open(buildDaySheetUrl(options.url, options.getDate(), uids, order?.value ?? "Alphabetic"), "_blank", "noopener");
        } catch (error) {
            window.alert(error instanceof Error ? error.message : "The day sheet could not be opened.");
        }
    });
}

declare global {
    interface Window { MicroEmrDaySheet: { bind: typeof bindDaySheet }; }
}
window.MicroEmrDaySheet = { bind: bindDaySheet };

document.addEventListener("DOMContentLoaded", () => {
    if (!document.querySelector("#schedulingDaySheet")) return;
    document.querySelectorAll<HTMLTimeElement>("[data-day-sheet-time]").forEach(element => {
        element.textContent = new Date(element.dateTime).toLocaleTimeString([], { hour: "numeric", minute: "2-digit" });
    });
    document.querySelectorAll<HTMLTimeElement>("[data-day-sheet-generated]").forEach(element => {
        element.textContent = new Date(element.dateTime).toLocaleString();
    });
    document.querySelector("#printDaySheetOutput")?.addEventListener("click", () => window.print());
});

export {};
