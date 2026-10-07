function encode(value) {
    return value.replace(/[&<>"']/g, character => ({
        "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
    })[character]);
}
function value(text) {
    return text?.trim() || "Not recorded";
}
function birthDate(text) {
    if (!text || !/^\d{4}-\d{2}-\d{2}$/.test(text) || text === "0001-01-01")
        return "Not recorded";
    const [year, month, day] = text.split("-").map(Number);
    // DOB is a calendar date, not a UTC instant: prevent timezone day shifts.
    const date = new Date(0);
    date.setFullYear(year, month - 1, day);
    if (date.getFullYear() !== year || date.getMonth() !== month - 1 || date.getDate() !== day)
        return "Not recorded";
    return date.toLocaleDateString("en-CA", { month: "short", day: "numeric", year: "numeric" });
}
class SchedulePatientDisplay {
    constructor(control, redraw) {
        this.expanded = false;
        if (control) {
            control.value = "name";
            control.addEventListener("change", () => {
                this.expanded = control.value === "details";
                redraw();
            });
        }
    }
    render(appointment) {
        const name = encode(value(appointment.patientDisplayName));
        if (!this.expanded)
            return name;
        const lineStyle = "white-space:nowrap;overflow:hidden;text-overflow:ellipsis";
        return `<div style="width:100%;min-width:0;padding-right:22px;font-size:11px;line-height:13px">`
            + `<div style="${lineStyle}">${encode(this.identifiers(appointment))}</div>`
            + `<div style="${lineStyle}">${encode(this.description(appointment))}</div></div>`;
    }
    identifiers(appointment) {
        return `HCN: ${value(appointment.patientHealthCardNumber)} · DOB: ${birthDate(appointment.patientDateOfBirth)}`
            + ` · Gender: ${value(appointment.patientGender)}`;
    }
    description(appointment) {
        const reason = appointment.reason?.trim() || appointment.appointmentType?.trim();
        return value(appointment.patientDisplayName) + (reason ? ` — ${reason}` : "");
    }
    tooltip(appointment) {
        if (!this.expanded)
            return value(appointment.patientDisplayName);
        return `${this.identifiers(appointment)}\n${this.description(appointment)}`;
    }
    apply(events) {
        return events.map(event => event.eventKind === "appointment" ? {
            ...event, text: value(event.patientDisplayName), html: this.render(event), toolTip: this.tooltip(event)
        } : event);
    }
}
window.SchedulePatientDisplay = SchedulePatientDisplay;
export {};
//# sourceMappingURL=patient-display.js.map