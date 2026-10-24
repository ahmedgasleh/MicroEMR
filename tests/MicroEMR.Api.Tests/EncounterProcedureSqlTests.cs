using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.PatientClinicalHistory;
using MicroEMR.Application.PatientEncounters;
using MicroEMR.Infrastructure.PatientClinicalHistory;
using MicroEMR.Infrastructure.PatientEncounters;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class EncounterProcedureSqlTests(EncounterProcedureDatabase db) : IClassFixture<EncounterProcedureDatabase>
{
    [DiagnosisSqlFact]
    public async Task CreateReplayUpdateCppPrintAndProvenancePreserveSoapAndSourceIdentity()
    {
        var (patient, encounter) = await db.Inner.Seed();
        var input = await db.Draft(encounter);
        var row = (await db.Repository().SaveAsync(patient, encounter, input, 7))!;
        Assert.Equal(input.HistoryUid, row.HistoryUid); Assert.Equal("Procedure", row.HistoryType); Assert.Equal(7, row.CreatedBy);
        var replay = (await db.Repository().SaveAsync(patient, encounter, input, 7))!;
        Assert.Equal(row.RowVersion, replay.RowVersion);
        Assert.Equal(1, await db.Inner.Scalar<int>("SELECT COUNT(*) FROM dbo.PatientClinicalHistory WHERE PatientUid=@p", patient));
        Assert.Equal(1, await db.Inner.Scalar<int>("SELECT COUNT(*) FROM dbo.PatientEncounterHistory WHERE EncounterUid=@p AND ActionType='CppProcedureCreated' AND CreatedBy=7", encounter));
        Assert.Equal(1, await db.Inner.Scalar<int>("SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@p) AND ActionName='Create' AND UserId=7", row.HistoryUid));
        input.IsNew = false; input.HistoryRowVersion = row.RowVersion; input.Description = "TEST ONLY corrected <procedure>";
        var changed = (await db.Repository().SaveAsync(patient, encounter, input, 7))!;
        Assert.NotEqual(row.RowVersion, changed.RowVersion);
        Assert.Equal("Preserved SOAP", await db.Inner.Scalar<string>("SELECT SubjectiveNote FROM dbo.PatientEncounter WHERE EncounterUid=@p", encounter));
        Assert.Equal("Open", await db.Inner.Scalar<string>("SELECT EncounterStatus FROM dbo.PatientEncounter WHERE EncounterUid=@p", encounter));
        Assert.Equal(1, await db.Inner.Scalar<int>("SELECT COUNT(*) FROM dbo.PatientEncounterHistory WHERE EncounterUid=@p AND ActionType='CppProcedureUpdated'", encounter));
        var stored = Assert.Single(await db.History().ListAsync(patient, "Active")); Assert.Equal(changed.HistoryUid, stored.HistoryUid);
        var fixture = new CppSelectivePrintTests.Fixture { ActualHistory = new PatientClinicalHistoryService(db.History()) };
        fixture.Patient.PatientUid = patient; fixture.Data = fixture.Data with { PatientUid = patient };
        var html = Encoding.UTF8.GetString((await fixture.Print("History"))!);
        Assert.Contains("TEST ONLY corrected &lt;procedure&gt;", html); Assert.Contains("2000", html);
        var block = html[html.IndexOf("TEST ONLY corrected", StringComparison.Ordinal)..];
        Assert.DoesNotContain("Life Stage", block); Assert.DoesNotContain("Clinical Status", block);
    }

    [DiagnosisSqlFact]
    public async Task UpgradeAndSurgicalUpdateKeepLegacyMedicalSurgicalValuesVersionsAndAudits()
    {
        Assert.Equal(db.LegacyBefore, await db.LegacySnapshot());
        var (patient, encounter) = await db.Inner.Seed();
        var surgery = await db.History().CreateAsync(patient, new() { HistoryType = "Surgical", Description = "TEST ONLY old surgery", RelevantDate = new(1990, 1, 2), ClinicalFields = new() { ProcedureDate = new() { Year = 1980 }, Notes = "old" } }, 7);
        var input = await db.Draft(encounter); input.IsNew = false; input.HistoryUid = surgery.HistoryUid; input.HistoryRowVersion = surgery.RowVersion;
        var saved = (await db.Repository().SaveAsync(patient, encounter, input, 7))!;
        Assert.Equal("Surgical", saved.HistoryType); Assert.Equal(surgery.RelevantDate, saved.RelevantDate);
        var medical = await db.History().CreateAsync(patient, new() { HistoryType = "Medical", Description = "TEST ONLY medical" }, 7);
        input.HistoryUid = medical.HistoryUid; input.HistoryRowVersion = medical.RowVersion;
        await Assert.ThrowsAsync<EncounterProcedureConflictException>(() => db.Repository().SaveAsync(patient, encounter, input, 7));
    }

    [DiagnosisSqlFact]
    public async Task StaleTokensSignedArchivedAndInactiveActorCannotWrite()
    {
        var (p, e) = await db.Inner.Seed(); var input = await db.Draft(e);
        var row = (await db.Repository().SaveAsync(p, e, input, 7))!;
        input.IsNew = false; input.HistoryRowVersion = Convert.ToBase64String(new byte[8]);
        var before = await db.Snapshot();
        await Assert.ThrowsAsync<EncounterProcedureConflictException>(() => db.Repository().SaveAsync(p, e, input, 7));
        Assert.Equal(before, await db.Snapshot());
        input.HistoryRowVersion = row.RowVersion; input.EncounterRowVersion = Convert.ToBase64String(new byte[8]);
        await Assert.ThrowsAsync<EncounterProcedureConflictException>(() => db.Repository().SaveAsync(p, e, input, 7));
        input.EncounterRowVersion = (await db.Draft(e)).EncounterRowVersion;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => db.Repository().SaveAsync(p, e, input, long.MaxValue));
        await db.Inner.Execute("UPDATE dbo.PatientEncounter SET EncounterStatus='Signed' WHERE EncounterUid=@p", e);
        input.EncounterRowVersion = (await db.Draft(e)).EncounterRowVersion;
        before = await db.Snapshot();
        await Assert.ThrowsAsync<EncounterProcedureConflictException>(() => db.Repository().SaveAsync(p, e, input, 7));
        Assert.Equal(before, await db.Snapshot()); // reopening the read UI cannot change Signed state
        var (p2, e2) = await db.Inner.Seed(); var newInput = await db.Draft(e2);
        var active = (await db.Repository().SaveAsync(p2, e2, newInput, 7))!;
        var archived = (await db.History().ArchiveAsync(p2, active.HistoryUid, active.RowVersion, 7))!;
        newInput.IsNew = false; newInput.HistoryRowVersion = archived.RowVersion;
        await Assert.ThrowsAsync<EncounterProcedureConflictException>(() => db.Repository().SaveAsync(p2, e2, newInput, 7));
    }

    [DiagnosisSqlFact]
    public async Task WrongPatientEncounterResourceAndBothTenantDirectionsStayUnchanged()
    {
        var (p, e) = await db.Inner.Seed(); var (other, otherEncounter) = await db.Inner.Seed();
        var input = await db.Draft(e); var row = (await db.Repository().SaveAsync(p, e, input, 7))!;
        input.IsNew = false; input.HistoryRowVersion = row.RowVersion;
        var a = await db.Snapshot(); var b = await db.Snapshot(true);
        Assert.Null(await db.Repository().SaveAsync(other, e, input, 7));
        input.EncounterRowVersion = (await db.Draft(otherEncounter)).EncounterRowVersion;
        Assert.Null(await db.Repository().SaveAsync(other, otherEncounter, input, 7));
        Assert.Null(await db.Repository(true).SaveAsync(p, e, input, 7));
        // Reverse direction uses genuine other-store patient/encounter/source IDs.
        var foreign = await db.SeedOther(); var foreignInput = await db.Draft(foreign.Encounter, true);
        var foreignRow = (await db.Repository(true).SaveAsync(foreign.Patient, foreign.Encounter, foreignInput, 7))!;
        foreignInput.IsNew = false; foreignInput.HistoryRowVersion = foreignRow.RowVersion;
        b = await db.Snapshot(true);
        Assert.Null(await db.Repository().SaveAsync(foreign.Patient, foreign.Encounter, foreignInput, 7));
        Assert.Equal(a, await db.Snapshot()); Assert.Equal(b, await db.Snapshot(true));
    }

    [DiagnosisSqlTheory]
    [InlineData("AuditLog")] [InlineData("PatientEncounterHistory")]
    public async Task LateAuditOrProvenanceFailureRollsBackDespitePrecommitResultRow(string table)
    {
        var (p, e) = await db.Inner.Seed(); var input = await db.Draft(e); var before = await db.Snapshot();
        await db.Inner.Execute($"CREATE TRIGGER dbo.ProcedureFailure ON dbo.{table} AFTER INSERT AS BEGIN THROW 53998,'TEST ONLY failure',1; END");
        try
        {
            await Assert.ThrowsAsync<SqlException>(() => db.Repository().SaveAsync(p, e, input, 7));
            Assert.Equal(before, await db.Snapshot());
        }
        finally { await db.Inner.Execute("DROP TRIGGER dbo.ProcedureFailure"); }
    }

    [DiagnosisSqlFact]
    public async Task UpdateProvenanceFailureRollsBackOriginalValuesAndVersion()
    {
        var (p, e) = await db.Inner.Seed(); var input = await db.Draft(e);
        var row = (await db.Repository().SaveAsync(p, e, input, 7))!;
        input.IsNew = false; input.HistoryRowVersion = row.RowVersion; input.Description = "TEST ONLY rollback edit";
        var before = await db.Snapshot();
        await db.Inner.Execute("CREATE TRIGGER dbo.ProcedureFailure ON dbo.PatientEncounterHistory AFTER INSERT AS BEGIN THROW 53998,'TEST ONLY history failure',1; END");
        try
        {
            await Assert.ThrowsAsync<SqlException>(() => db.Repository().SaveAsync(p, e, input, 7));
            Assert.Equal(before, await db.Snapshot());
        }
        finally { await db.Inner.Execute("DROP TRIGGER dbo.ProcedureFailure"); }
    }

    [DiagnosisSqlFact]
    public async Task ProcedureSaveFirstBlocksSigningUntilClinicalAuditAndProvenanceCommit()
    {
        var (p, e) = await db.Inner.Seed(); var input = await db.Draft(e);
        await db.Inner.Execute("CREATE TRIGGER dbo.ProcedureSlowHistory ON dbo.PatientEncounterHistory AFTER INSERT AS BEGIN WAITFOR DELAY '00:00:01'; END");
        try
        {
            var attempt = db.Repository().SaveAsync(p, e, input, 7);
            await Task.Delay(150); Assert.False(attempt.IsCompleted);
            var sign = db.Inner.Execute("UPDATE dbo.PatientEncounter SET EncounterStatus='Signed' WHERE EncounterUid=@p", e);
            await Task.Delay(150); Assert.False(sign.IsCompleted);
            Assert.NotNull(await attempt); await sign;
            Assert.Equal("Signed", await db.Inner.Scalar<string>("SELECT EncounterStatus FROM dbo.PatientEncounter WHERE EncounterUid=@p", e));
            Assert.Single(await db.History().ListAsync(p, "All"));
        }
        finally { await db.Inner.Execute("DROP TRIGGER dbo.ProcedureSlowHistory"); }
    }

    [DiagnosisSqlFact]
    public async Task SigningLockSerializesAndRejectsSaveAfterSignCommits()
    {
        var (p, e) = await db.Inner.Seed(); var input = await db.Draft(e);
        await using var connection = new SqlConnection(db.Inner.Connection); await connection.OpenAsync();
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync();
        await using var sign = new SqlCommand("UPDATE dbo.PatientEncounter SET EncounterStatus='Signed' WHERE EncounterUid=@e", connection, tx);
        sign.Parameters.AddWithValue("@e", e); await sign.ExecuteNonQueryAsync();
        var attempt = db.Repository().SaveAsync(p, e, input, 7);
        await Task.Delay(150); Assert.False(attempt.IsCompleted);
        await tx.CommitAsync();
        await Assert.ThrowsAsync<EncounterProcedureConflictException>(() => attempt);
        Assert.Empty(await db.History().ListAsync(p, "All"));
    }
}

public sealed class EncounterProcedureDatabase : IAsyncLifetime
{
    public EncounterDiagnosisSqlDatabase Inner { get; } = new();
    public string LegacyBefore = "";
    private Guid legacy;
    public EncounterProcedureRepository Repository(bool other = false) => new(new EncounterDiagnosisSqlTests.Factory(other ? Inner.OtherConnection : Inner.Connection));
    public PatientClinicalHistoryRepository History() => new(new EncounterDiagnosisSqlTests.Factory(Inner.Connection));
    public async Task InitializeAsync()
    {
        await Inner.InitializeAsync();
        try
        {
            var seed = await Inner.Seed(); legacy = seed.Item1;
            foreach (var kind in new[] { "Medical", "Surgical" }) await History().CreateAsync(legacy, new() { HistoryType = kind, Description = "TEST ONLY legacy " + kind }, 7);
            LegacyBefore = await LegacySnapshot();
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
            var sql = File.ReadAllText(Path.Combine(root!.FullName, "db/tenant-clinical/migrations/0092-encounter-procedures.sql"));
            foreach (var other in new[] { false, true })
            {
                await using var connection = new SqlConnection(other ? Inner.OtherConnection : Inner.Connection); await connection.OpenAsync();
                foreach (var batch in Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Where(x => !string.IsNullOrWhiteSpace(x)))
                    await new SqlCommand(batch, connection).ExecuteNonQueryAsync();
            }
        }
        catch { await Inner.DisposeAsync(); throw; }
    }
    public Task<string> LegacySnapshot() => Inner.Scalar<string>("SELECT (SELECT * FROM dbo.PatientClinicalHistory WHERE PatientUid=@p ORDER BY HistoryUid FOR JSON PATH,INCLUDE_NULL_VALUES) Clinical,(SELECT * FROM dbo.AuditLog WHERE PatientId=(SELECT PatientId FROM dbo.Patient WHERE PatientUid=@p) ORDER BY AuditLogId FOR JSON PATH,INCLUDE_NULL_VALUES) Audit FOR JSON PATH,WITHOUT_ARRAY_WRAPPER", legacy);
    public async Task<SaveEncounterProcedureRequest> Draft(Guid encounter, bool other = false)
    {
        await using var connection = new SqlConnection(other ? Inner.OtherConnection : Inner.Connection); await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT RowVersion FROM dbo.PatientEncounter WHERE EncounterUid=@e", connection); command.Parameters.AddWithValue("@e", encounter);
        var input = EncounterProcedureTests.Draft(); input.EncounterRowVersion = Convert.ToBase64String((byte[])(await command.ExecuteScalarAsync())!); return input;
    }
    public async Task<string> Snapshot(bool other = false)
    {
        await using var connection = new SqlConnection(other ? Inner.OtherConnection : Inner.Connection); await connection.OpenAsync();
        return (string)(await new SqlCommand("SELECT (SELECT * FROM dbo.PatientClinicalHistory ORDER BY HistoryUid FOR JSON PATH,INCLUDE_NULL_VALUES) Clinical,(SELECT * FROM dbo.AuditLog ORDER BY AuditLogId FOR JSON PATH,INCLUDE_NULL_VALUES) Audit,(SELECT * FROM dbo.PatientEncounterHistory ORDER BY EncounterHistoryUid FOR JSON PATH,INCLUDE_NULL_VALUES) History FOR JSON PATH,WITHOUT_ARRAY_WRAPPER", connection).ExecuteScalarAsync())!;
    }
    public async Task<(Guid Patient, Guid Encounter)> SeedOther()
    {
        var p = Guid.NewGuid(); var e = Guid.NewGuid();
        await using var connection = new SqlConnection(Inner.OtherConnection); await connection.OpenAsync();
        await using var cmd = new SqlCommand("INSERT dbo.Patient(PatientUid) VALUES(@p); INSERT dbo.PatientEncounter(EncounterUid,PatientUid,PatientId,EncounterStatus,SubjectiveNote) VALUES(@e,@p,SCOPE_IDENTITY(),'Open','Preserved SOAP');", connection);
        cmd.Parameters.AddWithValue("@p", p); cmd.Parameters.AddWithValue("@e", e); await cmd.ExecuteNonQueryAsync(); return (p, e);
    }
    public Task DisposeAsync() => Inner.DisposeAsync();
}
