using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.PatientRiskFactors;
using MicroEMR.Infrastructure.PatientRiskFactors;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class RiskFactorSqlTests(RiskFactorSqlDatabase db) : IClassFixture<RiskFactorSqlDatabase>
{
    internal static CreatePatientRiskFactorRequest Draft() => new()
    {
        RiskFactor = "TEST ONLY - tobacco exposure", ExposureDetails = "TEST ONLY - 2 cigarettes per day",
        LifeStageCode = "A", Notes = "TEST ONLY - synthetic risk-factor fixture"
    };
    internal static UpdatePatientRiskFactorRequest Edit(PatientRiskFactorResponse row) => new()
    {
        RiskFactor = row.RiskFactor, ExposureDetails = row.ExposureDetails, LifeStageCode = row.LifeStageCode,
        Notes = row.Notes, RowVersion = row.RowVersion
    };
    private static string Route(Guid patient, Guid? uid = null) => $"api/patients/{patient}/risk-factors" + (uid is null ? "" : $"/{uid}");
    private static async Task<PatientRiskFactorResponse> Create(HttpClient client, Guid patient, CreatePatientRiskFactorRequest? request = null)
    {
        using var response = await client.PostAsJsonAsync(Route(patient), request ?? Draft());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PatientRiskFactorResponse>())!;
    }

    [Fact]
    public async Task HttpLifecycleCorrelatesAllClinicalAuditAndRejectsStaleAndReadOnlyWrites()
    {
        var patient = await db.Clinical.SeedPatient(false);
        var creator = await db.Clinical.SeedUser(false); var editor = await db.Clinical.SeedUser(false);
        var reader = await db.Clinical.SeedUser(false, false);
        var noRead = await db.Clinical.SeedUser(false, false, false);
        await using var host = await FamilyHistoryAcceptanceHost.Start(db.Clinical, false, creator, editor, reader, noRead);
        using var a = host.Client(creator); using var e = host.Client(editor); using var r = host.Client(reader);
        using var deniedReader = host.Client(noRead);
        var from = await db.Clinical.Utc(false);
        var row = await Create(a, patient);
        Assert.Equal(patient, row.PatientUid); Assert.Equal(creator.Actor, row.CreatedBy);
        Assert.Equal(Draft().RiskFactor, row.RiskFactor); Assert.Equal(Draft().ExposureDetails, row.ExposureDetails);
        Assert.Equal("A", row.LifeStageCode); Assert.Equal("Adult", row.LifeStage); Assert.Equal(Draft().Notes, row.Notes);
        Assert.Equal(row, await r.GetFromJsonAsync<PatientRiskFactorResponse>(Route(patient, row.RiskFactorUid)));

        var change = Edit(row); change.Notes = "TEST ONLY - reviewed";
        using var update = await e.PutAsJsonAsync(Route(patient, row.RiskFactorUid), change);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var saved = (await update.Content.ReadFromJsonAsync<PatientRiskFactorResponse>())!;
        Assert.Equal(row.RiskFactorUid, saved.RiskFactorUid); Assert.Equal(row.PatientUid, saved.PatientUid);
        Assert.Equal(row.CreatedAt, saved.CreatedAt); Assert.Equal(row.CreatedBy, saved.CreatedBy);
        Assert.Equal(editor.Actor, saved.UpdatedBy); Assert.NotEqual(row.RowVersion, saved.RowVersion);
        Assert.Equal(row.RiskFactor, saved.RiskFactor); Assert.Equal(row.ExposureDetails, saved.ExposureDetails);
        Assert.Equal(row.LifeStageCode, saved.LifeStageCode); Assert.Equal(change.Notes, saved.Notes);

        var before = await db.Snapshot(false); var otherBefore = await db.Snapshot(true);
        using (var denied = await deniedReader.GetAsync(Route(patient))) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var denied = await deniedReader.GetAsync(Route(patient, row.RiskFactorUid))) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var stale = await a.PutAsJsonAsync(Route(patient, row.RiskFactorUid), Edit(row))) Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using (var stale = await a.PostAsJsonAsync(Route(patient, row.RiskFactorUid) + "/archive", new { rowVersion = row.RowVersion })) Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using (var denied = await r.PostAsJsonAsync(Route(patient), Draft())) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var denied = await r.PutAsJsonAsync(Route(patient, row.RiskFactorUid), Edit(saved))) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var denied = await r.PostAsJsonAsync(Route(patient, row.RiskFactorUid) + "/archive", new { rowVersion = saved.RowVersion })) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(before, await db.Snapshot(false)); Assert.Equal(otherBefore, await db.Snapshot(true));

        using var archive = await e.PostAsJsonAsync(Route(patient, row.RiskFactorUid) + "/archive", new { rowVersion = saved.RowVersion });
        Assert.Equal(HttpStatusCode.OK, archive.StatusCode);
        var archived = (await archive.Content.ReadFromJsonAsync<PatientRiskFactorResponse>())!;
        Assert.Equal("Archived", archived.Status); Assert.NotEqual(saved.RowVersion, archived.RowVersion);
        Assert.Equal(row.CreatedAt, archived.CreatedAt); Assert.Equal(creator.Actor, archived.CreatedBy);
        Assert.Equal(editor.Actor, archived.UpdatedBy); Assert.Equal(saved.Notes, archived.Notes);
        Assert.Equal(saved.RiskFactor, archived.RiskFactor); Assert.Equal(saved.ExposureDetails, archived.ExposureDetails);
        Assert.Equal(saved.LifeStageCode, archived.LifeStageCode);
        Assert.Empty((await a.GetFromJsonAsync<PatientRiskFactorResponse[]>(Route(patient)))!);
        Assert.Equal(archived, Assert.Single((await a.GetFromJsonAsync<PatientRiskFactorResponse[]>(Route(patient) + "?status=Archived"))!));
        var archivedBefore = await db.Snapshot(false);
        using (var forbidden = await a.PutAsJsonAsync(Route(patient, row.RiskFactorUid), Edit(archived))) Assert.Equal(HttpStatusCode.Conflict, forbidden.StatusCode);
        using (var forbidden = await a.PostAsJsonAsync(Route(patient, row.RiskFactorUid) + "/archive", new { rowVersion = archived.RowVersion })) Assert.Equal(HttpStatusCode.Conflict, forbidden.StatusCode);
        Assert.Equal(archivedBefore, await db.Snapshot(false));

        var until = await db.Clinical.Utc(false);
        var audits = await db.Clinical.Audit(false, row.RiskFactorUid, "PatientRiskFactor");
        Assert.Equal(3, audits.Count);
        foreach (var (action, user, old, current) in new[]
        {
            ("Create", creator, (PatientRiskFactorResponse?)null, row),
            ("Update", editor, row, saved), ("Archive", editor, saved, archived)
        })
        {
            var audit = Assert.Single(audits, x => x.Action == action);
            Assert.Equal(row.RiskFactorUid, audit.Entity); Assert.Equal(patient, audit.Patient);
            Assert.Equal(user.Actor, audit.Actor); Assert.Equal(user.Subject, audit.Subject);
            Assert.InRange(audit.At, from.AddSeconds(-1), until.AddSeconds(1));
            if (old is null) Assert.Null(audit.Old); else JsonEquals(old, audit.Old!);
            JsonEquals(current, audit.New);
        }
    }

    [Fact]
    public async Task BothTenantDirectionsAndForeignPatientReturn404WithoutAnyPersistenceOrDisclosure()
    {
        var patientA = await db.Clinical.SeedPatient(false); var otherA = await db.Clinical.SeedPatient(false);
        var patientB = await db.Clinical.SeedPatient(true);
        var writerA = await db.Clinical.SeedUser(false); var writerB = await db.Clinical.SeedUser(true);
        await using var hostA = await FamilyHistoryAcceptanceHost.Start(db.Clinical, false, writerA);
        await using var hostB = await FamilyHistoryAcceptanceHost.Start(db.Clinical, true, writerB);
        using var a = hostA.Client(writerA); using var b = hostB.Client(writerB);
        var rowA = await Create(a, patientA);
        var rowB = await Create(b, patientB, new() { RiskFactor = "TEST ONLY - asbestos exposure", ExposureDetails = "TEST ONLY - occupational fixture" });
        Assert.Equal(rowA, Assert.Single((await a.GetFromJsonAsync<PatientRiskFactorResponse[]>(Route(patientA)))!));
        Assert.Equal(rowB, Assert.Single((await b.GetFromJsonAsync<PatientRiskFactorResponse[]>(Route(patientB)))!));
        var beforeA = await db.Snapshot(false); var beforeB = await db.Snapshot(true);
        foreach (var (client, patient, foreign) in new[] { (a, otherA, rowA), (b, patientB, rowA), (b, patientA, rowA), (a, patientA, rowB), (a, patientB, rowB) })
        {
            var route = Route(patient, foreign.RiskFactorUid);
            using var get = await client.GetAsync(route);
            using var update = await client.PutAsJsonAsync(route, Edit(foreign));
            using var archive = await client.PostAsJsonAsync(route + "/archive", new { rowVersion = foreign.RowVersion });
            foreach (var response in new[] { get, update, archive })
            {
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                var body = await response.Content.ReadAsStringAsync();
                Assert.DoesNotContain(foreign.RiskFactor, body); Assert.DoesNotContain(foreign.RiskFactorUid.ToString(), body);
                Assert.DoesNotContain(foreign.RowVersion, body);
            }
        }
        Assert.Equal(beforeA, await db.Snapshot(false)); Assert.Equal(beforeB, await db.Snapshot(true));
    }

    [Fact]
    public async Task OptionalFieldsRemainNullAndInvalidSqlOrDeletedPatientCannotMutate()
    {
        var patient = await db.Clinical.SeedPatient(false); var actor = await db.Clinical.SeedUser(false);
        var repo = db.Repository(false);
        var row = await repo.CreateAsync(patient, new() { RiskFactor = " TEST ONLY - exposure ", ExposureDetails = " ", LifeStageCode = "", Notes = " " }, actor.Actor);
        Assert.Equal("TEST ONLY - exposure", row.RiskFactor); Assert.Null(row.ExposureDetails); Assert.Null(row.LifeStageCode); Assert.Null(row.Notes);
        Assert.Equal("Not recorded", row.LifeStage);
        JsonEquals(row, Assert.Single(await db.Clinical.Audit(false, row.RiskFactorUid, "PatientRiskFactor")).New);
        var before = await db.Snapshot(false);
        var invalid = Draft(); invalid.LifeStageCode = "UK";
        await Assert.ThrowsAsync<ArgumentException>(() => repo.CreateAsync(patient, invalid, actor.Actor));
        await Assert.ThrowsAsync<ArgumentException>(() => repo.CreateAsync(patient, new() { RiskFactor = " " }, actor.Actor));
        await Assert.ThrowsAsync<PatientRiskFactorNotFoundException>(() => repo.CreateAsync(Guid.NewGuid(), Draft(), actor.Actor));
        await db.Execute(false, "UPDATE dbo.ApplicationUser SET IsActive=0 WHERE UserId=@Actor", actor.Actor);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repo.CreateAsync(patient, Draft(), actor.Actor));
        Assert.Equal(before, await db.Snapshot(false));
        await db.Execute(false, "UPDATE dbo.Patient SET IsDeleted=1 WHERE PatientUid=@Patient", patient: patient);
        await Assert.ThrowsAsync<PatientRiskFactorNotFoundException>(() => repo.ListAsync(patient, "All"));
        Assert.Null(await repo.UpdateAsync(patient, row.RiskFactorUid, Edit(row), (await db.Clinical.SeedUser(false)).Actor));
        Assert.Equal(before, await db.Snapshot(false));
    }

    [Theory]
    [InlineData("Create")]
    [InlineData("Update")]
    [InlineData("Archive")]
    public async Task AuditFailureRollsBackRiskMutationAndAllAuditState(string action)
    {
        var patient = await db.Clinical.SeedPatient(false); var actor = await db.Clinical.SeedUser(false);
        var repo = db.Repository(false);
        var row = action == "Create" ? null : await repo.CreateAsync(patient, Draft(), actor.Actor);
        var before = await db.Snapshot(false); var otherBefore = await db.Snapshot(true);
        await db.Execute(false, "CREATE TRIGGER dbo.RiskAuditFailure ON dbo.AuditLog AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE EntityName=N'PatientRiskFactor') THROW 52997,'Synthetic risk audit failure.',1; END;");
        try
        {
            var error = await Assert.ThrowsAsync<SqlException>(async () =>
            {
                if (action == "Create") await repo.CreateAsync(patient, Draft(), actor.Actor);
                else if (action == "Update") { var edit = Edit(row!); edit.Notes = "Must roll back"; await repo.UpdateAsync(patient, row!.RiskFactorUid, edit, actor.Actor); }
                else await repo.ArchiveAsync(patient, row!.RiskFactorUid, row.RowVersion, actor.Actor);
            });
            Assert.Equal(52997, error.Number);
        }
        finally { await db.Execute(false, "DROP TRIGGER dbo.RiskAuditFailure;"); }
        Assert.Equal(before, await db.Snapshot(false)); Assert.Equal(otherBefore, await db.Snapshot(true));
    }

    private static void JsonEquals(PatientRiskFactorResponse row, string json)
    {
        using var document = JsonDocument.Parse(json);
        foreach (var (key, value) in new Dictionary<string, string?> { ["RiskFactor"] = row.RiskFactor, ["ExposureDetails"] = row.ExposureDetails, ["LifeStageCode"] = row.LifeStageCode, ["Notes"] = row.Notes, ["Status"] = row.Status })
            Assert.Equal(value, document.RootElement.GetProperty(key).GetString());
    }
}

public sealed class RiskFactorSqlDatabase : IAsyncLifetime
{
    internal FamilyHistoryAcceptanceDatabase Clinical { get; } = new();
    public async Task InitializeAsync()
    {
        try
        {
            await Clinical.InitializeAsync();
            var sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "database", "tenant-clinical", "migrations", "0076-patient-risk-factors.sql"));
            foreach (var other in new[] { false, true })
            {
                await using var connection = await Clinical.Open(other);
                foreach (var batch in Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(batch)) continue;
                    await using var command = new SqlCommand(batch, connection); await command.ExecuteNonQueryAsync();
                }
            }
        }
        catch { await Clinical.DisposeAsync(); throw; }
    }
    public Task DisposeAsync() => Clinical.DisposeAsync();
    internal PatientRiskFactorRepository Repository(bool other) => new(Clinical.Factory(other), NullLogger<PatientRiskFactorRepository>.Instance);
    internal async Task Execute(bool other, string sql, long? actor = null, Guid? patient = null)
    {
        await using var connection = await Clinical.Open(other); await using var command = new SqlCommand(sql, connection);
        if (actor.HasValue) command.Parameters.Add("@Actor", SqlDbType.BigInt).Value = actor.Value;
        if (patient.HasValue) command.Parameters.Add("@Patient", SqlDbType.UniqueIdentifier).Value = patient.Value;
        await command.ExecuteNonQueryAsync();
    }
    internal async Task<string> Snapshot(bool other)
    {
        var existing = await Clinical.Snapshot(other); // Includes every family-history row and all clinical audits.
        await using var connection = await Clinical.Open(other);
        await using var command = new SqlCommand("SELECT * FROM dbo.PatientRiskFactor ORDER BY RiskFactorUid FOR JSON PATH,INCLUDE_NULL_VALUES", connection);
        await using var reader = await command.ExecuteReaderAsync(); var json = new System.Text.StringBuilder(existing);
        while (await reader.ReadAsync()) json.Append(reader.GetString(0)); return json.ToString();
    }
}
