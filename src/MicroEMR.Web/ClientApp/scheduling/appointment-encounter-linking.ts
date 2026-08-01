interface AppointmentLinkDetails {
    appointmentUid: string;
    patientUid: string;
    status: string;
    linkedEncounterUid?: string | null;
    linkedEncounterStatus?: string | null;
}

interface StartEncounterResult {
    success: boolean;
    message?: string;
    encounter?: { encounterUid: string; patientUid: string; status: string };
}

const startButton = document.querySelector<HTMLButtonElement>("#startAppointmentEncounterButton");
const openButton = document.querySelector<HTMLAnchorElement>("#openAppointmentEncounterButton");
const statusBadge = document.querySelector<HTMLElement>("#linkedEncounterStatusBadge");
const token = document.querySelector<HTMLInputElement>('input[name="__RequestVerificationToken"]');
let current: AppointmentLinkDetails | null = null;

function encounterUrl(patientUid: string, encounterUid: string): string {
    const url = new URL("/Patients/Details", window.location.origin);
    url.searchParams.set("patientUid", patientUid);
    url.searchParams.set("tab", "encounters");
    url.searchParams.set("encounterUid", encounterUid);
    return `${url.pathname}${url.search}`;
}

function render(details: AppointmentLinkDetails): void {
    current = details;
    const linked = Boolean(details.linkedEncounterUid);
    const status = details.status.toLowerCase();
    const canStart = ["scheduled", "arrived", "checkedin", "roomed"].includes(status);
    startButton?.classList.toggle("d-none", linked || !canStart);
    openButton?.classList.toggle("d-none", !linked);
    statusBadge?.classList.toggle("d-none", !linked);
    if (linked && openButton && details.linkedEncounterUid) {
        openButton.href = encounterUrl(details.patientUid, details.linkedEncounterUid);
    }
    if (linked && statusBadge) {
        statusBadge.textContent = `Encounter: ${details.linkedEncounterStatus || "Open"}`;
    }
}

startButton?.addEventListener("click", async () => {
    if (!current || !token) return;
    startButton.disabled = true;
    try {
        const body = new URLSearchParams({
            appointmentUid: current.appointmentUid,
            __RequestVerificationToken: token.value
        });
        const response = await fetch("/Scheduling/StartEncounter", {
            method: "POST",
            headers: { "Content-Type": "application/x-www-form-urlencoded;charset=UTF-8" },
            body
        });
        const result = await response.json() as StartEncounterResult;
        if (!response.ok || !result.success || !result.encounter) {
            throw new Error(result.message || "Encounter could not be started.");
        }
        window.location.assign(encounterUrl(result.encounter.patientUid, result.encounter.encounterUid));
    } catch (error) {
        window.alert(error instanceof Error ? error.message : "Encounter could not be started.");
        startButton.disabled = false;
    }
});

declare global {
    interface Window {
        appointmentEncounterLinking: { render: (details: AppointmentLinkDetails) => void };
    }
}

window.appointmentEncounterLinking = { render };

export {};
