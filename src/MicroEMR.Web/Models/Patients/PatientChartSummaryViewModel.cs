using MicroEMR.Web.Models.PatientAllergies;
using MicroEMR.Web.Models.PatientDocuments;
using MicroEMR.Web.Models.PatientEncounters;
using MicroEMR.Web.Models.PatientMedications;
using MicroEMR.Web.Models.PatientProblems;
using MicroEMR.Web.Models.PatientVitals;

namespace MicroEMR.Web.Models.Patients;

public sealed class PatientChartSummaryViewModel
{
    public Guid PatientUid { get; set; }
    public string PatientDisplayName { get; set; } = string.Empty;
    public string? PreferredName { get; set; }
    public string ChartNumber { get; set; } = string.Empty;
    public string? HealthCardNumber { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public string AgeDisplay { get; set; } = string.Empty;
    public string? SexAtBirth { get; set; }
    public string? GenderIdentity { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public IReadOnlyList<PatientProblemViewModel> ActiveProblems { get; set; } = [];
    public int ActiveProblemsTotalCount { get; set; }
    public IReadOnlyList<PatientAllergyListItemResponse> ActiveAllergies { get; set; } = [];
    public int ActiveAllergiesTotalCount { get; set; }
    public IReadOnlyList<PatientMedicationListItemResponse> ActiveMedications { get; set; } = [];
    public int ActiveMedicationsTotalCount { get; set; }
    public PatientVitalViewModel? LatestVitals { get; set; }
    public IReadOnlyList<PatientEncounterListItemResponse> RecentEncounters { get; set; } = [];
    public int RecentEncountersTotalCount { get; set; }
    public IReadOnlyList<PatientDocumentListItemResponse> RecentDocuments { get; set; } = [];
    public int RecentDocumentsTotalCount { get; set; }
}
