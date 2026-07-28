const startButton = document.querySelector("#startAppointmentEncounterButton");
const openButton = document.querySelector("#openAppointmentEncounterButton");
const statusBadge = document.querySelector("#linkedEncounterStatusBadge");
const token = document.querySelector('input[name="__RequestVerificationToken"]');
let current = null;
function encounterUrl(patientUid, encounterUid) {
    const url = new URL("/Patients/Details", window.location.origin);
    url.searchParams.set("patientUid", patientUid);
    url.searchParams.set("tab", "encounters");
    url.searchParams.set("encounterUid", encounterUid);
    return `${url.pathname}${url.search}`;
}
function render(details) {
    current = details;
    const linked = Boolean(details.linkedEncounterUid);
    startButton?.classList.toggle("d-none", linked);
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
    if (!current || !token)
        return;
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
        const result = await response.json();
        if (!response.ok || !result.success || !result.encounter) {
            throw new Error(result.message || "Encounter could not be started.");
        }
        window.location.assign(encounterUrl(result.encounter.patientUid, result.encounter.encounterUid));
    }
    catch (error) {
        window.alert(error instanceof Error ? error.message : "Encounter could not be started.");
        startButton.disabled = false;
    }
});
window.appointmentEncounterLinking = { render };
export {};
//# sourceMappingURL=appointment-encounter-linking.js.map