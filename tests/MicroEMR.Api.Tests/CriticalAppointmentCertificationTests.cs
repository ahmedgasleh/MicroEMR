using MicroEMR.Application.Scheduling.Contracts;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class CriticalAppointmentCertificationTests
{
    [Fact]
    public void ContractsCarryCriticalFlagAcrossCreateUpdateAndReads()
    {
        Assert.True(new CreateScheduleAppointmentRequest { IsCritical = true }.IsCritical);
        Assert.True(new UpdateScheduleAppointmentRequest { IsCritical = true }.IsCritical);
        Assert.True(new ScheduleAppointmentDetailsResponse { IsCritical = true }.IsCritical);
        Assert.True(new ScheduleAppointmentListItemResponse { IsCritical = true }.IsCritical);
    }

    [Fact]
    public void MigrationAddsBackwardCompatibleFlagAndCriticalAwareProcedures()
    {
        var sql = File.ReadAllText(Path.Combine(Root(), "db", "tenant-clinical", "migrations",
            "0042-scheduling-critical-appointments.sql"));

        Assert.Contains("ADD IsCritical BIT NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DEFAULT (0)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ScheduleAppointment_CreateWithCriticalFlag", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ScheduleAppointment_UpdateWithCriticalFlag", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ScheduleAppointment_GetByUidWithCriticalFlag", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ManifestAppendsOnlyNextMigration()
    {
        var manifest = File.ReadAllText(Path.Combine(Root(), "db", "tenant-clinical", "manifest.json"));
        using var document = System.Text.Json.JsonDocument.Parse(manifest);
        var migrationIds = document.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("migrationId").GetString())
            .ToArray();

        Assert.Contains("0041-patient-clinical-history", migrationIds);
        Assert.Equal("0042-scheduling-critical-appointments", migrationIds[^17]);
        Assert.Equal("0043-patient-chart-read-audit", migrationIds[^16]);
        Assert.Equal("0044-structured-read-audit-procedure", migrationIds[^15]);
        Assert.Equal("0045-structured-disclosure-audit-events", migrationIds[^14]);
        Assert.Equal("0046-aggregate-report-audit-events", migrationIds[^13]);
        Assert.Equal("0047-patient-immunization-history", migrationIds[^12]);
        Assert.Equal("0048-clinical-data-migration-validation-foundation", migrationIds[^11]);
        Assert.Equal("0049-clinical-data-migration-import-foundation", migrationIds[^10]);
        Assert.Equal("0050-patient-prescription-foundation", migrationIds[^9]);
        Assert.Equal("0051-result-review-acknowledgement-hardening", migrationIds[^8]);
        Assert.Equal("0052-cds-foundation", migrationIds[^7]);
        Assert.Equal("0053-cdm-enrollment-foundation", migrationIds[^6]);
        Assert.Equal("0055-verified-negative-allergy-assertion", migrationIds[^4]);
        Assert.Equal("0056-referral-letter-artifact", migrationIds[^3]);
        Assert.Equal("0058-referral-followup-response-tracking", migrationIds[^1]);
        Assert.Equal(migrationIds.Length, migrationIds.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void DayViewProvidesFlagInputsDetailsAndDistinctStyling()
    {
        var view = File.ReadAllText(Path.Combine(Root(), "src", "MicroEMR.Web", "Views", "Scheduling", "Index.cshtml"));

        Assert.Contains("name=\"IsCritical\"", view);
        Assert.Contains("name=\"IsCritical\" value=\"true\"", view);
        Assert.Contains("detailsPriority", view);
        Assert.Contains("scheduling-critical-appointment", view);
        Assert.Contains("details.isCritical ? \"Critical\" : \"Standard\"", view);
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MicroEMR.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
