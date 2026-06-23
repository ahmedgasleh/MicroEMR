namespace MicroEMR.Api.Contracts.Patients;

public sealed class PatientSearchResponse
{
    public IReadOnlyList<PatientListItemResponse> Items { get; set; } =
        Array.Empty<PatientListItemResponse>();

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public int TotalRows { get; set; }

    public int TotalPages =>
        PageSize <= 0
            ? 0
            : (int)Math.Ceiling(TotalRows / (double)PageSize);
}