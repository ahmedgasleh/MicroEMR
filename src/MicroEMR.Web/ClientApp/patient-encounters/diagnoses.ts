interface Diagnosis { diagnosisUid?: string | null; name: string; description?: string | null; onsetDate?: string | null; patientProblemUid?: string | null; }
interface DiagnosisState { patientUid: string; encounterUid: string; rowVersion: string; status: string; diagnoses: Diagnosis[]; canEdit: boolean; canSaveToCpp: boolean; }
interface EncounterContext { patientUid: string; encounterUid: string; status: string; }
const root = document.querySelector<HTMLElement>("#encounterDiagnoses");
if (root) {
    const rows = document.querySelector<HTMLElement>("#encounterDiagnosisRows")!;
    const message = document.querySelector<HTMLElement>("#encounterDiagnosisMessage")!;
    const add = document.querySelector<HTMLButtonElement>("#addEncounterDiagnosis")!;
    const only = document.querySelector<HTMLButtonElement>("#saveEncounterDiagnosesOnly")!;
    const cpp = document.querySelector<HTMLButtonElement>("#saveEncounterDiagnosesCpp")!;
    let state: DiagnosisState | undefined;
    let context: EncounterContext | undefined;
    let generation = 0, busy = false, dirty = false;
    const controls = () => {
        add.disabled = busy || !state?.canEdit;
        only.disabled = busy || !state?.canEdit;
        cpp.disabled = busy || !state?.canSaveToCpp;
        rows.querySelectorAll<HTMLInputElement | HTMLTextAreaElement>("input,textarea").forEach(x => x.disabled = busy || !state?.canEdit);
        rows.querySelectorAll<HTMLButtonElement>("button").forEach(x => x.disabled = busy || !state?.canEdit);
    };
    const collect = (): Diagnosis[] => Array.from(rows.children).map(row => {
        const block = row as HTMLElement;
        return { diagnosisUid: block.dataset.uid || null,
            name: block.querySelector<HTMLInputElement>('[data-field="name"]')!.value.trim(),
            description: block.querySelector<HTMLTextAreaElement>('[data-field="description"]')!.value.trim() || null,
            onsetDate: block.querySelector<HTMLInputElement>('[data-field="onsetDate"]')!.value || null };
    });
    const render = (items: Diagnosis[]) => {
        rows.replaceChildren();
        items.forEach((item,index) => {
            const block = document.createElement("div"); block.className = "border rounded p-2 mb-2"; block.dataset.uid = item.diagnosisUid || "";
            const field = (key: string, labelText: string, multiline = false) => {
                const label = document.createElement("label"); label.className = "form-label small"; label.textContent = labelText;
                const input = multiline ? document.createElement("textarea") : document.createElement("input");
                input.id = `encounter-diagnosis-${index}-${key}`; label.htmlFor = input.id;
                input.className = "form-control form-control-sm mb-2"; input.dataset.field = key;
                if (input instanceof HTMLInputElement) input.type = key === "onsetDate" ? "date" : "text";
                if (key === "name") { input.maxLength = 200; input.required = true; }
                if (key === "description") input.maxLength = 1000;
                input.value = key === "onsetDate" ? (item.onsetDate || "").slice(0,10) : (key === "name" ? item.name : item.description || "");
                input.addEventListener("input", () => dirty = true);
                block.append(label,input);
            };
            field("name","Diagnosis"); field("description","Details",true); field("onsetDate","Onset date (optional)");
            if (item.patientProblemUid) { const linked = document.createElement("p"); linked.className = "small text-body-secondary"; linked.textContent = "Linked to CPP. Editing or removing this encounter entry does not edit or remove its CPP Problem."; block.append(linked); }
            const remove = document.createElement("button"); remove.type = "button"; remove.className = "btn btn-outline-secondary btn-sm"; remove.textContent = "Remove from encounter";
            remove.addEventListener("click", () => { block.remove(); dirty = true; }); block.append(remove); rows.append(block);
        });
        controls();
    };
    const url = (value: EncounterContext) => `${root.dataset.url}?patientUid=${encodeURIComponent(value.patientUid)}&encounterUid=${encodeURIComponent(value.encounterUid)}`;
    const read = async (response: Response): Promise<DiagnosisState> => {
        const result = await response.json().catch(() => ({}));
        if (!response.ok) throw new Error(result.message || result.detail || "Diagnoses could not be saved or loaded. Reload the encounter.");
        return result as DiagnosisState;
    };
    document.addEventListener("encounter-diagnoses-context", async event => {
        const value = (event as CustomEvent<EncounterContext>).detail;
        const keep = context?.encounterUid === value.encounterUid && context?.patientUid === value.patientUid && dirty;
        const drafts = keep ? collect() : null;
        context = value; const current = ++generation; busy = true; state = undefined; controls();
        if (!keep) { dirty = false; rows.replaceChildren(); }
        message.textContent = "Loading diagnoses...";
        try {
            const loaded = await read(await fetch(url(value), { cache: "no-store" }));
            if (current !== generation) return;
            if (loaded.patientUid !== value.patientUid || loaded.encounterUid !== value.encounterUid) throw new Error("Encounter context changed. Reload before saving.");
            state = loaded;
            render(drafts && state.canEdit ? drafts : state.diagnoses);
            if (!state.canEdit) dirty = false;
            message.textContent = keep && state.canEdit ? "Unsaved diagnosis changes retained. Choose a diagnosis save destination." : state.canEdit ? "Encounter Only keeps CPP unchanged. Encounter and CPP includes the entire current diagnosis list." : "Diagnoses are read-only for this encounter or role.";
        } catch (error) { if (current === generation) message.textContent = error instanceof Error ? error.message : "Diagnoses unavailable."; }
        finally { if (current === generation) { busy = false; controls(); } }
    });
    add.addEventListener("click", () => {
        const items = collect(); if (items.length >= 50) { message.textContent = "Provide up to 50 diagnoses."; return; }
        items.push({name:""}); dirty = true; render(items);
    });
    const save = async (saveToCpp: boolean) => {
        if (busy || !state?.canEdit || saveToCpp && !state.canSaveToCpp || !context) return;
        if (document.querySelector<HTMLButtonElement>("#saveEncounterNoteButton")?.disabled) {
            message.textContent = "Wait for the note save to finish before saving diagnoses."; return;
        }
        const diagnoses = collect(); const names = diagnoses.map(x => x.name.toLocaleLowerCase());
        if (diagnoses.some(x => !x.name || x.name.length > 200 || (x.description?.length || 0) > 1000) || new Set(names).size !== names.length) {
            message.textContent = "Enter a distinct name for each diagnosis and review field lengths."; return;
        }
        const token = document.querySelector<HTMLInputElement>('#encounterNoteAntiforgeryForm input[name="__RequestVerificationToken"]')!.value;
        const current = generation; const savedContext = context;
        busy = true; controls(); message.textContent = "Saving diagnoses...";
        try {
            const result = await read(await fetch(url(savedContext), { method: "POST", headers: {"Content-Type":"application/json","RequestVerificationToken":token},
                body: JSON.stringify({rowVersion:state.rowVersion,saveToCpp,diagnoses}) }));
            if (current !== generation) return;
            state = result; dirty = false; render(result.diagnoses);
            document.dispatchEvent(new CustomEvent("encounter-diagnoses-saved", { detail: result }));
            message.textContent = saveToCpp ? "Diagnoses saved to encounter and CPP. Reopen the chart summary to refresh CPP." : "Diagnoses saved to encounter only. CPP unchanged.";
        } catch (error) { if (current === generation) message.textContent = error instanceof Error ? error.message : "Diagnosis save failed."; }
        finally { if (current === generation) { busy = false; controls(); } }
    };
    only.addEventListener("click", () => save(false)); cpp.addEventListener("click", () => save(true));
    document.querySelector("#saveEncounterNoteButton")?.addEventListener("click", event => {
        if (busy) { event.preventDefault(); event.stopImmediatePropagation(); message.textContent = "Wait for the diagnosis operation to finish before saving note text."; }
    },true);
    document.querySelector("#signEncounterButton")?.addEventListener("click", event => {
        if (dirty || busy) { event.preventDefault(); event.stopImmediatePropagation(); message.textContent = "Save diagnoses before signing the encounter."; }
    },true);
    document.querySelector("#encounterDetailsModal")?.addEventListener("hidden.bs.modal", () => {
        generation++; state = undefined; context = undefined; busy = false; dirty = false; rows.replaceChildren(); controls();
    });
}
export {};
