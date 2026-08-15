using MicroEMR.Web.Models.Patients;

namespace MicroEMR.Web.Models.PatientEncounters;

public sealed class EncounterHistoryPrintViewModel
{
    public required PatientDetailsResponse Patient { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required IReadOnlyList<PatientEncounterListItemResponse> Encounters { get; init; }
    public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;

    public static EncounterHistoryPrintViewModel Create(
        PatientDetailsResponse patient,
        IEnumerable<PatientEncounterListItemResponse> encounters,
        DateOnly startDate,
        DateOnly endDate) => new()
        {
            Patient = patient,
            StartDate = startDate,
            EndDate = endDate,
            Encounters = encounters
                .Where(x =>
                {
                    var encounterDate = DateOnly.FromDateTime(x.EncounterDateUtc.ToLocalTime());
                    return encounterDate >= startDate && encounterDate <= endDate;
                })
                .OrderBy(x => x.EncounterDateUtc)
                .ThenBy(x => x.EncounterUid)
                .ToArray()
        };
}
