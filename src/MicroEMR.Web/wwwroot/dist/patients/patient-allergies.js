document.addEventListener("DOMContentLoaded", () => {
    document.querySelectorAll("[data-allergy-clinical-fields]").forEach(section => {
        const form = section.closest("form"), type = form.elements.namedItem("AllergenType");
        const applicability = form.elements.namedItem("ClinicalFields.DrugApplicability");
        const din = form.elements.namedItem("ClinicalFields.DrugDIN");
        const update = () => section.querySelector("[data-allergy-din]").classList.toggle("d-none", !(applicability?.value === "Drug" || (!applicability?.value && type.value === "Medication")) && !din.value.trim());
        applicability?.addEventListener("change", update);
        type.addEventListener("change", update);
        din.addEventListener("input", update);
        form.addEventListener("reset", () => queueMicrotask(update));
        update();
    });
    const element = document.querySelector("#resolveAllergyModal");
    if (!element)
        return;
    const modal = bootstrap.Modal.getOrCreateInstance(element), reason = document.querySelector("#resolveAllergyReason"), message = document.querySelector("#resolveAllergyMessage"), confirm = document.querySelector("#confirmResolveAllergyButton");
    let patient = "", allergy = "", version = "";
    document.querySelectorAll(".resolve-allergy-button").forEach(button => button.addEventListener("click", () => {
        patient = button.dataset.patientUid;
        allergy = button.dataset.allergyUid;
        version = button.dataset.rowVersion;
        reason.value = "";
        message.classList.add("d-none");
        modal.show();
    }));
    confirm.addEventListener("click", async () => {
        if (!patient || !allergy || !version)
            return;
        confirm.disabled = true;
        try {
            const body = new FormData();
            body.append("PatientUid", patient);
            body.append("AllergyUid", allergy);
            body.append("RowVersion", version);
            body.append("ResolveReason", reason.value);
            const token = document.querySelector('#allergyAntiforgeryForm input[name="__RequestVerificationToken"]').value;
            const response = await fetch(element.dataset.url, { method: "POST", headers: { RequestVerificationToken: token }, body });
            const result = await response.json();
            if (!response.ok || !result.success) {
                message.textContent = result.message || "Allergy could not be resolved. Reload and review.";
                message.classList.remove("d-none");
                return;
            }
            window.location.reload();
        }
        catch {
            message.textContent = "Allergy could not be resolved. Reload and review.";
            message.classList.remove("d-none");
        }
        finally {
            confirm.disabled = false;
        }
    });
});
export {};
//# sourceMappingURL=patient-allergies.js.map