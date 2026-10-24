interface Medication {
    medicationUid: string; patientUid: string; medicationName: string; status: string; rowVersion: string;
    classification?: string | null; strength?: string | null; dosageForm?: string | null; route?: string | null;
    directions?: string | null; frequency?: string | null; startDate?: string | null; endDate?: string | null;
    indication?: string | null; prescriberName?: string | null; notes?: string | null; createdAt?: string;
}
interface MedicationContext { patientUid: string; encounterUid: string; }
interface MedicationState extends MedicationContext { rowVersion: string; status: string; canManage: boolean; items: Medication[]; }
const root = document.querySelector<HTMLElement>("#encounterMedications");
if (root) {
    const element = <T extends HTMLElement>(id: string) => document.querySelector<T>(`#${id}`)!;
    const select = element<HTMLSelectElement>("encounterMedicationSelection"), fields = element<HTMLFieldSetElement>("encounterMedicationFields");
    const add = element<HTMLButtonElement>("newEncounterMedication"), save = element<HTMLButtonElement>("saveEncounterMedication");
    const discontinue = element<HTMLButtonElement>("discontinueEncounterMedication"), reload = element<HTMLButtonElement>("reloadEncounterMedications");
    const reason = element<HTMLInputElement>("encounterMedicationReason"), message = element<HTMLElement>("encounterMedicationMessage");
    const keys = ["MedicationName", "Strength", "DosageForm", "Route", "Frequency", "Indication", "PrescriberName", "Classification", "StartDate", "EndDate", "Directions", "Notes"] as const;
    const inputs = keys.map(key => element<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>("encounterMedication" + key));
    let context: MedicationContext | undefined, state: MedicationState | undefined, selected: Medication | undefined;
    let generation = 0, busy = false, dirty = false, creating = false, mustReload = false;
    const controls = () => {
        root.dataset.busy = String(busy);
        select.disabled = busy || !state;
        add.disabled = busy || !state?.canManage || mustReload;
        fields.disabled = busy || !state?.canManage || mustReload || !(creating || selected?.status === "Active");
        save.disabled = fields.disabled || !dirty;
        discontinue.disabled = busy || dirty || mustReload || !state?.canManage || selected?.status !== "Active";
        reason.disabled = discontinue.disabled;
        reload.disabled = busy || !context;
    };
    const clear = () => { inputs.forEach(x => x.value = ""); reason.value = ""; selected = undefined; dirty = creating = false; };
    const display = (row: Medication) => {
        selected = row; creating = dirty = false; reason.value = "";
        keys.forEach((key, index) => {
            const value = row[(key[0].toLowerCase() + key.slice(1)) as keyof Medication];
            inputs[index].value = value == null || value === "Unclassified" ? "" : key.endsWith("Date") ? String(value).slice(0, 10) : String(value);
        });
    };
    const url = (value: MedicationContext) => `${root.dataset.url}?patientUid=${encodeURIComponent(value.patientUid)}&encounterUid=${encodeURIComponent(value.encounterUid)}`;
    async function response<T>(value: Response): Promise<T> {
        const body = await value.json().catch(() => ({}));
        if (!value.ok) throw new Error(body.message || body.detail || "Medication operation failed. Reload before retrying.");
        return body as T;
    }
    function options() {
        select.replaceChildren(new Option("Select a medication", ""));
        for (const row of state?.items || []) select.append(new Option(`${row.medicationName} ${row.strength || ""} · ${row.status} · ${row.classification || "Unclassified"}`, row.medicationUid));
    }
    function refreshCpp() {
        const target = document.querySelector<HTMLElement>("#cppMedicationItems"); if (!target || !state) return;
        target.replaceChildren();
        const ongoing = state.items.filter(x => x.status.toLowerCase() === "active" && x.classification === "Ongoing")
            .sort((a, b) => Date.parse(b.startDate || b.createdAt || "1970-01-01") - Date.parse(a.startDate || a.createdAt || "1970-01-01")).slice(0, 5);
        if (!ongoing.length) { const p = document.createElement("p"); p.className = "text-muted mb-0"; p.textContent = "No ongoing medications recorded."; target.append(p); }
        for (const row of ongoing) {
            const block = document.createElement("div"); block.className = "border-top py-2";
            for (const [key, value] of [["DisplayName", row.medicationName], ["Strength", row.strength], ["Frequency", row.frequency], ["Route", row.route]]) {
                if (!value) continue; const text = document.createElement("span"); text.dataset.cppField = `Medications.${key}`; text.textContent = value;
                text.className = key === "DisplayName" ? "fw-semibold me-1" : "small me-1"; block.append(text);
            }
            target.append(block);
        }
    }
    async function load(value: MedicationContext, keepDraft: boolean, savedUid?: string) {
        const previous = selected; const current = ++generation; context = value; busy = true; state = undefined; controls();
        if (!keepDraft) clear();
        try {
            const loaded = await response<MedicationState>(await fetch(url(value), { cache: "no-store", headers: { Accept: "application/json" } }));
            if (current !== generation) return;
            if (loaded.patientUid !== value.patientUid || loaded.encounterUid !== value.encounterUid || loaded.items.some(x => x.patientUid !== value.patientUid)) throw new Error("Medication context changed. Reload before saving.");
            state = loaded; options(); refreshCpp();
            if (keepDraft && loaded.canManage) { selected = previous; select.value = previous?.medicationUid || ""; }
            else {
                clear(); mustReload = false;
                const row = loaded.items.find(x => x.medicationUid === savedUid); if (row) { display(row); select.value = row.medicationUid; }
            }
            message.textContent = savedUid ? "Medication Summary saved and CPP refreshed. Note text and prescriptions are unchanged." : loaded.canManage ? "Select a medication to review or edit. Discontinued records are read-only." : "Current Medication Summary is read-only for this encounter or role.";
        } catch (error) { if (current === generation) { mustReload = true; message.textContent = error instanceof Error ? error.message : "Medications unavailable."; } }
        finally { if (current === generation) { busy = false; controls(); } }
    }
    for (const name of ["encounter-diagnoses-context", "encounter-diagnoses-saved", "encounter-contribution-saved"]) document.addEventListener(name, event => {
        const value = (event as CustomEvent<MedicationContext>).detail;
        const same = context?.patientUid === value.patientUid && context?.encounterUid === value.encounterUid;
        if (name !== "encounter-diagnoses-context" && !same) return;
        if (!same) mustReload = false;
        void load(value, same && dirty);
    });
    select.addEventListener("change", () => {
        if (dirty && !confirm("Discard unsaved medication changes?")) { select.value = selected?.medicationUid || ""; return; }
        clear(); const row = state?.items.find(x => x.medicationUid === select.value); if (row) display(row); controls();
    });
    add.addEventListener("click", () => {
        if (busy || !state?.canManage || mustReload || dirty && !confirm("Discard unsaved medication changes?")) return;
        clear(); select.value = ""; creating = dirty = true; controls(); inputs[0].focus();
    });
    for (const input of inputs) input.addEventListener("input", () => { dirty = true; controls(); });
    reload.addEventListener("click", () => { if (context && !busy && (!dirty || confirm("Reload medications and discard medication edits? Note text is preserved."))) void load(context, false); });
    async function persist(action: "Create" | "Update" | "Discontinue") {
        if (busy || mustReload || !state?.canManage || !context || action === "Discontinue" && (dirty || selected?.status !== "Active") || action !== "Create" && !selected) return;
        if (element<HTMLButtonElement>("saveEncounterNoteButton").disabled || ["encounterDiagnoses", "encounterContributions", "encounterProcedures"].some(id => element<HTMLElement>(id)?.dataset.busy === "true")) {
            message.textContent = "Wait for the current encounter operation to finish."; return;
        }
        if (action !== "Discontinue" && (!dirty || fields.disabled || !inputs.every(x => x.reportValidity()) || !inputs[0].value.trim())) return;
        if (action === "Discontinue" && !confirm("Discontinue this Medication Summary entry? Its history is retained and prescriptions are unchanged.")) return;
        const medication: Record<string, string | null> = {};
        keys.forEach((key, i) => medication[key[0].toLowerCase() + key.slice(1)] = inputs[i].value.trim() || null);
        const value = context, current = generation;
        const payload = { requestUid: crypto.randomUUID(), action, encounterRowVersion: state.rowVersion,
            medicationUid: action === "Create" ? null : selected!.medicationUid, medicationRowVersion: selected?.rowVersion,
            medication: action === "Discontinue" ? null : medication, discontinueReason: action === "Discontinue" ? reason.value.trim() || null : null };
        const csrf = document.querySelector<HTMLInputElement>('#encounterNoteAntiforgeryForm input[name="__RequestVerificationToken"]')!.value;
        busy = true; controls(); message.textContent = "Saving Medication Summary...";
        try {
            const row = await response<Medication>(await fetch(url(value), { method: "POST", headers: { "Content-Type": "application/json", RequestVerificationToken: csrf }, body: JSON.stringify(payload) }));
            if (current !== generation) return;
            if (row.patientUid !== value.patientUid || action !== "Create" && row.medicationUid !== payload.medicationUid) throw new Error("Medication context changed. Reload before retrying.");
            dirty = false;
            await load(value, false, row.medicationUid);
            if (context?.patientUid !== value.patientUid || context?.encounterUid !== value.encounterUid || mustReload) return;
            document.dispatchEvent(new CustomEvent("encounter-medication-saved", { detail: { ...value, medicationUid: row.medicationUid } }));
            message.focus();
        } catch (error) { if (current === generation) { mustReload = true; message.textContent = `${error instanceof Error ? error.message : "Save outcome uncertain."} Reload medications before another save; unsaved note text is preserved.`; } }
        finally { if (current === generation) { busy = false; controls(); } }
    }
    save.addEventListener("click", () => persist(creating ? "Create" : "Update"));
    discontinue.addEventListener("click", () => persist("Discontinue"));
    for (const id of ["saveEncounterNoteButton", "saveEncounterDiagnosesOnly", "saveEncounterDiagnosesCpp", "signEncounterButton"]) element<HTMLElement>(id)?.addEventListener("click", event => {
        if (busy || id === "signEncounterButton" && dirty) { event.preventDefault(); event.stopImmediatePropagation(); message.textContent = "Finish or discard medication changes before signing or starting another save."; }
    }, true);
    for (const id of ["encounterContributions", "encounterProcedures"]) element<HTMLElement>(id)?.addEventListener("click", event => {
        if (busy) { event.preventDefault(); event.stopImmediatePropagation(); }
    }, true);
    document.querySelector("#encounterDetailsModal")?.addEventListener("hide.bs.modal", event => { if (busy || dirty && !confirm("Discard unsaved medication changes?")) event.preventDefault(); });
    document.querySelector("#encounterDetailsModal")?.addEventListener("hidden.bs.modal", () => { generation++; context = state = undefined; busy = mustReload = false; clear(); controls(); });
    controls();
}
