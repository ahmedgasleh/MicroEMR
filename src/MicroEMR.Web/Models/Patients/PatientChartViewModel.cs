using MicroEMR.Web.Models.PatientDocuments;
using MicroEMR.Web.Models.PatientEncounters;
using MicroEMR.Web.Models.PatientAllergies;
using MicroEMR.Web.Models.PatientMedications;

namespace MicroEMR.Web.Models.Patients;

public sealed class PatientChartViewModel
{
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

    public string ActiveTab { get; set; } = "demographics";
}
