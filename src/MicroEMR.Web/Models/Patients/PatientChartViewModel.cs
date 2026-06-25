using MicroEMR.Web.Models.PatientDocuments;

namespace MicroEMR.Web.Models.Patients;

public sealed class PatientChartViewModel
{
    public PatientDetailsResponse Patient { get; set; } = new();

    public IReadOnlyList<PatientDocumentListItemResponse> Documents
        { get; set; } =
        Array.Empty<PatientDocumentListItemResponse>();

    public string ActiveTab { get; set; } = "demographics";
}