interface CppPrintClinician { clinicianUid: string; displayName: string; providerType: string; }
interface CppPrintOptions { clinicians: CppPrintClinician[]; }
const cppPrintRoot = document.querySelector<HTMLDetailsElement>("#cppPrint");
const cppPrintForm = document.querySelector<HTMLFormElement>("#cppPrintForm");
if (cppPrintRoot && cppPrintForm) {
    const clinician = document.querySelector<HTMLSelectElement>("#cppPrintClinician")!;
    const message = document.querySelector<HTMLElement>("#cppPrintMessage")!;
    const button = document.querySelector<HTMLButtonElement>("#cppPrintButton")!;
    let ready = false;
    let loading = false;
    cppPrintRoot.addEventListener("toggle", async () => {
        if (!cppPrintRoot.open || ready || loading) return;
        loading = true;
        message.textContent = "Loading clinicians...";
        try {
            const response = await fetch(cppPrintForm.dataset.optionsUrl!, { cache: "no-store" });
            if (!response.ok) throw new Error("CPP print options could not be loaded. Close and reopen Print CPP to retry.");
            const options = await response.json() as CppPrintOptions;
            options.clinicians.forEach(row => {
                const option = document.createElement("option");
                option.value = row.clinicianUid;
                option.textContent = `${row.displayName} (${row.providerType})`;
                clinician.append(option);
            });
            ready = options.clinicians.length > 0;
            button.disabled = !ready;
            message.textContent = ready ? "The printable PDF opens in a new tab. Use its Print command." : "No active clinician is available. Configure a clinician before printing.";
        } catch (error) { message.textContent = error instanceof Error ? error.message : "CPP print options are unavailable."; }
        finally { loading = false; }
    });
    cppPrintForm.addEventListener("submit", event => {
        if (!ready || !clinician.value || cppPrintForm.querySelectorAll<HTMLInputElement>('input[name="Categories"]:checked').length === 0) {
            event.preventDefault();
            message.textContent = "Select a clinician and at least one CPP category.";
        }
    });
}
export {};
