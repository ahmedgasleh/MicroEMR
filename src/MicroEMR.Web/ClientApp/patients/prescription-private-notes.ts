type PrivateEditor = { noteText: string | null; canEdit: boolean; rowVersion: string };

export function resetPrivateNoteEditor(form: HTMLFormElement): void {
    const input = form.querySelector<HTMLTextAreaElement>('[name="PrivateNote"]');
    if (!input) return;
    input.value = "";
    input.disabled = false;
    input.closest<HTMLElement>("[data-private-note-editor]")!.hidden = false;
}

export async function loadPrivateNoteEditor(form: HTMLFormElement, url: string): Promise<void> {
    const input = form.querySelector<HTMLTextAreaElement>('[name="PrivateNote"]');
    if (!input) return; // No private field is rendered for an unentitled reader.
    input.value = "";
    input.disabled = true;
    const container = input.closest<HTMLElement>("[data-private-note-editor]")!;
    container.hidden = true;
    const response = await fetch(url, { headers: { Accept: "application/json" }, cache: "no-store" });
    if (!response.ok) throw new Error("The private note could not be loaded. Reload the chart before editing.");
    const note = await response.json() as PrivateEditor;
    if (!note.canEdit) return;
    // The protected read is later than ordinary draft loading: review one consistent token.
    // A changed token means public fields may already be stale; require a fresh load.
    const version = form.querySelector<HTMLInputElement>('[name="RowVersion"]')!;
    if (version.value !== note.rowVersion) throw new Error("The prescription changed. Reload the chart and try again.");
    input.value = note.noteText ?? "";
    input.disabled = false;
    container.hidden = false;
}
