using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.PatientCpp;
using MicroEMR.Application.PatientEncounters;
using MicroEMR.Application.PatientMedications.Contracts;
using MicroEMR.Application.PatientMedications.Services;
using MicroEMR.Application.ReadAudit;
using MicroEMR.Infrastructure.PatientEncounters;
using MicroEMR.Infrastructure.PatientMedications;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class EncounterMedicationSqlTests(EncounterMedicationDatabase db) : IClassFixture<EncounterMedicationDatabase>
{
    [DiagnosisSqlFact]
    public async Task CreateUpdateDiscontinueReplayAndProvenanceKeepOneLongitudinalRecordAndSoap()
    {
        var (p, e) = await db.Inner.Seed(); var input = await db.Draft(e);
        Assert.Empty(await db.Medications().GetByPatientUidAsync(p));
        var row = (await db.Repository().SaveAsync(p, e, input, 7))!;
        var replay = (await db.Repository().SaveAsync(p, e, input, 7))!; Assert.Equal(row.RowVersion, replay.RowVersion);
        Assert.Equal(7L, row.CreatedBy); Assert.Single(await db.Medications().GetByPatientUidAsync(p));
        var before = await db.Snapshot(); input.Medication!.MedicationName = "TEST ONLY changed replay";
        await Assert.ThrowsAsync<EncounterMedicationConflictException>(() => db.Repository().SaveAsync(p, e, input, 7)); Assert.Equal(before, await db.Snapshot());
        input = await db.Draft(e); input.Action = "Update"; input.MedicationUid = row.MedicationUid; input.MedicationRowVersion = row.RowVersion; input.Medication!.Directions = "TEST ONLY reviewed instructions";
        var updated = (await db.Repository().SaveAsync(p, e, input, 7))!;
        Assert.Equal(row.MedicationUid, updated.MedicationUid); Assert.NotEqual(row.RowVersion, updated.RowVersion);
        Assert.Equal(updated.RowVersion, (await db.Repository().SaveAsync(p, e, input, 7))!.RowVersion);
        input = await db.Draft(e); input.Action = "Discontinue"; input.Medication = null; input.MedicationUid = row.MedicationUid; input.MedicationRowVersion = updated.RowVersion; input.DiscontinueReason = "TEST ONLY stopped";
        var stopped = (await db.Repository().SaveAsync(p, e, input, 7))!;
        Assert.Equal("Discontinued", stopped.Status); Assert.Equal(stopped.RowVersion, (await db.Repository().SaveAsync(p, e, input, 7))!.RowVersion);
        Assert.Single(await db.Medications().GetByPatientUidAsync(p));
        Assert.Equal(3, await db.Inner.Scalar<int>("SELECT COUNT(*) FROM dbo.PatientEncounterHistory WHERE EncounterUid=@p AND CreatedBy=7 AND ActionType LIKE 'CppMedication%'", e));
        Assert.Equal(3, await db.Inner.Scalar<int>("SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@p) AND EntityName='PatientMedication' AND UserId=7", row.MedicationUid));
        Assert.Equal("TEST ONLY reviewed instructions", await db.Inner.Scalar<string>("SELECT JSON_VALUE(NewValue,'$.Directions') FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@p) AND ActionName='Update'", row.MedicationUid));
        Assert.Equal("Preserved SOAP", await db.Inner.Scalar<string>("SELECT SubjectiveNote FROM dbo.PatientEncounter WHERE EncounterUid=@p", e));
    }

    [DiagnosisSqlFact]
    public async Task CppReflectsAuthoritativeOngoingChangesAndPreservesUneditedPlanComplianceAndLegacy()
    {
        var (p, e) = await db.Inner.Seed(); var input = await db.Draft(e);
        input.Medication!.PlanFields = new() { LongTermIntent = true };
        input.Medication.ComplianceAssessment = new() { State = "Not assessed", Explanation = "TEST ONLY retained", AssessmentAt = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        var row = (await db.Repository().SaveAsync(p, e, input, 7))!;
        var source = CppSelectivePrintTests.Source(p, null!, CppSelectivePrintTests.Stub<IPatientChartReadAuditService>((_, _) => Task.FromResult(Guid.NewGuid())), medication: new PatientMedicationService(db.Medications()));
        Assert.Equal(row.MedicationUid, Assert.Single((await source.GetAsync(p, "test"))!.Medications.Items).MedicationUid);
        var revision = row.ComplianceAssessment;
        input = await db.Draft(e); input.Action = "Update"; input.MedicationUid = row.MedicationUid; input.MedicationRowVersion = row.RowVersion; input.Medication!.Classification = "Acute";
        var acute = (await db.Repository().SaveAsync(p, e, input, 7))!;
        Assert.True(acute.LongTermIntent); Assert.Equal(System.Text.Json.JsonSerializer.Serialize(revision), System.Text.Json.JsonSerializer.Serialize(acute.ComplianceAssessment)); Assert.Single(acute.ComplianceHistory);
        Assert.Empty((await source.GetAsync(p, "test"))!.Medications.Items);
        Assert.Single((await source.GetForPrintAsync(p, ["Medications"], "test", default, "Acute"))!.Medications.Items);
        var legacy = (await db.Medications().GetByUidAsync(db.LegacyUid))!;
        Assert.Equal(db.LegacyVersion, legacy.RowVersion); Assert.Null(legacy.Classification); Assert.Null(legacy.LongTermIntent); Assert.Null(legacy.ComplianceAssessment);
    }

    [DiagnosisSqlFact]
    public async Task StaleVersionsSignedStateAndInactiveActorCannotMutate()
    {
        var (p, e) = await db.Inner.Seed(); var input = await db.Draft(e); var row = (await db.Repository().SaveAsync(p, e, input, 7))!;
        input = await db.Draft(e); input.Action = "Update"; input.MedicationUid = row.MedicationUid; input.MedicationRowVersion = Convert.ToBase64String(new byte[8]);
        var before = await db.Snapshot();
        await Assert.ThrowsAsync<EncounterMedicationConflictException>(() => db.Repository().SaveAsync(p, e, input, 7)); Assert.Equal(before, await db.Snapshot());
        input.MedicationRowVersion = row.RowVersion; input.EncounterRowVersion = Convert.ToBase64String(new byte[8]);
        await Assert.ThrowsAsync<EncounterMedicationConflictException>(() => db.Repository().SaveAsync(p, e, input, 7));
        input.EncounterRowVersion = (await db.Draft(e)).EncounterRowVersion;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => db.Repository().SaveAsync(p, e, input, long.MaxValue));
        await db.Inner.Execute("UPDATE dbo.PatientEncounter SET EncounterStatus='Signed' WHERE EncounterUid=@p", e);
        input.EncounterRowVersion = (await db.Draft(e)).EncounterRowVersion; before = await db.Snapshot();
        await Assert.ThrowsAsync<EncounterMedicationConflictException>(() => db.Repository().SaveAsync(p, e, input, 7)); Assert.Equal(before, await db.Snapshot());
    }

    [DiagnosisSqlFact]
    public async Task WrongPatientEncounterMedicationAndBothTenantDirectionsStayUnchanged()
    {
        var (p, e) = await db.Inner.Seed(); var (other, otherEncounter) = await db.Inner.Seed(); var input = await db.Draft(e);
        var row = (await db.Repository().SaveAsync(p, e, input, 7))!;
        input.Action = "Update"; input.RequestUid = Guid.NewGuid(); input.MedicationUid = row.MedicationUid; input.MedicationRowVersion = row.RowVersion;
        var a = await db.Snapshot(); var b = await db.Snapshot(true);
        Assert.Null(await db.Repository().SaveAsync(other, e, input, 7));
        input.EncounterRowVersion = (await db.Draft(otherEncounter)).EncounterRowVersion;
        Assert.Null(await db.Repository().SaveAsync(other, otherEncounter, input, 7));
        Assert.Null(await db.Repository(true).SaveAsync(p, e, input, 7));
        var foreign = await db.SeedOther(); var foreignInput = await db.Draft(foreign.Encounter, true);
        var foreignRow = (await db.Repository(true).SaveAsync(foreign.Patient, foreign.Encounter, foreignInput, 7))!;
        foreignInput.Action = "Update"; foreignInput.RequestUid = Guid.NewGuid(); foreignInput.MedicationUid = foreignRow.MedicationUid; foreignInput.MedicationRowVersion = foreignRow.RowVersion;
        b = await db.Snapshot(true); Assert.Null(await db.Repository().SaveAsync(foreign.Patient, foreign.Encounter, foreignInput, 7));
        Assert.Equal(a, await db.Snapshot()); Assert.Equal(b, await db.Snapshot(true));
    }

    [DiagnosisSqlTheory]
    [InlineData("AuditLog", "Create")] [InlineData("PatientEncounterHistory", "Create")]
    [InlineData("PatientEncounterHistory", "Update")] [InlineData("PatientEncounterHistory", "Discontinue")]
    public async Task SourceAuditOrLateProvenanceFailureRollsBackAllChanges(string table, string action)
    {
        var (p, e) = await db.Inner.Seed(); var input = await db.Draft(e);
        if (action != "Create")
        {
            var row = (await db.Repository().SaveAsync(p, e, input, 7))!; input = await db.Draft(e);
            input.Action = action; input.MedicationUid = row.MedicationUid; input.MedicationRowVersion = row.RowVersion;
        }
        var before = await db.Snapshot();
        await db.Inner.Execute($"CREATE TRIGGER dbo.MedicationFailure ON dbo.{table} AFTER INSERT AS BEGIN THROW 53997,'TEST ONLY medication failure',1; END");
        try { await Assert.ThrowsAsync<SqlException>(() => db.Repository().SaveAsync(p, e, input, 7)); Assert.Equal(before, await db.Snapshot()); }
        finally { await db.Inner.Execute("DROP TRIGGER dbo.MedicationFailure"); }
    }

    [DiagnosisSqlTheory]
    [InlineData(true)] [InlineData(false)]
    public async Task SigningAndMedicationCommitSerializeInBothOrderings(bool signFirst)
    {
        var (p, e) = await db.Inner.Seed(); var input = await db.Draft(e);
        if (signFirst)
        {
            await using var connection = new SqlConnection(db.Inner.Connection); await connection.OpenAsync();
            await using var tx = (SqlTransaction)await connection.BeginTransactionAsync();
            await using var sign = new SqlCommand("UPDATE dbo.PatientEncounter SET EncounterStatus='Signed' WHERE EncounterUid=@e", connection, tx); sign.Parameters.AddWithValue("@e", e); await sign.ExecuteNonQueryAsync();
            var attempt = db.Repository().SaveAsync(p, e, input, 7); await Task.Delay(150); Assert.False(attempt.IsCompleted); await tx.CommitAsync();
            await Assert.ThrowsAsync<EncounterMedicationConflictException>(() => attempt); Assert.Empty(await db.Medications().GetByPatientUidAsync(p));
        }
        else
        {
            await db.Inner.Execute("CREATE TRIGGER dbo.MedicationSlowHistory ON dbo.PatientEncounterHistory AFTER INSERT AS BEGIN WAITFOR DELAY '00:00:01'; END");
            try
            {
                var attempt = db.Repository().SaveAsync(p, e, input, 7); await Task.Delay(150); Assert.False(attempt.IsCompleted);
                var sign = db.Inner.Execute("UPDATE dbo.PatientEncounter SET EncounterStatus='Signed' WHERE EncounterUid=@p", e); await Task.Delay(150); Assert.False(sign.IsCompleted);
                Assert.NotNull(await attempt); await sign; Assert.Single(await db.Medications().GetByPatientUidAsync(p));
            }
            finally { await db.Inner.Execute("DROP TRIGGER dbo.MedicationSlowHistory"); }
        }
    }
}

public sealed class EncounterMedicationDatabase : IAsyncLifetime
{
    public EncounterDiagnosisSqlDatabase Inner { get; } = new();
    public Guid LegacyUid { get; } = Guid.NewGuid(); public string LegacyVersion = "";
    public EncounterMedicationRepository Repository(bool other = false) => new(new EncounterDiagnosisSqlTests.Factory(other ? Inner.OtherConnection : Inner.Connection));
    public PatientMedicationRepository Medications(bool other = false) => new(new EncounterDiagnosisSqlTests.Factory(other ? Inner.OtherConnection : Inner.Connection), NullLogger<PatientMedicationRepository>.Instance);
    public async Task InitializeAsync()
    {
        await Inner.InitializeAsync();
        try
        {
            foreach (var other in new[] { false, true })
            {
                await Apply(other, "patient_medication_stored_procedures.sql");
                await Apply(other, "tenant-clinical/migrations/0059-medication-discontinuation-concurrency.sql");
                if (!other)
                {
                    var (p, _) = await Inner.Seed(); await using var connection = new SqlConnection(Inner.Connection); await connection.OpenAsync();
                    await using var command = new SqlCommand("INSERT dbo.PatientMedication(MedicationUid,PatientUid,MedicationName) OUTPUT inserted.RowVersion VALUES(@u,@p,'TEST ONLY legacy')", connection);
                    command.Parameters.AddWithValue("@u", LegacyUid); command.Parameters.AddWithValue("@p", p); LegacyVersion = Convert.ToBase64String((byte[])(await command.ExecuteScalarAsync())!);
                }
                foreach (var file in new[] { "0077-medication-classification.sql", "0087-medication-plan-intent-safeguards.sql", "0089-medication-compliance-assessments.sql" }) await Apply(other, "tenant-clinical/migrations/" + file);
            }
        }
        catch { await Inner.DisposeAsync(); throw; }
    }
    private async Task Apply(bool other, string file)
    {
        var sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "database", file));
        await using var connection = new SqlConnection(other ? Inner.OtherConnection : Inner.Connection); await connection.OpenAsync();
        foreach (var batch in Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Where(x => !string.IsNullOrWhiteSpace(x))) await new SqlCommand(batch, connection).ExecuteNonQueryAsync();
    }
    public async Task<SaveEncounterMedicationRequest> Draft(Guid encounter, bool other = false)
    {
        await using var connection = new SqlConnection(other ? Inner.OtherConnection : Inner.Connection); await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT RowVersion FROM dbo.PatientEncounter WHERE EncounterUid=@e", connection); command.Parameters.AddWithValue("@e", encounter);
        var input = EncounterMedicationTests.Draft(); input.EncounterRowVersion = Convert.ToBase64String((byte[])(await command.ExecuteScalarAsync())!); EncounterMedicationService.Validate(input); return input;
    }
    public async Task<string> Snapshot(bool other = false)
    {
        await using var connection = new SqlConnection(other ? Inner.OtherConnection : Inner.Connection); await connection.OpenAsync();
        return (string)(await new SqlCommand("SELECT (SELECT * FROM dbo.PatientMedication ORDER BY MedicationUid FOR JSON PATH,INCLUDE_NULL_VALUES) Clinical,(SELECT * FROM dbo.PatientMedicationComplianceRevision ORDER BY MedicationUid,RevisionNumber FOR JSON PATH,INCLUDE_NULL_VALUES) Revisions,(SELECT * FROM dbo.AuditLog ORDER BY AuditLogId FOR JSON PATH,INCLUDE_NULL_VALUES) Audit,(SELECT * FROM dbo.PatientEncounterHistory ORDER BY EncounterHistoryUid FOR JSON PATH,INCLUDE_NULL_VALUES) History FOR JSON PATH,WITHOUT_ARRAY_WRAPPER", connection).ExecuteScalarAsync())!;
    }
    public async Task<(Guid Patient, Guid Encounter)> SeedOther()
    {
        var p = Guid.NewGuid(); var e = Guid.NewGuid();
        await using var connection = new SqlConnection(Inner.OtherConnection); await connection.OpenAsync();
        await using var cmd = new SqlCommand("INSERT dbo.Patient(PatientUid) VALUES(@p); INSERT dbo.PatientEncounter(EncounterUid,PatientUid,PatientId,EncounterStatus,SubjectiveNote) VALUES(@e,@p,SCOPE_IDENTITY(),'Open','Preserved SOAP');", connection); cmd.Parameters.AddWithValue("@p", p); cmd.Parameters.AddWithValue("@e", e); await cmd.ExecuteNonQueryAsync(); return (p, e);
    }
    public Task DisposeAsync() => Inner.DisposeAsync();
}
