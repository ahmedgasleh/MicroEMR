namespace MicroEMR.Api.Contracts.Patients;

public sealed class PatientListItemResponse
{
    public Guid PatientUid { get; set; }

    public string ChartNumber { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string? MiddleName { get; set; }

    public string LastName { get; set; } = string.Empty;

    public string? PreferredName { get; set; }

    public DateOnly DateOfBirth { get; set; }

    public string? SexAtBirth { get; set; }

    public string? HealthCardNumber { get; set; }

    public string? HealthCardVersion { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public bool IsActive { get; set; }

    public int TotalRows { get; set; }

    public string FullName =>
        string.Join(
            " ",
            new [] { FirstName, MiddleName, LastName }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
}