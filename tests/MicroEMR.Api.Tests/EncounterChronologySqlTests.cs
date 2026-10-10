using Microsoft.Data.SqlClient;
using MicroEMR.Infrastructure.PatientResults;
using Xunit;

namespace MicroEMR.Api.Tests;

// Reuse the existing opt-in, uniquely named two-database fixture; no application database is used.
public sealed class EncounterChronologySqlTests(NextAvailableSqlDatabase db) : IClassFixture<NextAvailableSqlDatabase>
{
    [SchedulingSearchSqlFact]
    public async Task HistoricalResultsReadIncludesAllLifecycleStatesAndIsolatesPatientAndTenant()
    {
        foreach (var connection in new[] { db.Connection, db.OtherConnection })
        {
            await using var sql = new SqlConnection(connection); await sql.OpenAsync();
            using var schema = new SqlCommand("""
                CREATE TABLE dbo.PatientResult (
                    PatientResultUid uniqueidentifier NOT NULL, PatientUid uniqueidentifier NOT NULL,
                    ResultType nvarchar(50) NOT NULL, ResultName nvarchar(200) NOT NULL, ResultDate datetime2 NOT NULL,
                    ResultSummary nvarchar(max), ResultValue nvarchar(500), ResultUnit nvarchar(100), ReferenceRange nvarchar(200),
                    ResultStatus nvarchar(50) NOT NULL, LifecycleStatus nvarchar(20) NOT NULL, SourceType nvarchar(20) NOT NULL,
                    SourceOrganization nvarchar(200), SourceSystem nvarchar(200), ExternalResultId nvarchar(200), ReceivedAtUtc datetime2,
                    Abnormality nvarchar(20) NOT NULL, PreviousResultUid uniqueidentifier, EnteredInErrorAtUtc datetime2,
                    EnteredInErrorBy bigint, EnteredInErrorReason nvarchar(500), ReviewedAt datetime2, ReviewedBy bigint, ReviewNote nvarchar(1000),
                    CreatedAt datetime2 NOT NULL, CreatedBy bigint, UpdatedAt datetime2, UpdatedBy bigint, RowVersion rowversion);
                """, sql);
            await schema.ExecuteNonQueryAsync();
        }
        var (patient, _, _) = await db.Seed(); var (otherPatient, _, _) = await db.Seed();
        await using (var sql = new SqlConnection(db.Connection))
        {
            await sql.OpenAsync();
            using var seed = new SqlCommand("""
                INSERT dbo.PatientResult(PatientResultUid,PatientUid,ResultType,ResultName,ResultDate,ResultValue,
                    ResultStatus,LifecycleStatus,SourceType,Abnormality,CreatedAt,CreatedBy)
                SELECT NEWID(),@patient,N'Lab',N'Report',CAST('2030-01-04' AS datetime2),N'42',N'Reviewed',state,N'Manual',N'Normal',SYSUTCDATETIME(),7
                FROM (VALUES(N'Current'),(N'Superseded'),(N'EnteredInError')) AS states(state);
                """, sql);
            seed.Parameters.AddWithValue("@patient", patient); await seed.ExecuteNonQueryAsync();
        }
        var first = new PatientResultRepository(new NextAvailableSqlDatabase.Factory(db.Connection));
        var second = new PatientResultRepository(new NextAvailableSqlDatabase.Factory(db.OtherConnection));
        var rows = await first.ListChronologyAsync(patient);
        Assert.Equal(3, rows.Count); Assert.All(rows, x => { Assert.Equal(patient, x.PatientUid); Assert.Equal("42", x.ResultValue); Assert.Equal("Test clinician", x.CreatedByDisplayName); });
        Assert.Equal(new[] { "Current", "EnteredInError", "Superseded" }, rows.Select(x => x.LifecycleStatus).Order());
        Assert.Empty(await first.ListChronologyAsync(otherPatient));
        Assert.Empty(await second.ListChronologyAsync(patient));
    }
}
