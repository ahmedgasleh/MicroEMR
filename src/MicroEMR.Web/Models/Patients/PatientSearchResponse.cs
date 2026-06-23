namespace MicroEMR.Web.Models.Patients;

public sealed class PatientSearchResponse
{
    public IReadOnlyList<PatientListItem> Items { get; set; } =
        Array.Empty<PatientListItem>();

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 25;

    public int TotalRows { get; set; }

    public int TotalPages =>
        PageSize <= 0
            ? 0
            : (int)Math.Ceiling(
                TotalRows / (double)PageSize);
}
