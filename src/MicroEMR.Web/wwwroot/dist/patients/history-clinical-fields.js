export function partialDate(value) {
    if (value?.year == null)
        return "Not recorded";
    return String(value.year).padStart(4, "0") + (value.month == null ? "" : `-${String(value.month).padStart(2, "0")}`) + (value.day == null ? "" : `-${String(value.day).padStart(2, "0")}`);
}
export function clinicalItems(kind, fields) {
    const f = fields ?? {};
    return [...(kind === "Medical" ? [
            ["OnsetDate", "Medical onset", partialDate(f.onsetDate)],
            ["ResolutionDate", "Clinical resolution", partialDate(f.resolutionDate)],
            ["LifeStage", "Life stage", f.lifeStage || "Not recorded"],
            ["ClinicalStatus", "Clinical status", f.clinicalStatus || "Not recorded"]
        ] : [["ProcedureDate", "Procedure date", partialDate(f.procedureDate)]]), ["Notes", "Clinical notes", f.notes || "Not recorded"]];
}
//# sourceMappingURL=history-clinical-fields.js.map