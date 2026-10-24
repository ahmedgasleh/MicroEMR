const root = document.querySelector("#encounterContributions");
if (root) {
    const host = root.querySelector("[data-contribution-blocks]");
    const editor = root.querySelector("[data-contribution-editor]");
    const section = root.querySelector("select");
    const content = root.querySelector("textarea");
    const save = root.querySelector("[data-contribution-save]");
    const cancel = root.querySelector("[data-contribution-cancel]");
    const message = root.querySelector("[data-contribution-message]");
    let state, editing, busy = false, generation = 0;
    const fieldDrafts = new Map();
    let refreshRequired = false;
    const date = (value) => new Date(value).toLocaleString();
    const text = (tag, value) => { const node = document.createElement(tag); node.textContent = value; return node; };
    const reset = () => { editing = undefined; content.value = ""; section.disabled = false; save.textContent = "Add contribution"; };
    const render = (encounter) => {
        state = encounter;
        document.querySelectorAll("[data-contribution-card], [data-template-note-editor]").forEach(card => card.remove());
        host.replaceChildren();
        const editable = encounter.status.toLowerCase() === "open" && root.dataset.canEdit === "true";
        editor.hidden = !editable;
        const selectedSection = section.value;
        section.replaceChildren();
        const sections = encounter.templateDefinition?.sections || [
            { key: "subjective", title: "Subjective", order: 0 }, { key: "objective", title: "Objective", order: 1 },
            { key: "assessment", title: "Assessment", order: 2 }, { key: "plan", title: "Plan", order: 3 }, { key: "notes", title: "Note", order: 4 }
        ];
        [...sections].sort((a, b) => a.order - b.order).forEach(s => {
            section.add(new Option(s.title, s.key));
            for (const field of s.fields || [])
                if (field.type === "Text" || field.type === "TextArea") {
                    const option = new Option(`${s.title} / ${field.label}${field.required ? " (required field)" : ""}`, s.key + "/" + field.key);
                    option.dataset.sectionKey = s.key;
                    option.dataset.fieldKey = field.key;
                    section.add(option);
                }
        });
        if (Array.from(section.options).some(option => option.value === selectedSection))
            section.value = selectedSection;
        for (const container of encounter.templateDefinition?.sections || []) {
            for (const field of container.fields || []) {
                if (!editable || (field.type !== "Text" && field.type !== "TextArea"))
                    continue;
                const baseline = Array.from(document.querySelectorAll("#encounterStructuredFields [data-field-key]"))
                    .find(input => input.dataset.fieldKey === field.key);
                if (!baseline)
                    continue;
                const draftKey = JSON.stringify([container.key, field.key]);
                const hasSavedText = !!baseline.value.trim() || encounter.contributions.some(block => block.sectionKey === container.key && block.fieldKey === field.key && block.valueKind === "Narrative");
                if (hasSavedText && !fieldDrafts.get(draftKey)?.trim())
                    continue;
                const fieldEditor = document.createElement("div");
                fieldEditor.dataset.templateNoteEditor = "true";
                fieldEditor.className = "mb-3";
                const input = document.createElement("textarea");
                input.className = "form-control mb-2";
                input.rows = 3;
                input.setAttribute("aria-label", field.label + (field.required ? " (required)" : ""));
                input.placeholder = `Enter ${field.label}`;
                input.value = fieldDrafts.get(draftKey) || "";
                input.addEventListener("input", () => fieldDrafts.set(draftKey, input.value));
                const fieldSave = document.createElement("button");
                fieldSave.type = "button";
                fieldSave.className = "btn btn-primary btn-sm";
                fieldSave.textContent = `Save ${field.label}`;
                const status = text("p", "");
                status.className = "small mt-2 mb-0";
                status.setAttribute("role", "status");
                fieldSave.addEventListener("click", async () => {
                    if (!input.value.trim()) {
                        status.textContent = `Enter ${field.label} before saving.`;
                        input.focus();
                        return;
                    }
                    await persistContribution(input.value, container.key, field.key, undefined, () => fieldDrafts.delete(draftKey), status);
                });
                fieldEditor.append(input, fieldSave, status);
                baseline.parentElement.append(fieldEditor);
            }
        }
        for (const block of encounter.contributions || []) {
            const card = document.createElement("section");
            card.className = "border rounded p-3 mb-2";
            card.dataset.contributionCard = "true";
            card.append(text("h6", block.fieldLabel || block.sectionTitle));
            const identity = text("p", `${block.valueKind === "Value" ? "Original contributor of tracked value" : "Original contributor"}: ${block.originalContributorDisplayName} (clinical user ${block.originalContributorId}) | ${date(block.createdAtUtc)}`);
            identity.className = "small text-muted";
            card.append(identity);
            const body = text("p", block.valueKind === "Value" ? "" : block.content);
            body.style.whiteSpace = "pre-wrap";
            card.append(body);
            const latest = block.revisions[block.revisions.length - 1];
            if (latest?.revisionNumber > 1)
                card.append(text("p", `Last revised by ${latest.editorDisplayName} (clinical user ${latest.editorId}) | ${date(latest.revisedAtUtc)} | revision ${latest.revisionNumber}`));
            const history = document.createElement("details");
            history.append(text("summary", "Contribution revisions"));
            if (block.priorValueJson !== undefined && block.priorValueJson !== null)
                history.append(text("p", `Prior value (identity unavailable): ${block.priorValueJson}`));
            for (const revision of block.revisions) {
                history.append(text("p", `Revision ${revision.revisionNumber}: ${revision.editorDisplayName} (clinical user ${revision.editorId}) | ${date(revision.revisedAtUtc)}`));
                const previous = text("p", revision.content);
                previous.style.whiteSpace = "pre-wrap";
                history.append(previous);
            }
            card.append(history);
            if (editable && block.valueKind !== "Value") {
                const button = document.createElement("button");
                button.type = "button";
                button.className = "btn btn-outline-secondary btn-sm";
                button.textContent = "Edit contribution";
                button.disabled = busy;
                button.addEventListener("click", () => { editing = block.contributionUid; section.value = block.fieldKey ? block.sectionKey + "/" + block.fieldKey : block.sectionKey; section.disabled = true; content.value = block.content; save.textContent = "Save revision"; content.focus(); });
                card.append(button);
            }
            const fieldInput = Array.from(document.querySelectorAll("#encounterStructuredFields [data-field-key]"))
                .find(input => input.dataset.fieldKey === block.fieldKey);
            (fieldInput?.parentElement || host).append(card);
        }
    };
    document.addEventListener("encounter-contributions-context", event => {
        const encounter = event.detail;
        if (state?.patientUid !== encounter.patientUid || state?.encounterUid !== encounter.encounterUid)
            fieldDrafts.clear();
        generation++;
        refreshRequired = false;
        reset();
        message.textContent = "";
        render(encounter);
    });
    document.addEventListener("encounter-fields-rendered", () => { if (state)
        render(state); });
    document.addEventListener("encounter-diagnoses-saved", event => {
        const saved = event.detail;
        if (state?.encounterUid === saved.encounterUid && state?.patientUid === saved.patientUid)
            state.rowVersion = saved.rowVersion;
    });
    cancel.addEventListener("click", reset);
    const persistContribution = async (value, sectionKey, fieldKey, contributionUid, onSaved, status = message) => {
        if (busy || !state || !value.trim())
            return;
        if (refreshRequired) {
            status.textContent = "Reopen the encounter to refresh saved content before editing again.";
            return;
        }
        const current = state, currentGeneration = generation;
        busy = true;
        root.dataset.busy = "true";
        save.disabled = true;
        cancel.disabled = true;
        content.disabled = true;
        document.querySelectorAll("[data-contribution-card] button, [data-template-note-editor] button, [data-template-note-editor] textarea").forEach(control => control.disabled = true);
        try {
            const body = new FormData();
            body.append("patientUid", current.patientUid);
            body.append("encounterUid", current.encounterUid);
            body.append("sectionKey", sectionKey);
            if (fieldKey)
                body.append("fieldKey", fieldKey);
            body.append("content", value);
            body.append("rowVersion", current.rowVersion);
            if (contributionUid)
                body.append("contributionUid", contributionUid);
            body.append("__RequestVerificationToken", document.querySelector('#encounterNoteAntiforgeryForm input[name="__RequestVerificationToken"]').value);
            const response = await fetch(root.dataset.saveUrl, { method: "POST", body });
            const result = await response.json();
            if (!response.ok || !result.success)
                throw new Error(result.message || "Contribution could not be saved.");
            if (currentGeneration !== generation)
                return;
            refreshRequired = true;
            onSaved();
            const query = new URLSearchParams({ patientUid: current.patientUid, encounterUid: current.encounterUid });
            const refreshed = await fetch(`${root.dataset.detailsUrl}?${query}`);
            const details = await refreshed.json();
            if (!refreshed.ok || !details.success)
                throw new Error("Contribution saved, but refresh failed. Reopen the encounter before editing again.");
            if (currentGeneration !== generation)
                return;
            refreshRequired = false;
            render(details.encounter);
            message.textContent = fieldKey ? "Template field saved." : "Contribution saved.";
            document.dispatchEvent(new CustomEvent("encounter-contribution-saved", { detail: details.encounter }));
        }
        catch (error) {
            if (currentGeneration === generation)
                status.textContent = error instanceof Error ? error.message : "Contribution could not be saved.";
        }
        finally {
            busy = false;
            root.dataset.busy = "false";
            save.disabled = false;
            cancel.disabled = false;
            content.disabled = false;
            document.querySelectorAll("[data-contribution-card] button, [data-template-note-editor] button, [data-template-note-editor] textarea").forEach(control => control.disabled = false);
        }
    };
    save.addEventListener("click", async () => {
        const option = section.selectedOptions[0];
        if (!option)
            return;
        await persistContribution(content.value, option.dataset.sectionKey || section.value, option.dataset.fieldKey, editing, reset);
    });
    root.querySelector("[data-contribution-print]").addEventListener("click", () => {
        if (!state)
            return;
        const print = window.open("", "_blank");
        if (!print)
            return;
        print.document.title = "Encounter note";
        print.document.body.append(text("h1", "Encounter note"));
        print.document.body.append(text("p", `${root.dataset.patientName || ""} | Chart ${root.dataset.chartNumber || ""}`));
        print.document.body.append(text("p", `${state.encounterType} | ${date(state.encounterDateUtc)}`));
        if (state.signedAt)
            print.document.body.append(text("p", `Signed by ${state.signedByDisplayName || "Identity unavailable"} ? ${date(state.signedAt)}`));
        // Print persisted values only: unsaved controls never inherit a saved identity.
        print.document.body.append(text("p", state.status.toLowerCase() === "signed" ? "Signed encounter" : "Saved draft - unsaved field changes are not printed"));
        const appendBlock = (block) => {
            print.document.body.append(text("p", `${block.valueKind === "Value" ? "Original contributor of tracked value" : "Original contributor"}: ${block.originalContributorDisplayName} (clinical user ${block.originalContributorId}) | ${date(block.createdAtUtc)}`));
            const latest = block.revisions[block.revisions.length - 1];
            if (latest?.revisionNumber > 1)
                print.document.body.append(text("p", `Revision ${latest.revisionNumber}: ${latest.editorDisplayName} (clinical user ${latest.editorId}) | ${date(latest.revisedAtUtc)}`));
            if (block.valueKind !== "Value") {
                const p = text("p", block.content);
                p.style.whiteSpace = "pre-wrap";
                print.document.body.append(p);
            }
        };
        if (state.templateDefinition) {
            const values = JSON.parse(state.structuredDataJson || '{"values":{}}').values;
            for (const section of state.templateDefinition.sections) {
                print.document.body.append(text("h2", section.title));
                for (const field of section.fields || []) {
                    if (field.type === "StaticText") {
                        print.document.body.append(text("p", field.content || ""));
                        continue;
                    }
                    const p = text("p", `${field.label}: ${field.type === "Number" ? state.templateFieldValues?.[field.key] ?? values[field.key] ?? "" : values[field.key] ?? ""}`);
                    p.style.whiteSpace = "pre-wrap";
                    print.document.body.append(p);
                    const linked = state.contributions.filter(c => c.fieldKey === field.key);
                    if (!linked.some(c => c.valueKind === "Value"))
                        print.document.body.append(text("p", "Earlier/template contribution identity unavailable"));
                    linked.forEach(appendBlock);
                }
                state.contributions.filter(c => c.sectionKey === section.key && !c.fieldKey).forEach(appendBlock);
            }
        }
        else {
            for (const [title, value] of [["Subjective", state.subjectiveNote], ["Objective", state.objectiveNote], ["Assessment", state.assessmentNote], ["Plan", state.planNote], ["Legacy note", state.notes]]) {
                if (value) {
                    const p = text("p", value);
                    p.style.whiteSpace = "pre-wrap";
                    print.document.body.append(text("h2", title), text("p", "Earlier contribution identity unavailable"), p);
                }
            }
            for (const block of state.contributions) {
                print.document.body.append(text("h2", block.sectionTitle));
                appendBlock(block);
            }
        }
        const amendments = document.querySelector("#encounterAddendumsContent");
        if (amendments)
            print.document.body.append(text("h2", "Addendums"), amendments.cloneNode(true));
        print.print();
    });
}
export {};
//# sourceMappingURL=contributions.js.map