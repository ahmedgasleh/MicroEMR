import { partialDate, type PartialDate, type ClinicalFields } from "../patients/history-clinical-fields.js";

interface Procedure { historyUid: string; patientUid: string; historyType: string; description: string; rowVersion: string; clinicalFields?: ClinicalFields; }
interface Context { patientUid: string; encounterUid: string; }
interface State extends Context { rowVersion: string; status: string; canManage: boolean; items: Procedure[]; }
const root = document.querySelector<HTMLElement>("#encounterProcedures");
if (root) {
    const element = <T extends HTMLElement>(id: string) => document.querySelector<T>(`#${id}`)!;
    const select = element<HTMLSelectElement>("encounterProcedureSelection"), fields = element<HTMLFieldSetElement>("encounterProcedureFields");
    const add = element<HTMLButtonElement>("newEncounterProcedure"), save = element<HTMLButtonElement>("saveEncounterProcedure"), reload = element<HTMLButtonElement>("reloadEncounterProcedures");
    const description = element<HTMLTextAreaElement>("encounterProcedureDescription"), notes = element<HTMLTextAreaElement>("encounterProcedureNotes");
    const year = element<HTMLInputElement>("encounterProcedureYear"), month = element<HTMLInputElement>("encounterProcedureMonth"), day = element<HTMLInputElement>("encounterProcedureDay");
    const message = element<HTMLElement>("encounterProcedureMessage");
    let context: Context | undefined, state: State | undefined, selected: Procedure | undefined, newUid: string | undefined;
    let generation = 0, busy = false, dirty = false, mustReload = false;
    const controls = () => {
        root.dataset.busy = String(busy);
        select.disabled = busy || !state;
        add.disabled = busy || !state?.canManage || mustReload;
        fields.disabled = busy || !state?.canManage || mustReload || !(selected || newUid);
        save.disabled = fields.disabled || !dirty;
        reload.disabled = busy || !context;
    };
    const clear = () => { selected = undefined; newUid = undefined; description.value = notes.value = year.value = month.value = day.value = ""; dirty = false; };
    const display = (row: Procedure) => {
        selected = row; newUid = undefined; description.value = row.description; notes.value = row.clinicalFields?.notes || "";
        const date = row.clinicalFields?.procedureDate;
        year.value = String(date?.year ?? ""); month.value = String(date?.month ?? ""); day.value = String(date?.day ?? "");
        dirty = false;
    };
    const url = (value: Context) => `${root.dataset.url}?patientUid=${encodeURIComponent(value.patientUid)}&encounterUid=${encodeURIComponent(value.encounterUid)}`;
    async function response<T>(value: Response): Promise<T> {
        const body = await value.json().catch(() => ({}));
        if (!value.ok) throw new Error(body.message || body.detail || "Procedure operation failed. Reload before retrying.");
        return body as T;
    }
    async function load(value: Context, keepDraft: boolean) {
        const previousUid = selected?.historyUid || newUid;
        const current = ++generation; context = value; busy = true; state = undefined; controls();
        if (!keepDraft) clear();
        try {
            const loaded = await response<State>(await fetch(url(value), { cache: "no-store", headers: { Accept: "application/json" } }));
            if (current !== generation) return;
            if (loaded.patientUid !== value.patientUid || loaded.encounterUid !== value.encounterUid || loaded.items.some(x => x.patientUid !== value.patientUid))
                throw new Error("Encounter context changed. Reload before saving.");
            state = loaded; select.replaceChildren(new Option("Select an entry", ""));
            for (const item of loaded.items) select.append(new Option(`${item.historyType}: ${item.description} · ${partialDate(item.clinicalFields?.procedureDate)}`, item.historyUid));
            if (keepDraft && loaded.canManage) select.value = previousUid || "";
            else { clear(); mustReload = false; }
            message.textContent = loaded.canManage ? "Select an existing entry to update, or explicitly record a new procedure." : "Current CPP procedures are read-only for this encounter or role.";
        } catch (error) { if (current === generation) { mustReload = true; message.textContent = error instanceof Error ? error.message : "Procedures unavailable."; } }
        finally { if (current === generation) { busy = false; controls(); } }
    }
    document.addEventListener("encounter-diagnoses-context", event => {
        const value = (event as CustomEvent<Context>).detail;
        const same = context?.patientUid === value.patientUid && context?.encounterUid === value.encounterUid;
        if (!same) mustReload = false;
        void load(value, same && dirty);
    });
    document.addEventListener("encounter-diagnoses-saved", event => {
        const value = (event as CustomEvent<Context>).detail;
        if (context?.patientUid === value.patientUid && context?.encounterUid === value.encounterUid) void load(value, dirty);
    });
    document.addEventListener("encounter-contribution-saved", event => {
        const value = (event as CustomEvent<Context>).detail;
        if (context?.patientUid === value.patientUid && context?.encounterUid === value.encounterUid) void load(value, dirty);
    });
    select.addEventListener("change", () => {
        if (dirty && !confirm("Discard unsaved procedure changes?")) { select.value = selected?.historyUid || ""; return; }
        clear(); const row = state?.items.find(x => x.historyUid === select.value); if (row) display(row); controls();
    });
    add.addEventListener("click", () => {
        if (busy || !state?.canManage || mustReload || dirty && !confirm("Discard unsaved procedure changes?")) return;
        clear(); select.value = ""; newUid = crypto.randomUUID(); dirty = true; controls(); description.focus();
    });
    for (const input of [description, notes, year, month, day]) input.addEventListener("input", () => { dirty = true; controls(); });
    reload.addEventListener("click", () => {
        if (context && !busy && (!dirty || confirm("Reload procedures and discard unsaved procedure edits? Note text is preserved."))) void load(context, false);
    });
    save.addEventListener("click", async () => {
        if (busy || mustReload || !dirty || !state?.canManage || !context || !(selected || newUid)) return;
        if (element<HTMLButtonElement>("saveEncounterNoteButton").disabled || document.querySelector<HTMLElement>("#encounterDiagnoses")?.dataset.busy === "true" || document.querySelector<HTMLElement>("#encounterContributions")?.dataset.busy === "true") {
            message.textContent = "Wait for the note or diagnosis operation to finish."; return;
        }
        if (![description, notes, year, month, day].every(input => input.reportValidity()) || !description.value.trim()) return;
        const number = (input: HTMLInputElement) => input.value === "" ? null : Number(input.value);
        const date: PartialDate = { year: number(year), month: number(month), day: number(day) };
        const value = context, current = generation, uid = selected?.historyUid || newUid!;
        const payload = { historyUid: uid, isNew: !selected, encounterRowVersion: state.rowVersion, historyRowVersion: selected?.rowVersion,
            description: description.value.trim(), procedureDate: date, notes: notes.value.trim() || null };
        const csrf = document.querySelector<HTMLInputElement>('#encounterNoteAntiforgeryForm input[name="__RequestVerificationToken"]')!.value;
        busy = true; controls(); message.textContent = "Saving procedure...";
        try {
            const row = await response<Procedure>(await fetch(url(value), { method: "POST", headers: { "Content-Type": "application/json", RequestVerificationToken: csrf }, body: JSON.stringify(payload) }));
            if (current !== generation) return;
            if (row.patientUid !== value.patientUid || row.historyUid !== uid) throw new Error("Procedure context changed. Reload before retrying.");
            display(row); mustReload = false;
            state.items = [...state.items.filter(x => x.historyUid !== row.historyUid), row];
            select.replaceChildren(new Option("Select an entry", ""));
            for (const item of state.items) select.append(new Option(`${item.historyType}: ${item.description}`, item.historyUid));
            select.value = row.historyUid;
            document.dispatchEvent(new CustomEvent("encounter-procedure-saved", { detail: { ...value, historyUid: row.historyUid } }));
            message.textContent = "Procedure saved to CPP. Note text is unchanged."; message.focus();
        } catch (error) {
            if (current === generation) { mustReload = true; message.textContent = `${error instanceof Error ? error.message : "Save outcome uncertain."} Reload procedures before another save; unsaved note text is preserved.`; }
        } finally { if (current === generation) { busy = false; controls(); } }
    });
    // Capture before legacy note/diagnosis handlers. No note fields are read, rewritten or submitted here.
    for (const id of ["saveEncounterNoteButton", "saveEncounterDiagnosesOnly", "saveEncounterDiagnosesCpp", "signEncounterButton"]) {
        element<HTMLElement>(id)?.addEventListener("click", event => {
            if (busy || id === "signEncounterButton" && dirty) {
                event.preventDefault(); event.stopImmediatePropagation(); message.textContent = "Finish or discard procedure changes before signing or starting another save.";
            }
        }, true);
    }
    document.querySelector("#encounterContributions")?.addEventListener("click", event => {
        if (busy) { event.preventDefault(); event.stopImmediatePropagation(); message.textContent = "Wait for the procedure save to finish."; }
    }, true);
    document.querySelector("#encounterDetailsModal")?.addEventListener("hide.bs.modal", event => {
        if (busy || dirty && !confirm("Discard unsaved procedure changes?")) event.preventDefault();
    });
    document.querySelector("#encounterDetailsModal")?.addEventListener("hidden.bs.modal", () => {
        generation++; context = undefined; state = undefined; busy = false; mustReload = false; clear(); controls();
    });
    controls();
}
