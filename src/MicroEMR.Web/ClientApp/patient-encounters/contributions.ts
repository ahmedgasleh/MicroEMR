interface ContributionRevision { revisionNumber: number; content: string; editorId: number; editorDisplayName: string; revisedAtUtc: string; }
interface Contribution { contributionUid: string; sectionKey: string; sectionTitle: string; originalContributorId: number; originalContributorDisplayName: string; createdAtUtc: string; content: string; revisions: ContributionRevision[]; }
interface ContributionEncounter { patientUid: string; encounterUid: string; rowVersion: string; status: string; encounterDateUtc: string; encounterType: string; signedAt?: string; signedByDisplayName?: string; notes?: string; subjectiveNote?: string; objectiveNote?: string; assessmentNote?: string; planNote?: string; contributions: Contribution[]; templateDefinition?: { sections: { key: string; title: string; order: number }[] }; }
const root = document.querySelector<HTMLElement>("#encounterContributions");
if (root) {
    const host = root.querySelector<HTMLElement>("[data-contribution-blocks]")!;
    const editor = root.querySelector<HTMLElement>("[data-contribution-editor]")!;
    const section = root.querySelector<HTMLSelectElement>("select")!;
    const content = root.querySelector<HTMLTextAreaElement>("textarea")!;
    const save = root.querySelector<HTMLButtonElement>("[data-contribution-save]")!;
    const cancel = root.querySelector<HTMLButtonElement>("[data-contribution-cancel]")!;
    const message = root.querySelector<HTMLElement>("[data-contribution-message]")!;
    let state: ContributionEncounter | undefined, editing: string | undefined, busy = false, generation = 0;
    const date = (value: string) => new Date(value).toLocaleString();
    const text = (tag: string, value: string): HTMLElement => { const node = document.createElement(tag); node.textContent = value; return node; };
    const reset = () => { editing = undefined; content.value = ""; section.disabled = false; save.textContent = "Add contribution"; };
    const render = (encounter: ContributionEncounter) => {
        state = encounter;
        host.replaceChildren();
        const editable = encounter.status.toLowerCase() === "open" && root.dataset.canEdit === "true";
        editor.hidden = !editable;
        section.replaceChildren();
        const sections = encounter.templateDefinition?.sections || [
            { key: "subjective", title: "Subjective", order: 0 }, { key: "objective", title: "Objective", order: 1 },
            { key: "assessment", title: "Assessment", order: 2 }, { key: "plan", title: "Plan", order: 3 }, { key: "notes", title: "Note", order: 4 }];
        [...sections].sort((a,b) => a.order-b.order).forEach(s => { const option = new Option(s.title, s.key); section.add(option); });
        for (const block of encounter.contributions || []) {
            const card = document.createElement("section"); card.className = "border rounded p-3 mb-2";
            card.append(text("h6", block.sectionTitle));
            const identity = text("p", `Original contributor: ${block.originalContributorDisplayName} (clinical user ${block.originalContributorId}) | ${date(block.createdAtUtc)}`);
            identity.className = "small text-muted"; card.append(identity);
            const body = text("p", block.content); body.style.whiteSpace = "pre-wrap"; card.append(body);
            const latest = block.revisions[block.revisions.length-1];
            if (latest?.revisionNumber > 1) card.append(text("p", `Last revised by ${latest.editorDisplayName} (clinical user ${latest.editorId}) | ${date(latest.revisedAtUtc)} | revision ${latest.revisionNumber}`));
            const history = document.createElement("details"); history.append(text("summary", "Contribution revisions"));
            for (const revision of block.revisions) {
                history.append(text("p", `Revision ${revision.revisionNumber}: ${revision.editorDisplayName} (clinical user ${revision.editorId}) | ${date(revision.revisedAtUtc)}`));
                const previous = text("p", revision.content); previous.style.whiteSpace = "pre-wrap"; history.append(previous);
            }
            card.append(history);
            if (editable) {
                const button = document.createElement("button"); button.type = "button"; button.className = "btn btn-outline-secondary btn-sm"; button.textContent = "Edit contribution";
                button.disabled = busy;
                button.addEventListener("click", () => { editing = block.contributionUid; section.value = block.sectionKey; section.disabled = true; content.value = block.content; save.textContent = "Save revision"; content.focus(); });
                card.append(button);
            }
            host.append(card);
        }
    };
    document.addEventListener("encounter-contributions-context", event => {
        generation++; reset(); message.textContent = ""; render((event as CustomEvent<ContributionEncounter>).detail);
    });
    document.addEventListener("encounter-diagnoses-saved", event => {
        const saved = (event as CustomEvent<ContributionEncounter>).detail;
        if (state?.encounterUid === saved.encounterUid && state?.patientUid === saved.patientUid) state.rowVersion = saved.rowVersion;
    });
    cancel.addEventListener("click", reset);
    save.addEventListener("click", async () => {
        if (busy || !state || !content.value.trim()) return;
        const current = state, currentGeneration = generation;
        busy = true; save.disabled = true; cancel.disabled = true; content.disabled = true;
        host.querySelectorAll<HTMLButtonElement>("button").forEach(b => b.disabled = true);
        try {
            const body = new FormData();
            body.append("patientUid", current.patientUid); body.append("encounterUid", current.encounterUid);
            body.append("sectionKey", section.value); body.append("content", content.value); body.append("rowVersion", current.rowVersion);
            if (editing) body.append("contributionUid", editing);
            body.append("__RequestVerificationToken", document.querySelector<HTMLInputElement>('#encounterNoteAntiforgeryForm input[name="__RequestVerificationToken"]')!.value);
            const response = await fetch(root.dataset.saveUrl!, { method: "POST", body });
            const result = await response.json();
            if (!response.ok || !result.success) throw new Error(result.message || "Contribution could not be saved.");
            const query = new URLSearchParams({ patientUid: current.patientUid, encounterUid: current.encounterUid });
            const refreshed = await fetch(`${root.dataset.detailsUrl}?${query}`);
            const details = await refreshed.json();
            if (!refreshed.ok || !details.success) throw new Error("Contribution saved, but refresh failed. Reopen the encounter before editing again.");
            if (currentGeneration !== generation) return;
            reset(); render(details.encounter); message.textContent = "Contribution saved.";
            document.dispatchEvent(new CustomEvent("encounter-contribution-saved", { detail: details.encounter }));
        } catch (error) {
            if (currentGeneration === generation) message.textContent = error instanceof Error ? error.message : "Contribution could not be saved.";
        } finally { busy = false; save.disabled = false; cancel.disabled = false; content.disabled = false; host.querySelectorAll<HTMLButtonElement>("button").forEach(b => b.disabled = false); }
    });
    root.querySelector("[data-contribution-print]")!.addEventListener("click", () => {
        if (!state) return;
        const print = window.open("", "_blank"); if (!print) return;
        print.document.title = "Encounter note";
        print.document.body.append(text("h1", "Encounter note"));
        print.document.body.append(text("p", `${root.dataset.patientName || ""} | Chart ${root.dataset.chartNumber || ""}`));
        print.document.body.append(text("p", `${state.encounterType} | ${date(state.encounterDateUtc)}`));
        if (state.signedAt) print.document.body.append(text("p", `Signed by ${state.signedByDisplayName || "Identity unavailable"} ? ${date(state.signedAt)}`));
        // Capture current displayed section values, including unsaved draft text; signed values are read-only.
        print.document.body.append(text("p", state.status.toLowerCase() === "signed" ? "Signed encounter" : "Draft preview - may include unsaved section text"));
        const template = document.querySelector<HTMLElement>("#encounterStructuredFields");
        if (state.templateDefinition && template) {
            for (const wrapper of Array.from(template.children)) {
                print.document.body.append(text("h2", wrapper.querySelector("h4")?.textContent || "Section"));
                print.document.body.append(text("p", "Earlier contribution identity unavailable"));
                wrapper.querySelectorAll<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>("input,textarea,select").forEach(input => {
                    const value = input instanceof HTMLInputElement && input.type === "checkbox" ? String(input.checked) : input.value;
                    const p = text("p", `${input.parentElement?.querySelector("label")?.textContent || ""}: ${value}`); p.style.whiteSpace = "pre-wrap"; print.document.body.append(p);
                });
                wrapper.querySelectorAll("p").forEach(p => print.document.body.append(p.cloneNode(true)));
            }
        } else {
            for (const name of ["Subjective", "Objective", "Assessment", "Plan"]) {
                const value = document.querySelector<HTMLTextAreaElement>(`#encounter${name}Note`)?.value;
                if (value) { print.document.body.append(text("h2", name), text("p", "Earlier contribution identity unavailable")); const p = text("p", value); p.style.whiteSpace = "pre-wrap"; print.document.body.append(p); }
            }
        }
        if (state.notes) { const p = text("p", state.notes); p.style.whiteSpace = "pre-wrap"; print.document.body.append(text("h2", "Legacy note"), text("p", "Earlier contribution identity unavailable"), p); }
        print.document.body.append(text("h2", "Contributions"));
        const blocks = host.cloneNode(true) as HTMLElement;
        blocks.querySelectorAll("button").forEach(b => b.remove());
        blocks.querySelectorAll("details").forEach(d => { d.open = true; });
        print.document.body.append(blocks);
        const amendments = document.querySelector<HTMLElement>("#encounterAddendumsContent");
        if (amendments) print.document.body.append(text("h2", "Addendums"), amendments.cloneNode(true));
        print.print();
    });
}
export {};
