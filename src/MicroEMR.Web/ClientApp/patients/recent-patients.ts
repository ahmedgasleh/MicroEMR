interface RecentPatient {
    patientUid: string;
    displayName: string;
    chartNumber: string;
    dateOfBirth?: string;
    lastOpenedAt: string;
}

const storageKey = "microemr.recentPatients.v1";
const maximumStoredPatients = 10;
const maximumDisplayedPatients = 5;

// MVP local recent-patient cache. Replace with server-side per-user recent patients for production.
function readRecentPatients(): RecentPatient[] {
    try {
        const value = window.localStorage.getItem(storageKey);
        if (!value) return [];

        const parsed: unknown = JSON.parse(value);
        if (!Array.isArray(parsed)) return [];

        return parsed.filter((item): item is RecentPatient => {
            if (!item || typeof item !== "object") return false;
            const candidate = item as Partial<RecentPatient>;
            return typeof candidate.patientUid === "string"
                && typeof candidate.displayName === "string"
                && typeof candidate.chartNumber === "string"
                && typeof candidate.lastOpenedAt === "string";
        });
    } catch {
        return [];
    }
}

function recordCurrentPatient(): void {
    const root = document.getElementById("patientChartBanner");
    if (!root) return;

    const patientUid = root.dataset.patientUid?.trim();
    const displayName = root.dataset.patientName?.trim();
    const chartNumber = root.dataset.chartNumber?.trim();
    if (!patientUid || !displayName || !chartNumber) return;

    const current: RecentPatient = {
        patientUid,
        displayName,
        chartNumber,
        dateOfBirth: root.dataset.dateOfBirth?.trim() || undefined,
        lastOpenedAt: new Date().toISOString()
    };

    const recentPatients = readRecentPatients()
        .filter(patient => patient.patientUid !== patientUid);
    recentPatients.unshift(current);

    try {
        window.localStorage.setItem(
            storageKey,
            JSON.stringify(recentPatients.slice(0, maximumStoredPatients)));
    } catch {
        // Patient navigation must continue when storage is disabled or full.
    }
}

function formatDateOfBirth(value?: string): string | null {
    if (!value) return null;
    const parts = value.split("-");
    return parts.length === 3 ? `${parts[1]}/${parts[2]}/${parts[0]}` : value;
}

function formatLastOpened(value: string): string {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return "Recently opened";

    return `Opened ${new Intl.DateTimeFormat(undefined, {
        dateStyle: "medium",
        timeStyle: "short"
    }).format(date)}`;
}

function renderRecentPatients(): void {
    const container = document.getElementById("recentPatientsList");
    if (!container) return;

    const patients = readRecentPatients().slice(0, maximumDisplayedPatients);
    container.replaceChildren();

    if (patients.length === 0) {
        const emptyState = document.createElement("div");
        emptyState.className = "text-body-secondary py-3";
        emptyState.textContent = "No recent patients yet.";
        container.append(emptyState);
        return;
    }

    patients.forEach(patient => {
        const link = document.createElement("a");
        link.className = "list-group-item list-group-item-action px-0 py-3";
        link.href = `/Patients/Details?patientUid=${encodeURIComponent(patient.patientUid)}&tab=summary`;

        const row = document.createElement("div");
        row.className = "d-flex justify-content-between align-items-center gap-3";

        const details = document.createElement("div");
        const name = document.createElement("div");
        name.className = "fw-semibold";
        name.textContent = patient.displayName;

        const metadata = document.createElement("div");
        metadata.className = "small text-body-secondary";
        const dateOfBirth = formatDateOfBirth(patient.dateOfBirth);
        metadata.textContent = dateOfBirth
            ? `Chart ${patient.chartNumber} · DOB ${dateOfBirth}`
            : `Chart ${patient.chartNumber}`;

        const opened = document.createElement("div");
        opened.className = "small text-body-secondary mt-1";
        opened.textContent = formatLastOpened(patient.lastOpenedAt);
        details.append(name, metadata, opened);

        const action = document.createElement("span");
        action.className = "btn btn-sm btn-outline-primary flex-shrink-0";
        action.textContent = "Open Chart";

        row.append(details, action);
        link.append(row);
        container.append(link);
    });
}

recordCurrentPatient();
renderRecentPatients();
