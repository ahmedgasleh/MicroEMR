using MicroEMR.Web.Models.PatientDocuments;
using MicroEMR.Web.Models.PatientEncounters;
using MicroEMR.Web.Models.PatientAllergies;
using MicroEMR.Web.Models.PatientMedications;
using MicroEMR.Web.Models.PatientProblems;
using MicroEMR.Web.Models.PatientVitals;

namespace MicroEMR.Web.Models.Patients;

public sealed class PatientChartViewModel
{
    public PatientChartSummaryViewModel Summary { get; set; } = new();
    public PatientDetailsResponse Patient { get; set; } = new();

    public IReadOnlyList<PatientDocumentListItemResponse> Documents
        { get; set; } =
        Array.Empty<PatientDocumentListItemResponse>();

    public IReadOnlyList<PatientEncounterListItemResponse> Encounters
        { get; set; } =
        Array.Empty<PatientEncounterListItemResponse>();

    public IReadOnlyList<PatientAllergyListItemResponse> Allergies
        { get; set; } =
        Array.Empty<PatientAllergyListItemResponse>();

    public IReadOnlyList<PatientMedicationListItemResponse> Medications
        { get; set; } =
        Array.Empty<PatientMedicationListItemResponse>();

    public IReadOnlyList<PatientProblemViewModel> Problems { get; set; } =
        Array.Empty<PatientProblemViewModel>();

    public IReadOnlyList<PatientVitalViewModel> Vitals { get; set; } =
        Array.Empty<PatientVitalViewModel>();

    public string ActiveTab { get; set; } = "summary";
}
