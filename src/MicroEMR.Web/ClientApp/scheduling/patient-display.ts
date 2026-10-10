interface SchedulePatientDisplayData {
    eventKind?: string;
    isAdHoc?: boolean;
    html?: string;
    text?: string | null;
    patientDisplayName?: string | null;
    patientHealthCardNumber?: string | null;
    patientDateOfBirth?: string | null;
    patientGender?: string | null;
    reason?: string | null;
    appointmentType?: string | null;
}

function encode(value: string): string {
    return value.replace(/[&<>"']/g, character => ({
        "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
    })[character]!);
}

function value(text: string | null | undefined): string {
    return text?.trim() || "Not recorded";
}

function birthDate(text: string | null | undefined): string {
    if (!text || !/^\d{4}-\d{2}-\d{2}$/.test(text) || text === "0001-01-01") return "Not recorded";
    const [year, month, day] = text.split("-").map(Number);
    // DOB is a calendar date, not a UTC instant: prevent timezone day shifts.
    const date = new Date(0);
    date.setFullYear(year, month - 1, day);
    if (date.getFullYear() !== year || date.getMonth() !== month - 1 || date.getDate() !== day) return "Not recorded";
    return date.toLocaleDateString("en-CA", { month: "short", day: "numeric", year: "numeric" });
}

class SchedulePatientDisplay {
    private expanded = false;

    constructor(control: HTMLSelectElement | null, redraw: () => void) {
        if (control) {
            control.value = "name";
            control.addEventListener("change", () => {
                this.expanded = control.value === "details";
                redraw();
            });
        }
    }

    render(appointment: SchedulePatientDisplayData): string {
        const name = encode(value(appointment.patientDisplayName));
        const mode = appointment.isAdHoc ? "<strong>Ad-hoc</strong> · " : "";
        if (!this.expanded) return mode + name;
        const lineStyle = "white-space:nowrap;overflow:hidden;text-overflow:ellipsis";
        return `<div style="width:100%;min-width:0;padding-right:22px;font-size:11px;line-height:13px">`
            + `<div style="${lineStyle}">${encode(this.identifiers(appointment))}</div>`
            + `<div style="${lineStyle}">${mode}${encode(this.description(appointment))}</div></div>`;
    }

    private identifiers(appointment: SchedulePatientDisplayData): string {
        return `HCN: ${value(appointment.patientHealthCardNumber)} · DOB: ${birthDate(appointment.patientDateOfBirth)}`
            + ` · Gender: ${value(appointment.patientGender)}`;
    }

    private description(appointment: SchedulePatientDisplayData): string {
        const reason = appointment.reason?.trim() || appointment.appointmentType?.trim();
        return value(appointment.patientDisplayName) + (reason ? ` — ${reason}` : "");
    }

    private tooltip(appointment: SchedulePatientDisplayData): string {
        const mode = appointment.isAdHoc ? "Ad-hoc · " : "";
        if (!this.expanded) return mode + value(appointment.patientDisplayName);
        return `${this.identifiers(appointment)}\n${mode}${this.description(appointment)}`;
    }

    apply<T extends SchedulePatientDisplayData>(events: T[]): T[] {
        return events.map(event => event.eventKind === "appointment" ? {
            ...event, text: (event.isAdHoc ? "Ad-hoc · " : "") + value(event.patientDisplayName), html: this.render(event), toolTip: this.tooltip(event)
        } : event);
    }
}

declare global {
    interface Window {
        SchedulePatientDisplay: typeof SchedulePatientDisplay;
    }
}

window.SchedulePatientDisplay = SchedulePatientDisplay;
export {};
