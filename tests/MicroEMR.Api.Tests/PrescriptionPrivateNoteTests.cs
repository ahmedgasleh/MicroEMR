using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientCpp;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Application.ReadAudit;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PrescriptionPrivateNoteTests(PrescriptionPrivateNoteFixture fixture) : IClassFixture<PrescriptionPrivateNoteFixture>
{
    private PrescriptionDateDatabase Db => fixture.Db;
    private const string Secret = "TEST ONLY PRIVATE 121 <private>& clinical";
    private static string Route(Guid patient, Guid? prescription = null) => $"api/patients/{patient}/prescriptions" + (prescription is {} uid ? $"/{uid}" : "");
    private static CreatePrescriptionDraftRequest Draft(string? text = Secret)
    {
        var r = PrescriptionDatePrecisionTests.Draft(); r.PrivateNote = text;
        r.NotesToPharmacist = "TEST ONLY PHARMACIST 121"; r.Indication = "TEST ONLY indication"; return r;
    }
    private static JsonObject Edit(PatientPrescriptionResponse row, bool specified = true, string? text = Secret)
    {
        var body = JsonSerializer.SerializeToNode(Draft(text), new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
        body["rowVersion"] = row.RowVersion; if (!specified) body.Remove("privateNote"); return body;
    }
    private static async Task<PatientPrescriptionResponse> Row(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        Assert.DoesNotContain(Secret, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;
    }
    private static async Task<PrescriptionPrivateNoteDetail> Note(HttpClient c, Guid p, Guid uid) => (await c.GetFromJsonAsync<PrescriptionPrivateNoteDetail>(Route(p, uid) + "/private-note"))!;
    private Task<int> Count(Guid uid, string action) => Db.Stores.AuditCount(uid, action);
    private Task<int> Revisions(Guid uid) => Db.Stores.Read<int>("SELECT COUNT(*) FROM dbo.PatientPrescriptionPrivateNoteRevision r JOIN dbo.PatientPrescriptionPrivateNote n ON n.PrivateNoteUid=r.PrivateNoteUid WHERE n.PrescriptionUid=@Uid", uid);

    [Fact]
    public async Task OriginalAuthorWithoutPrivateReadCanStillMakeOrdinaryOmittedFieldUpdate()
    {
        var p=await Db.Stores.SeedPatient();await using var host=await fixture.Host();using var c=host.Client();
        var row=await Row(await c.PostAsJsonAsync(Route(p),Draft()));
        async Task Permission(string state)
        {
            await using var connection=new SqlConnection(fixture.PlatformConnection);await connection.OpenAsync();
            await using var q=new SqlCommand("UPDATE dbo.UserPermissionOverride SET OverrideState=@State WHERE TenantUid=@Tenant AND UserId=N'writer' AND PermissionKey=N'Prescriptions.ViewPrivateNotes'",connection);
            q.Parameters.AddWithValue("@Tenant",Db.Tenant(false).TenantUid);q.Parameters.AddWithValue("@State",state);await q.ExecuteNonQueryAsync();
        }
        try
        {
            await Permission("Deny");
            var saved=await Row(await c.PutAsJsonAsync(Route(p,row.PrescriptionUid),Edit(row,false)));
            Assert.NotEqual(row.RowVersion,saved.RowVersion);
            Assert.Equal(HttpStatusCode.Forbidden,(await c.GetAsync(Route(p,row.PrescriptionUid)+"/private-note")).StatusCode);
        }
        finally {await Permission("Allow");}
        Assert.Equal(Secret,(await Note(c,p,row.PrescriptionUid)).NoteText);Assert.Equal(1,await Revisions(row.PrescriptionUid));
    }

    [Fact]
    public async Task OptInPlatformPermissionCanBeGrantedAndRevokedWithoutImplicitAccess()
    {
        var p=await Db.Stores.SeedPatient();await using var host=await fixture.Host();using var writer=host.Client();using var staff=host.Client("no-private");
        var row=await Row(await writer.PostAsJsonAsync(Route(p),Draft()));
        Assert.Equal(HttpStatusCode.Forbidden,(await staff.GetAsync(Route(p,row.PrescriptionUid)+"/private-note")).StatusCode);
        await using(var platform=new SqlConnection(fixture.PlatformConnection))
        {
            await platform.OpenAsync();await using var check=new SqlCommand("SELECT COUNT(*) FROM dbo.PlatformSecurityAuditEvent WHERE ActorSubject=N'no-private' AND Capability=N'PrescriptionPrivateNoteView' AND RequiredPermission=N'Prescriptions.ViewPrivateNotes' AND Outcome='Denied'",platform);
            Assert.True((int)(await check.ExecuteScalarAsync())!>0);
        }
        async Task Set(string state)
        {
            await using var c=new SqlConnection(fixture.PlatformConnection);await c.OpenAsync();
            await using var q=new SqlCommand("DECLARE @Version binary(8)=(SELECT RowVersion FROM dbo.UserTenantMembership WHERE TenantUid=@Tenant AND UserId=N'no-private'); EXEC dbo.UserPermissionOverride_Set @TenantUid=@Tenant,@UserId=N'no-private',@PermissionKey=N'Prescriptions.ViewPrivateNotes',@OverrideState=@State,@ExpectedMembershipRowVersion=@Version,@ActorUserId=N'writer';",c);
            q.Parameters.AddWithValue("@Tenant",Db.Tenant(false).TenantUid);q.Parameters.AddWithValue("@State",state);await q.ExecuteNonQueryAsync();
        }
        try {await Set("Allow");Assert.Equal(Secret,(await Note(staff,p,row.PrescriptionUid)).NoteText);}
        finally {await Set("Inherit");}
        Assert.Equal(HttpStatusCode.Forbidden,(await staff.GetAsync(Route(p,row.PrescriptionUid)+"/private-note/history")).StatusCode);
    }

    [Fact]
    public async Task ProviderDeactivationBlocksWritingButDoesNotHideHistoricalNotesFromActiveStaff()
    {
        var p=await Db.Stores.SeedPatient();await using var host=await fixture.Host();using var c=host.Client();using var staff=host.Client("staff");
        var row=await Row(await c.PostAsJsonAsync(Route(p),Draft()));
        await Db.Stores.Execute("UPDATE dbo.Provider SET IsActive=0 WHERE ProviderId=1");
        try
        {
            Assert.Equal(HttpStatusCode.Conflict,(await c.PutAsJsonAsync(Route(p,row.PrescriptionUid),Edit(row))).StatusCode);
            Assert.Equal(Secret,(await Note(staff,p,row.PrescriptionUid)).NoteText);
            Assert.False((await Note(c,p,row.PrescriptionUid)).CanEdit);
        }
        finally {await Db.Stores.Execute("UPDATE dbo.Provider SET IsActive=1 WHERE ProviderId=1");}
        Assert.Equal(row.RowVersion,(await Db.Repository().GetAsync(p,row.PrescriptionUid))!.RowVersion);
        await Row(await c.PostAsJsonAsync(Route(p,row.PrescriptionUid)+"/sign-and-finalize",new{row.RowVersion,ConfirmSignature=true}));
        await Db.Stores.Execute("UPDATE dbo.ApplicationUser SET IsActive=0 WHERE UserId=7");
        try
        {
            Assert.Equal(Secret,(await Note(staff,p,row.PrescriptionUid)).NoteText);
            Assert.Equal(HttpStatusCode.Forbidden,(await c.GetAsync(Route(p,row.PrescriptionUid)+"/private-note")).StatusCode);
        }
        finally {await Db.Stores.Execute("UPDATE dbo.ApplicationUser SET IsActive=1 WHERE UserId=7");}
    }

    [Fact]
    public async Task PrivateContentCannotBeReadOrWrittenDirectlyByProcedureOnlyDatabasePrincipal()
    {
        var p=await Db.Stores.SeedPatient();await using var host=await fixture.Host();using var c=host.Client();
        var row=await Row(await c.PostAsJsonAsync(Route(p),Draft()));
        await Db.Stores.Execute("CREATE USER Step121ProcedureOnly WITHOUT LOGIN; GRANT EXECUTE ON dbo.PatientPrescriptionPrivateNote_Read TO Step121ProcedureOnly; GRANT EXECUTE ON dbo.PatientPrescription_UpdateDraft TO Step121ProcedureOnly;");
        try
        {
            // Every command gets a new connection, so an expected error cannot leave impersonation active.
            foreach(var sql in new[]{"SELECT * FROM dbo.PatientPrescriptionPrivateNote", "SELECT * FROM dbo.PatientPrescriptionPrivateNoteRevision", "EXEC dbo.PatientPrescriptionPrivateNote_Write @PatientUid=NULL,@PrescriptionUid=NULL,@ActorUserId=7,@Note=NULL,@Correlation=N'test'"})
                await Assert.ThrowsAsync<SqlException>(()=>Db.Stores.Execute("EXECUTE AS USER=N'Step121ProcedureOnly'; "+sql+"; REVERT;"));
            await using var connection=await Db.Factory().OpenConnectionAsync();
            await using var q=new SqlCommand("EXECUTE AS USER=N'Step121ProcedureOnly'; EXEC dbo.PatientPrescriptionPrivateNote_Read @PatientUid=@Patient,@PrescriptionUid=@Uid,@ActorUserId=7,@Correlation=N'step121-principal'; REVERT;",connection);
            q.Parameters.AddWithValue("@Patient",p);q.Parameters.AddWithValue("@Uid",row.PrescriptionUid);
            await using var reader=await q.ExecuteReaderAsync();Assert.True(await reader.ReadAsync());Assert.Equal(Secret,reader.GetString(2));
        }
        finally {await Db.Stores.Execute("DROP USER Step121ProcedureOnly");}
    }

    [Fact]
    public async Task ClinicalAuditCorrelationAndRepresentativeReportCsvExcludeProtectedContent()
    {
        var p=await Db.Stores.SeedPatient();await using var host=await fixture.Host();using var c=host.Client();
        var row=await Row(await c.PostAsJsonAsync(Route(p),Draft()));
        var correlated=await Db.Stores.Read<int>("SELECT COUNT(*) FROM dbo.AuditLog a JOIN dbo.PatientPrescriptionPrivateNoteRevision r ON r.AuditEventUid=a.AuditEventUid JOIN dbo.AuditLog parent ON parent.RequestCorrelationId=a.RequestCorrelationId AND parent.ActionName=N'PrescriptionDraftCreated' WHERE a.EntityId=CONVERT(nvarchar(100),@Uid) AND a.UserId=7 AND a.Outcome='Succeeded'",row.PrescriptionUid);
        Assert.Equal(1,correlated);
        Assert.DoesNotContain(typeof(PatientCppMedication).GetProperties(),property=>property.Name.Contains("Private",StringComparison.OrdinalIgnoreCase));
        var report=new MicroEMR.Application.Reporting.AppointmentStatusReport(new(2026,10,19),new(2026,10,19),"UTC",1,[],
            [new(Guid.NewGuid(),new(2026,10,19,12,0,0),new(2026,10,19,12,30,0),p,"TEST ONLY patient","TEST","TEST ONLY provider","Booked")]);
        var service=new MicroEMR.Application.Reporting.AppointmentStatusReportService(null!,null!,null!);
        var json=JsonSerializer.Serialize(report);var csv=Encoding.UTF8.GetString(service.CreateCsv(report));
        Assert.DoesNotContain(Secret,json);Assert.DoesNotContain(Secret,csv);Assert.DoesNotContain("PrivateNote",json);Assert.Contains("TEST ONLY patient",csv);
    }

    [Fact]
    public void OmissionAndClearSurviveTransportWithoutChangingSharedResponse()
    {
        var omitted = JsonSerializer.Serialize(new PrescriptionDraftRequest()); Assert.DoesNotContain("privateNote", omitted);
        var clear = JsonSerializer.Serialize(new PrescriptionDraftRequest { PrivateNote = null }); Assert.Contains("\"privateNote\":null", clear);
        Assert.True(JsonSerializer.Deserialize<PrescriptionDraftRequest>(clear)!.PrivateNoteSpecified);
        Assert.False(JsonSerializer.Deserialize<PrescriptionDraftRequest>(omitted)!.PrivateNoteSpecified);
        Assert.DoesNotContain(typeof(PatientPrescriptionResponse).GetProperties(), p => p.Name.Contains("Private", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(Secret)] [InlineData(null)] [InlineData("  ")]
    public async Task CreateIsAtomicAndNullRemainsHonest(string? text)
    {
        var p = await Db.Stores.SeedPatient(); await using var host = await fixture.Host(); using var c = host.Client();
        var row = await Row(await c.PostAsJsonAsync(Route(p), Draft(text)));
        var note = await Note(c, p, row.PrescriptionUid); Assert.Equal(string.IsNullOrWhiteSpace(text) ? null : text, note.NoteText); Assert.True(note.CanEdit);
        Assert.Equal(row.RowVersion, note.RowVersion); Assert.Equal(7, row.PrescriberUserId);
        Assert.Equal(text == Secret ? 1 : 0, await Revisions(row.PrescriptionUid));
        Assert.Equal(1, await Count(row.PrescriptionUid, text == Secret ? "PrescriptionPrivateNoteCreated" : "PrescriptionPrivateNoteCleared"));
        Assert.Equal(1, await Count(row.PrescriptionUid, "PrescriptionPrivateNoteViewed"));
    }

    [Fact]
    public async Task EditOmissionClearAndRetainedHistoryUseOneParentVersion()
    {
        var p = await Db.Stores.SeedPatient(); await using var host = await fixture.Host(); using var c = host.Client();
        var row = await Row(await c.PostAsJsonAsync(Route(p), Draft()));
        var preserved = await Row(await c.PutAsJsonAsync(Route(p, row.PrescriptionUid), Edit(row, false)));
        Assert.Equal(Secret, (await Note(c, p, row.PrescriptionUid)).NoteText); Assert.Equal(1, await Revisions(row.PrescriptionUid));
        var changed = await Row(await c.PutAsJsonAsync(Route(p, row.PrescriptionUid), Edit(preserved, true, "TEST ONLY amended")));
        Assert.NotEqual(preserved.RowVersion, changed.RowVersion);
        var clear = await Row(await c.PutAsJsonAsync(Route(p, row.PrescriptionUid), Edit(changed, true, null)));
        Assert.Null((await Note(c, p, row.PrescriptionUid)).NoteText);
        var history = (await c.GetFromJsonAsync<PrescriptionPrivateNoteRevision[]>(Route(p, row.PrescriptionUid) + "/private-note/history"))!;
        Assert.Equal(["Create", "Update", "Clear"], history.Select(x => x.Operation).ToArray()); Assert.Equal(Secret, history[0].NoteText); Assert.Null(history[2].NoteText);
        Assert.All(history, r => Assert.Equal(7, r.ActorUserId)); Assert.NotEqual(changed.RowVersion, clear.RowVersion);
        Assert.Equal(1, await Count(row.PrescriptionUid, "PrescriptionPrivateNoteHistoryViewed"));
        var metadata = await Db.Stores.Read<string>("SELECT STRING_AGG(CONVERT(nvarchar(max),COALESCE(OldValue,N'')+COALESCE(NewValue,N'')),N'|') FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@Uid)", row.PrescriptionUid);
        Assert.DoesNotContain(Secret, metadata); Assert.DoesNotContain("TEST ONLY amended", metadata); Assert.Contains("BeforeRevisionUid", metadata); Assert.Contains("AfterRowVersion", metadata);
    }

    [Theory]
    [InlineData("chart")] [InlineData("no-private")] [InlineData("no-view")]
    [InlineData("inactive")] [InlineData("unresolved")] [InlineData("inactive-membership")]
    public async Task ForbiddenReadsAndWritesHaveNoPrivateSuccessAudit(string identity)
    {
        var p = await Db.Stores.SeedPatient(); await using var host = await fixture.Host(); using var writer = host.Client(); using var c = host.Client(identity);
        var row = await Row(await writer.PostAsJsonAsync(Route(p), Draft()));
        foreach (var suffix in new[] { "/private-note", "/private-note/history" })
        { var response = await c.GetAsync(Route(p, row.PrescriptionUid) + suffix); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); Assert.DoesNotContain(Secret, await response.Content.ReadAsStringAsync()); }
        var write = await c.PutAsJsonAsync(Route(p, row.PrescriptionUid), Edit(row)); Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        Assert.Equal(0, await Count(row.PrescriptionUid, "PrescriptionPrivateNoteViewed")); Assert.Equal(0, await Count(row.PrescriptionUid, "PrescriptionDraftUpdated"));
        Assert.Equal(3, await Count(row.PrescriptionUid, "PrescriptionAccessDenied")); Assert.Equal(1, await Revisions(row.PrescriptionUid));
    }

    [Fact]
    public async Task AuthorizedNonPrescribingStaffReadsButCannotWriteAndOtherPrescriberCannotEdit()
    {
        var p = await Db.Stores.SeedPatient(); await using var host = await fixture.Host(); using var c = host.Client(); using var staff = host.Client("staff"); using var other = host.Client("other");
        var row = await Row(await c.PostAsJsonAsync(Route(p), Draft()));
        var note = await Note(staff, p, row.PrescriptionUid); Assert.Equal(Secret, note.NoteText); Assert.False(note.CanEdit);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PutAsJsonAsync(Route(p, row.PrescriptionUid), Edit(row))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PutAsJsonAsync(Route(p, row.PrescriptionUid), Edit(row))).StatusCode);
        using var noPrivate = host.Client("no-private"); var absent = Edit(row, true, null);
        Assert.Equal(HttpStatusCode.Forbidden, (await noPrivate.PostAsJsonAsync(Route(p), Draft(null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await noPrivate.PutAsJsonAsync(Route(p, row.PrescriptionUid), absent)).StatusCode);
        Assert.Equal(row.RowVersion, (await Db.Repository().GetAsync(p, row.PrescriptionUid))!.RowVersion);
    }

    [Fact]
    public async Task CrossPatientAndBothTenantDirectionsRejectWithoutForeignReadAudit()
    {
        var p = await Db.Stores.SeedPatient(); var wrong = await Db.Stores.SeedPatient(); var p2 = await Db.SeedOtherPatient();
        await using var a = await fixture.Host(); await using var b = await fixture.Host(true); using var ca = a.Client(); using var cb = b.Client();
        var row = await Row(await ca.PostAsJsonAsync(Route(p), Draft())); var row2 = await Row(await cb.PostAsJsonAsync(Route(p2), Draft()));
        foreach (var attempt in new[] { (ca, wrong, row), (cb, p, row), (ca, p2, row2) })
        {
            foreach (var suffix in new[] { "/private-note", "/private-note/history" })
                Assert.Equal(HttpStatusCode.NotFound, (await attempt.Item1.GetAsync(Route(attempt.Item2, attempt.Item3.PrescriptionUid) + suffix)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await attempt.Item1.PutAsJsonAsync(Route(attempt.Item2, attempt.Item3.PrescriptionUid), Edit(attempt.Item3))).StatusCode);
        }
        Assert.Equal(0, await Count(row.PrescriptionUid, "PrescriptionPrivateNoteViewed")); Assert.Equal(1, await Revisions(row.PrescriptionUid));
    }

    [Fact]
    public async Task StaleMalformedAndConcurrentWritesCannotLoseNotes()
    {
        var p = await Db.Stores.SeedPatient(); await using var host = await fixture.Host(); using var c = host.Client();
        var row = await Row(await c.PostAsJsonAsync(Route(p), Draft()));
        var responses = await Task.WhenAll(c.PutAsJsonAsync(Route(p, row.PrescriptionUid), Edit(row, true, "TEST ONLY race A")), c.PutAsJsonAsync(Route(p, row.PrescriptionUid), Edit(row, true, "TEST ONLY race B")));
        Assert.Single(responses, x => x.IsSuccessStatusCode); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(2, await Revisions(row.PrescriptionUid)); Assert.Equal(1, await Count(row.PrescriptionUid, "PrescriptionDraftUpdated"));
        var invalid = Edit(row); invalid["rowVersion"] = Convert.ToBase64String(new byte[9]);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync(Route(p, row.PrescriptionUid), invalid)).StatusCode);
        var oversized = Draft(new string('x', 1001)); Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Route(p), oversized)).StatusCode);
    }

    [Theory]
    [InlineData("finalize")] [InlineData("sign-and-finalize")]
    public async Task FinalizationAndEditRaceSerializeAndProtectAllOutwardArtifacts(string action)
    {
        var p = await Db.Stores.SeedPatient(); await using var host = await fixture.Host(); using var c = host.Client();
        var row = await Row(await c.PostAsJsonAsync(Route(p), Draft()));
        var race = await Task.WhenAll(c.PostAsJsonAsync(Route(p, row.PrescriptionUid) + "/" + action, new { row.RowVersion, ConfirmSignature = true }), c.PutAsJsonAsync(Route(p, row.PrescriptionUid), Edit(row)));
        Assert.Single(race, x => x.IsSuccessStatusCode); Assert.Single(race, x => x.StatusCode == HttpStatusCode.Conflict);
        var saved = (await Db.Repository().GetAsync(p, row.PrescriptionUid))!;
        if (saved.Status == "Draft") saved = await Row(await c.PostAsJsonAsync(Route(p, row.PrescriptionUid) + "/" + action, new { saved.RowVersion, ConfirmSignature = true }));
        var before = await Db.Repository().GetArtifactAsync(p, row.PrescriptionUid);
        Assert.DoesNotContain(Secret, before!.Json); Assert.DoesNotContain("PrivateNote", before.Json);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync(Route(p, row.PrescriptionUid), Edit(saved, true, null))).StatusCode);
        Assert.False((await Note(c, p, row.PrescriptionUid)).CanEdit);
        var output = Encoding.UTF8.GetString(await c.GetByteArrayAsync(Route(p, row.PrescriptionUid) + "/artifact"));
        Assert.DoesNotContain(Secret, output); Assert.Contains("TEST ONLY", output);
        if (action == "sign-and-finalize") Assert.Contains("TEST ONLY PHARMACIST 121", output);
        Assert.Equal(before, await Db.Repository().GetArtifactAsync(p, row.PrescriptionUid));
    }

    [Fact]
    public async Task DraftCancellationRejectedFinalizedCancellationRetainsProtectedContentAndArtifact()
    {
        var p = await Db.Stores.SeedPatient(); await using var host = await fixture.Host(); using var c = host.Client(); using var other = host.Client("other");
        var row = await Row(await c.PostAsJsonAsync(Route(p), Draft()));
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(Route(p, row.PrescriptionUid) + "/cancel", new { row.RowVersion, Reason = "TEST ONLY" })).StatusCode);
        Assert.Equal(row.RowVersion, (await Db.Repository().GetAsync(p, row.PrescriptionUid))!.RowVersion);
        var final = await Row(await c.PostAsJsonAsync(Route(p, row.PrescriptionUid) + "/sign-and-finalize", new { row.RowVersion, ConfirmSignature = true }));
        var artifact = await Db.Repository().GetArtifactAsync(p, row.PrescriptionUid);
        Assert.Equal(HttpStatusCode.Conflict, (await other.PostAsJsonAsync(Route(p, row.PrescriptionUid) + "/cancel", new { final.RowVersion, Reason = "TEST ONLY" })).StatusCode);
        var cancelled = await Row(await c.PostAsJsonAsync(Route(p, row.PrescriptionUid) + "/cancel", new { final.RowVersion, Reason = "TEST ONLY" }));
        Assert.Equal("Cancelled", cancelled.Status); Assert.Equal(Secret, (await Note(c, p, row.PrescriptionUid)).NoteText);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync(Route(p, row.PrescriptionUid), Edit(cancelled))).StatusCode);
        Assert.Equal(1, await Revisions(row.PrescriptionUid)); Assert.Equal(artifact, await Db.Repository().GetArtifactAsync(p, row.PrescriptionUid));
    }

    [Theory]
    [InlineData("writer")] [InlineData("other")]
    public async Task CorrectionsHaveIndependentAuthorsNoCopyAndRetainSupersededOriginal(string creator)
    {
        var p = await Db.Stores.SeedPatient(); await using var host = await fixture.Host(); using var c = host.Client(); using var correctionActor = host.Client(creator);
        var row = await Row(await c.PostAsJsonAsync(Route(p), Draft()));
        var final = await Row(await c.PostAsJsonAsync(Route(p, row.PrescriptionUid) + "/sign-and-finalize", new { row.RowVersion, ConfirmSignature = true }));
        var artifact = await Db.Repository().GetArtifactAsync(p, row.PrescriptionUid);
        var correction = await Row(await correctionActor.PostAsJsonAsync(Route(p, row.PrescriptionUid) + "/correction", new { final.RowVersion }));
        Assert.Null((await Note(correctionActor, p, correction.PrescriptionUid)).NoteText); Assert.Equal(0, await Revisions(correction.PrescriptionUid));
        Assert.Equal(creator == "writer" ? 7 : 8, correction.PrescriberUserId);
        correction = await Row(await correctionActor.PutAsJsonAsync(Route(p, correction.PrescriptionUid), Edit(correction, true, "TEST ONLY correction note")));
        await Row(await correctionActor.PostAsJsonAsync(Route(p, correction.PrescriptionUid) + "/sign-and-finalize", new { correction.RowVersion, ConfirmSignature = true }));
        var original = (await Db.Repository().GetAsync(p, row.PrescriptionUid))!; Assert.Equal("Superseded", original.Status);
        Assert.Equal(Secret, (await Note(c, p, row.PrescriptionUid)).NoteText); Assert.Equal(artifact, await Db.Repository().GetArtifactAsync(p, row.PrescriptionUid));
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync(Route(p, row.PrescriptionUid), Edit(original))).StatusCode);
    }

    [Theory]
    [InlineData("create")] [InlineData("update")] [InlineData("clear")] [InlineData("parent")] [InlineData("revision")]
    public async Task MandatoryAuditOrRevisionFailureRollsBackWholeMutation(string failure)
    {
        var p = await Db.Stores.SeedPatient(); await using var host = await fixture.Host(); using var c = host.Client();
        var row = await Row(await c.PostAsJsonAsync(Route(p), Draft()));
        var table = failure == "revision" ? "PatientPrescriptionPrivateNoteRevision" : "AuditLog";
        var predicate = failure == "parent" ? "WHERE ActionName=N'PrescriptionDraftUpdated'" : failure == "revision" ? "" : "WHERE ActionName LIKE N'PrescriptionPrivateNote%'";
        await Db.Stores.Execute($"CREATE TRIGGER dbo.Step121Fault ON dbo.{table} AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted {predicate}) THROW 53999,'TEST ONLY fault',1; END");
        try
        {
            var response = failure == "create" ? await c.PostAsJsonAsync(Route(p), Draft()) : await c.PutAsJsonAsync(Route(p, row.PrescriptionUid), Edit(row, true, failure == "clear" ? null : "TEST ONLY failed update"));
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode); Assert.DoesNotContain(Secret, await response.Content.ReadAsStringAsync());
        }
        finally { await Db.Stores.Execute($"DROP TRIGGER dbo.Step121Fault"); }
        Assert.Equal(row.RowVersion, (await Db.Repository().GetAsync(p, row.PrescriptionUid))!.RowVersion); Assert.Equal(1, await Revisions(row.PrescriptionUid));
        Assert.Equal(Secret, (await Note(c, p, row.PrescriptionUid)).NoteText); Assert.Single(await Db.Repository().ListAsync(p)); Assert.Equal(0, await Count(row.PrescriptionUid, "PrescriptionDraftUpdated"));
    }

    [Fact]
    public async Task ReadAuditFailureReleasesNothingAndOrdinaryLogsAndAuditsContainNoText()
    {
        var p = await Db.Stores.SeedPatient(); await using var host = await fixture.Host(); using var c = host.Client();
        var row = await Row(await c.PostAsJsonAsync(Route(p), Draft()));
        await Db.Stores.Execute("CREATE TRIGGER dbo.Step121ReadFault ON dbo.AuditLog AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE EventCategory=N'ClinicalRead' AND ActionName LIKE N'PrescriptionPrivateNote%') THROW 53999,'TEST ONLY read fault',1; END");
        try
        {
            foreach (var suffix in new[] { "/private-note", "/private-note/history" })
            { var response = await c.GetAsync(Route(p, row.PrescriptionUid) + suffix); Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode); Assert.DoesNotContain(Secret, await response.Content.ReadAsStringAsync()); }
        }
        finally { await Db.Stores.Execute("DROP TRIGGER dbo.Step121ReadFault"); }
        Assert.Equal(0, await Count(row.PrescriptionUid, "PrescriptionPrivateNoteViewed"));
        Assert.DoesNotContain(fixture.Logs.Messages, message => message.Contains(Secret));
        await Db.Stores.Execute("CREATE TRIGGER dbo.Step121DeniedFault ON dbo.AuditLog AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE Outcome='Denied') THROW 53999,'TEST ONLY denial fault',1; END");
        try { using var chart = host.Client("chart"); Assert.Equal(HttpStatusCode.Forbidden, (await chart.GetAsync(Route(p, row.PrescriptionUid) + "/private-note")).StatusCode); }
        finally { await Db.Stores.Execute("DROP TRIGGER dbo.Step121DeniedFault"); }
        Assert.Contains(fixture.Logs.Messages, message => message.Contains("denial audit unavailable"));
    }

    [Fact]
    public async Task SharedResponsesCppSelectedPrintAndLegacyArtifactsNeverAcquirePrivateContent()
    {
        var p = await Db.Stores.SeedPatient(); await using var host = await fixture.Host(); using var c = host.Client();
        var row = await Row(await c.PostAsJsonAsync(Route(p), Draft()));
        await Row(await c.PostAsJsonAsync(Route(p, row.PrescriptionUid) + "/finalize", new { row.RowVersion }));
        foreach (var route in new[] { Route(p), Route(p, row.PrescriptionUid) })
        { var json = await c.GetStringAsync(route); Assert.DoesNotContain("privateNote", json, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain(Secret, json); }
        var service = new PatientPrescriptionService(Db.Repository(), new PrescriptionDatePrecisionTests.Capture());
        var readAudit = CppSelectivePrintTests.Stub<IPatientChartReadAuditService>((_, _) => Task.FromResult(Guid.NewGuid()));
        var source = CppSelectivePrintTests.Source(p, null!, readAudit, prescriptions: service);
        var f = new CppSelectivePrintTests.Fixture(); f.Patient.PatientUid = p; f.ActualSource = source;
        var cpp = await source.GetForPrintAsync(p, ["Prescriptions"], "step121");
        Assert.DoesNotContain("PrivateNote", JsonSerializer.Serialize(cpp)); Assert.DoesNotContain(Secret, JsonSerializer.Serialize(cpp));
        Assert.DoesNotContain(Secret, Encoding.UTF8.GetString((await f.Print("Prescriptions"))!));
        foreach (var item in new[] { (Db.Stores.LegacyPatient, Db.Stores.LegacyPrescription, Db.Stores.LegacyJson, Db.Stores.LegacyRowVersion), (Db.Stores.PriorSignedPatient, Db.Stores.PriorSignedPrescription, Db.Stores.PriorSignedJson, Db.Stores.PriorSignedRowVersion) })
        {
            var artifact = (await Db.Repository().GetArtifactAsync(item.Item1, item.Item2))!;
            Assert.Equal(Encoding.UTF8.GetBytes(item.Item3), Encoding.UTF8.GetBytes(artifact.Json));
            Assert.Equal(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(item.Item3)), System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(artifact.Json)));
            Assert.Equal(item.Item4, (await Db.Repository().GetAsync(item.Item1, item.Item2))!.RowVersion);
            Assert.Null((await Note(c, item.Item1, item.Item2)).NoteText);
        }
    }

    [Fact]
    public async Task DirectSqlRejectsOversizeStaleAndImmutableHistory()
    {
        var p = await Db.Stores.SeedPatient(); await using var host = await fixture.Host(); using var c = host.Client();
        var row = await Row(await c.PostAsJsonAsync(Route(p), Draft()));
        await using var connection = await Db.Factory().OpenConnectionAsync();
        await using var command = new SqlCommand("EXEC dbo.PatientPrescription_UpdateDraft @PatientUid=@Patient,@PrescriptionUid=@Uid,@ProductName=N'Test',@ProductDisplayText=N'Test',@Route=N'Oral',@FrequencyCode=N'ONCE',@FrequencyDisplay=N'Once',@Prn=0,@Directions=N'Test',@Quantity=1,@QuantityUnit=N'tablet',@AuthorizedRepeats=0,@ActorUserId=7,@RowVersion=@Version,@PrivateNote=@Text,@PrivateNoteSpecified=1,@PrivateNoteCorrelationId=N'step121-direct'", connection);
        command.Parameters.AddWithValue("@Patient", p); command.Parameters.AddWithValue("@Uid", row.PrescriptionUid); command.Parameters.AddWithValue("@Version", Convert.FromBase64String(row.RowVersion)); command.Parameters.AddWithValue("@Text", new string('x', 1001));
        Assert.Equal(53000, (await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync())).Number);
        command.Parameters["@Text"].Value = "TEST ONLY"; command.Parameters["@Version"].Value = new byte[8];
        Assert.Equal(51504, (await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync())).Number);
        Assert.Equal(row.RowVersion, (await Db.Repository().GetAsync(p, row.PrescriptionUid))!.RowVersion);
        await Assert.ThrowsAsync<SqlException>(() => Db.Stores.Execute("UPDATE dbo.PatientPrescriptionPrivateNoteRevision SET NoteText=NULL"));
        await Assert.ThrowsAsync<SqlException>(() => Db.Stores.Execute("DELETE dbo.PatientPrescriptionPrivateNote"));
    }
}
