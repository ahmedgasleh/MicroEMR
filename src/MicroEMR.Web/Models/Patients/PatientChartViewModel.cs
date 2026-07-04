using MicroEMR.Web.Models.PatientDocuments;
using MicroEMR.Web.Models.PatientEncounters;

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

    public string ActiveTab { get; set; } = "demographics";
}
