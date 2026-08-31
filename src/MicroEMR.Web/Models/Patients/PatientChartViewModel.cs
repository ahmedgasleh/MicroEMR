using MicroEMR.Web.Models.PatientDocuments;
using MicroEMR.Web.Models.PatientEncounters;
using MicroEMR.Web.Models.PatientAllergies;
using MicroEMR.Web.Models.PatientMedications;
using MicroEMR.Web.Models.PatientProblems;
using MicroEMR.Web.Models.PatientVitals;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Application.PatientCpp;

namespace MicroEMR.Web.Models.Patients;

public sealed class PatientChartViewModel
{
    public PatientCppSummaryResponse Cpp { get; set; } = null!;
    public PatientChartSummaryViewModel Summary { get; set; } = new();
    public PatientTimelineViewModel Timeline { get; set; } = new();
    public PatientDetailsResponse Patient { get; set; } = new();

    public IReadOnlyList<PatientDocumentListItemResponse> Documents
        { get; set; } =
        Array.Empty<PatientDocumentListItemResponse>();

    public IReadOnlyList<DocumentTemplateListItemResponse> DocumentTemplates
        { get; set; } = Array.Empty<DocumentTemplateListItemResponse>();

    public IReadOnlyList<PatientEncounterListItemResponse> Encounters
        { get; set; } =
        Array.Empty<PatientEncounterListItemResponse>();

    public IReadOnlyList<PatientAllergyListItemResponse> Allergies
        { get; set; } =
        Array.Empty<PatientAllergyListItemResponse>();
    public AllergyDocumentationStateResponse? AllergyDocumentationState { get; set; }
    public bool CanManageClinicalData { get; set; }

    public IReadOnlyList<PatientMedicationListItemResponse> Medications
        { get; set; } =
        Array.Empty<PatientMedicationListItemResponse>();
    public IReadOnlyList<PatientPrescriptionResponse> Prescriptions { get; set; }=[];
    public bool CanPrescribe { get; set; }

    public IReadOnlyList<PatientProblemViewModel> Problems { get; set; } =
        Array.Empty<PatientProblemViewModel>();

    public IReadOnlyList<PatientVitalViewModel> Vitals { get; set; } =
        Array.Empty<PatientVitalViewModel>();

    public string ActiveTab { get; set; } = "summary";
}
