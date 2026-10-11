using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.PatientEncounters;
using MicroEMR.Infrastructure.PatientEncounters;
using Xunit;

namespace MicroEMR.Api.Tests;

// Extend the established isolated two-tenant fixture; never use an application database.
public sealed class EncounterContributionSqlTests(ContributionSqlDatabase db) : IClassFixture<ContributionSqlDatabase>
{
    [DiagnosisSqlFact]
    public async Task RawReplacementClientsCannotOverwriteLegacyContent()
    {
        var (p,e)=await db.Seed();
        await db.Inner.Execute("UPDATE dbo.PatientEncounter SET EncounterNotes=N'Legacy free text' WHERE EncounterUid=@p",e);
        await Assert.ThrowsAsync<EncounterNoteNotEditableException>(()=>db.Notes.UpdateSoapNoteAsync(p,e,new() {SubjectiveNote="Replacement"},7));
        await Assert.ThrowsAsync<EncounterNoteNotEditableException>(()=>db.Notes.UpdateNoteAsync(p,e,new() {Notes="Replacement"},8));
        var note=(await db.Notes.GetByUidAsync(e))!;
        Assert.Equal("Preserved SOAP",note.SubjectiveNote);Assert.Equal("Legacy free text",note.Notes);
        Assert.Empty(note.Contributions);
    }

    [DiagnosisSqlFact]
    public async Task FieldsPreserveBaselineAuthorshipNoOpsRevisionsAndClears()
    {
        var (p,e)=await SeedTemplate();
        var first=(await FieldSave(p,e,"{\"history\":\"Legacy history\",\"pulse\":80}",7))!;
        var block=Assert.Single(first.Contributions);Assert.Equal("pulse",block.FieldKey);
        Assert.Equal("Value",block.ValueKind);Assert.Equal(7,block.OriginalContributorId);Assert.Equal("null",block.PriorValueJson);
        var uid=block.ContributionUid;var timestamp=block.CreatedAtUtc;
        var same=(await FieldSave(p,e,"{\"history\":\"Legacy history\",\"pulse\":80.0}",8))!;
        Assert.Single(same.Contributions[0].Revisions);
        var revised=(await FieldSave(p,e,"{\"history\":\"Legacy history\",\"pulse\":81,\"flag\":false}",8))!;
        block=revised.Contributions.Single(c=>c.FieldKey=="pulse");
        Assert.Equal(uid,block.ContributionUid);Assert.Equal(timestamp,block.CreatedAtUtc);Assert.Equal(7,block.OriginalContributorId);
        Assert.Equal("80",block.Revisions[0].Content);Assert.Equal("81",block.Content);Assert.Equal(8,block.Revisions[1].EditorId);
        Assert.Equal("false",revised.Contributions.Single(c=>c.FieldKey=="flag").Content);
        var cleared=(await FieldSave(p,e,"{\"history\":\"Legacy history\",\"flag\":false}",7))!;
        Assert.Equal("null",cleared.Contributions.Single(c=>c.FieldKey=="pulse").Content);
        Assert.Equal(3,cleared.Contributions.Single(c=>c.FieldKey=="pulse").Revisions.Count);
        await Assert.ThrowsAsync<EncounterNoteNotEditableException>(()=>FieldSave(p,e,"{\"history\":\"Replacement\"}",8));
        Assert.Contains("Legacy history",(await db.Notes.GetByUidAsync(e))!.StructuredDataJson);
        Assert.True(await db.Save(p,e,"New narrative",7,field:"history"));
        var narrative=(await db.Notes.GetByUidAsync(e))!.Contributions.Single(c=>c.ValueKind=="Narrative");
        Assert.Equal("history",narrative.FieldKey);Assert.Equal("History",narrative.FieldLabel);
        Assert.Contains("Legacy history",(await db.Notes.GetByUidAsync(e))!.StructuredDataJson);
        await Assert.ThrowsAsync<SqlException>(()=>db.Save(p,e,"Forged scalar",7,field:"pulse"));
        await Assert.ThrowsAsync<SqlException>(()=>db.Save(p,e,"Forged field",7,field:"other"));
    }

    [DiagnosisSqlFact]
    public async Task FieldChangesAndProvenanceRollbackTogetherAndRejectStaleAndForeignSaves()
    {
        var (p,e)=await SeedTemplate();var before=(await db.Notes.GetByUidAsync(e))!;
        await db.Inner.Execute("ALTER TABLE dbo.AuditLog WITH NOCHECK ADD CONSTRAINT CK_TestFieldAudit CHECK (ActionName <> 'FieldValueSaved')");
        try {await Assert.ThrowsAsync<SqlException>(()=>FieldSave(p,e,"{\"history\":\"Legacy history\",\"pulse\":80}",7));}
        finally {await db.Inner.Execute("ALTER TABLE dbo.AuditLog DROP CONSTRAINT CK_TestFieldAudit");}
        var after=(await db.Notes.GetByUidAsync(e))!;
        Assert.Equal(before.StructuredDataJson,after.StructuredDataJson);Assert.Equal(before.RowVersion,after.RowVersion);Assert.Empty(after.Contributions);
        var saved=(await FieldSave(p,e,"{\"history\":\"Legacy history\",\"pulse\":80}",7))!;
        await Assert.ThrowsAsync<EncounterNoteNotEditableException>(()=>FieldSave(p,e,"{\"history\":\"Legacy history\",\"pulse\":81}",8,before.RowVersion));
        var (otherP,_)=await db.Seed();Assert.Null(await FieldSave(otherP,e,"{}",8,saved.RowVersion));
        var foreign=new PatientEncounterRepository(new EncounterDiagnosisSqlTests.Factory(db.Inner.OtherConnection),NullLogger<PatientEncounterRepository>.Instance);
        Assert.Null(await foreign.UpdateStructuredDataAsync(p,e,new() {RowVersion=saved.RowVersion,StructuredDataJson="{\"schemaVersion\":1,\"values\":{}}"},null,null,null,null,8));
        await Assert.ThrowsAsync<EncounterCannotBeSignedException>(()=>db.Notes.SignVersionedAsync(p,e,8,MicroEMR.Application.Scheduling.AppointmentStatus.Seen,MicroEMR.Application.Scheduling.AppointmentStatus.Completed,before.RowVersion));
        var signed=(await db.Notes.SignVersionedAsync(p,e,8,MicroEMR.Application.Scheduling.AppointmentStatus.Seen,MicroEMR.Application.Scheduling.AppointmentStatus.Completed,saved.RowVersion))!;
        Assert.Equal(7,Assert.Single(signed.Contributions).OriginalContributorId);
        await Assert.ThrowsAsync<EncounterNoteNotEditableException>(()=>FieldSave(p,e,"{}",8));
        Assert.NotNull(await db.Notes.CreateAddendumAsync(p,e,new() {AddendumText="Signed clarification"},8));
    }


    [DiagnosisSqlFact]
    public async Task TinyDecimalChangeIsARevisionAndEquivalentPrecisionIsANoOp()
    {
        var (p,e)=await SeedTemplate();
        await FieldSave(p,e,"{\"history\":\"Legacy history\",\"pulse\":0.0000000000000000000000000001}",7);
        var next=(await FieldSave(p,e,"{\"history\":\"Legacy history\",\"pulse\":0.0000000000000000000000000002}",8))!;
        Assert.Equal(2,Assert.Single(next.Contributions).Revisions.Count);
        var same=(await FieldSave(p,e,"{\"history\":\"Legacy history\",\"pulse\":0.00000000000000000000000000020}",7))!;
        Assert.Equal(2,Assert.Single(same.Contributions).Revisions.Count);
        Assert.Equal(8,same.Contributions[0].Revisions[1].EditorId);
    }

    private async Task<(Guid p,Guid e)> SeedTemplate()
    {
        var (p,e)=await db.Seed();var version=Guid.NewGuid();
        await db.Inner.Execute($$$"""
            INSERT dbo.DocumentTemplateVersion VALUES('{{{version}}}',N'{"schemaVersion":1,"sections":[{"key":"subjective","title":"Subjective","fields":[{"key":"history","label":"History","type":"TextArea"},{"key":"pulse","label":"Pulse","type":"Number"},{"key":"flag","label":"Flag","type":"Boolean"}]}]}');
            UPDATE dbo.PatientEncounter SET TemplateVersionUid='{{{version}}}',StructuredDataJson=N'{"schemaVersion":1,"values":{"history":"Legacy history"}}' WHERE EncounterUid=@p;
            """,e);
        return(p,e);
    }
    private async Task<MicroEMR.Application.PatientEncounters.Contracts.PatientEncounterDetailsResponse?> FieldSave(Guid p,Guid e,string values,long actor,string? version=null)
    {
        version ??=(await db.Notes.GetByUidAsync(e))!.RowVersion;
        return await db.Notes.UpdateStructuredDataAsync(p,e,new() {StructuredDataJson="{\"schemaVersion\":1,\"values\":"+values+"}",RowVersion=version},null,null,null,null,actor);
    }

    [DiagnosisSqlFact]
    public async Task TwoCliniciansSameSectionRevisionAndSigningPreserveAttribution()
    {
        var (p,e)=await db.Seed();
        Assert.True(await db.Save(p,e,"A text",7));
        var first=(await db.Notes.GetByUidAsync(e))!;var uid=Assert.Single(first.Contributions).ContributionUid;
        Assert.True(await db.Save(p,e,"B text",8));
        Assert.True(await db.Save(p,e,"A edited by B",8,uid));
        var before=(await db.Notes.GetByUidAsync(e))!;
        Assert.Equal(2,before.Contributions.Count);
        var block=before.Contributions.Single(c=>c.ContributionUid==uid);
        Assert.Equal(7,block.OriginalContributorId);Assert.Equal("Test clinician",block.OriginalContributorDisplayName);
        Assert.Equal("A edited by B",block.Content);Assert.Equal(new[] {1,2},block.Revisions.Select(r=>r.RevisionNumber));
        Assert.Equal("A text",block.Revisions[0].Content);Assert.Equal(8,block.Revisions[1].EditorId);
        Assert.Equal("Clinician B",block.Revisions[1].EditorDisplayName);
        Assert.True(block.CreatedAtUtc<=block.Revisions[1].RevisedAtUtc);
        var signed=await db.Notes.SignAsync(p,e,8,MicroEMR.Application.Scheduling.AppointmentStatus.Seen,MicroEMR.Application.Scheduling.AppointmentStatus.Completed);
        Assert.Equal("Signed",signed!.Status);Assert.Equal(8,signed.SignedBy);Assert.NotNull(signed.SignedAt);
        Assert.Equal(2,signed.Contributions.Count);Assert.Equal(block.Content,signed.Contributions[0].Content);
        Assert.Equal(7,signed.Contributions[0].OriginalContributorId);
        await Assert.ThrowsAsync<EncounterNoteNotEditableException>(()=>db.Save(p,e,"forbidden",8,uid));
        await Assert.ThrowsAsync<EncounterNoteNotEditableException>(()=>db.Save(p,e,"forbidden new",8));
        await Assert.ThrowsAsync<EncounterNoteNotEditableException>(()=>db.Notes.UpdateSoapNoteAsync(p,e,new() {SubjectiveNote="forbidden"},8));
        var addendum=await db.Notes.CreateAddendumAsync(p,e,new() {AddendumText="Allowed amendment"},8);
        Assert.Equal(8,addendum!.CreatedBy);Assert.Equal("Allowed amendment",addendum.AddendumText);
        Assert.Equal(4,await db.Inner.Scalar<int>("SELECT COUNT(*) FROM dbo.AuditLog WHERE PatientId=(SELECT PatientId FROM dbo.Patient WHERE PatientUid=@p)",p));
        Assert.Equal(3,await db.Inner.Scalar<int>("SELECT COUNT(*) FROM dbo.PatientEncounterHistory WHERE EncounterUid=@p AND ActionType IN ('ContributionCreated','ContributionRevised')",e));
    }

    [DiagnosisSqlFact]
    public async Task StaleSaveRollsBackContentRevisionAndAudit()
    {
        var (p,e)=await db.Seed();var old=(await db.Notes.GetByUidAsync(e))!.RowVersion;
        Assert.True(await db.Save(p,e,"first",7));
        await Assert.ThrowsAsync<EncounterNoteNotEditableException>(()=>db.Save(p,e,"stale",8,version:old));
        var current=(await db.Notes.GetByUidAsync(e))!;
        Assert.Equal("first",Assert.Single(current.Contributions).Content);
        Assert.Single(current.Contributions[0].Revisions);
        Assert.Equal(1,await db.Inner.Scalar<int>("SELECT COUNT(*) FROM dbo.AuditLog WHERE PatientId=(SELECT PatientId FROM dbo.Patient WHERE PatientUid=@p)",p));
        Assert.Equal(1,await db.Inner.Scalar<int>("SELECT COUNT(*) FROM dbo.PatientEncounterHistory WHERE EncounterUid=@p",e));
    }

    [DiagnosisSqlFact]
    public async Task CrossPatientForeignBlockAndOtherTenantCannotReadOrWrite()
    {
        var (p,e)=await db.Seed();var (otherP,otherE)=await db.Seed();
        Assert.True(await db.Save(p,e,"Private",7));var note=(await db.Notes.GetByUidAsync(e))!;
        Assert.False(await db.Save(otherP,e,"spoofed patient",8,version:note.RowVersion));
        Assert.False(await db.Save(otherP,otherE,"foreign block",8,note.Contributions[0].ContributionUid));
        var foreign=new EncounterContributionRepository(new EncounterDiagnosisSqlTests.Factory(db.Inner.OtherConnection),NullLogger<EncounterContributionRepository>.Instance);
        Assert.False(await foreign.SaveAsync(p,e,new() {SectionKey="subjective",Content="spoofed tenant",RowVersion=note.RowVersion},8));
        var otherNotes=new PatientEncounterRepository(new EncounterDiagnosisSqlTests.Factory(db.Inner.OtherConnection),NullLogger<PatientEncounterRepository>.Instance);
        Assert.Null(await otherNotes.GetByUidAsync(e));
        Assert.Empty((await db.Notes.GetByUidAsync(otherE))!.Contributions);
        await using var sql=new SqlConnection(db.Inner.OtherConnection);await sql.OpenAsync();
        await using var command=new SqlCommand("dbo.PatientEncounterContribution_Get",sql) {CommandType=CommandType.StoredProcedure};
        command.Parameters.AddWithValue("@PatientUid",p);command.Parameters.AddWithValue("@EncounterUid",e);
        await using var reader=await command.ExecuteReaderAsync();Assert.False(await reader.ReadAsync());
    }

    [DiagnosisSqlFact]
    public async Task IdentitySnapshotSurvivesRenameAndLegacyContentIsUntouched()
    {
        var (p,e)=await db.Seed();var legacy=(await db.Notes.GetByUidAsync(e))!;
        Assert.Empty(legacy.Contributions);Assert.Equal("Preserved SOAP",legacy.SubjectiveNote);
        Assert.True(await db.Save(p,e,"new content",8));
        await db.Inner.Execute("UPDATE dbo.ApplicationUser SET DisplayName='Renamed B' WHERE UserId=8");
        var note=(await db.Notes.GetByUidAsync(e))!;
        Assert.Equal("Preserved SOAP",note.SubjectiveNote);
        Assert.Equal("Clinician B",note.Contributions[0].OriginalContributorDisplayName);
        Assert.Equal("Clinician B",note.Contributions[0].Revisions[0].EditorDisplayName);
        await db.Inner.Execute("UPDATE dbo.ApplicationUser SET DisplayName='Clinician B' WHERE UserId=8");
    }

    [DiagnosisSqlFact]
    public async Task TemplateBlockUsesHistoricalSectionAndDoesNotRewriteValuesOrSoapProjection()
    {
        var (p,e)=await db.Seed();var version=Guid.NewGuid();
        await db.Inner.Execute($"INSERT dbo.DocumentTemplateVersion VALUES('{version}',N'{{\"schemaVersion\":1,\"sections\":[{{\"key\":\"shared\",\"title\":\"Shared section\"}}]}}'); UPDATE dbo.PatientEncounter SET TemplateVersionUid='{version}',StructuredDataJson=N'{{\"values\":{{}}}}' WHERE EncounterUid=@p",e);
        Assert.True(await db.Save(p,e,"Shared text",7,section:"shared"));
        var note=(await db.Notes.GetByUidAsync(e))!;
        Assert.Equal("Shared section",Assert.Single(note.Contributions).SectionTitle);
        Assert.Equal("{\"values\":{}}",note.StructuredDataJson);Assert.Equal("Preserved SOAP",note.SubjectiveNote);
        await Assert.ThrowsAsync<SqlException>(()=>db.Save(p,e,"Invalid",7,section:"subjective"));
    }

    [DiagnosisSqlFact]
    public async Task FailedAuditLeavesNoContributionOrRowVersionChange()
    {
        var (p,e)=await db.Seed();var before=(await db.Notes.GetByUidAsync(e))!;
        await db.Inner.Execute("ALTER TABLE dbo.AuditLog WITH NOCHECK ADD CONSTRAINT CK_TestContributionAudit CHECK (ActionName <> 'ContributionCreated')");
        try { await Assert.ThrowsAsync<SqlException>(()=>db.Save(p,e,"will rollback",7)); }
        finally { await db.Inner.Execute("ALTER TABLE dbo.AuditLog DROP CONSTRAINT CK_TestContributionAudit"); }
        var after=(await db.Notes.GetByUidAsync(e))!;
        Assert.Empty(after.Contributions);Assert.Equal(before.RowVersion,after.RowVersion);
        Assert.Equal(0,await db.Inner.Scalar<int>("SELECT COUNT(*) FROM dbo.PatientEncounterHistory WHERE EncounterUid=@p",e));
    }

    [DiagnosisSqlFact]
    public async Task UnknownClinicalActorFailsWithoutPersistingContent()
    {
        var (p,e)=await db.Seed();await Assert.ThrowsAsync<SqlException>(()=>db.Save(p,e,"invalid actor",999));
        Assert.Empty((await db.Notes.GetByUidAsync(e))!.Contributions);
    }
}

public sealed class ContributionSqlDatabase : IAsyncLifetime
{
    public EncounterDiagnosisSqlDatabase Inner {get;}=new();
    private string connection=string.Empty;
    public PatientEncounterRepository Notes=>new(new EncounterDiagnosisSqlTests.Factory(connection),NullLogger<PatientEncounterRepository>.Instance);
    public async Task InitializeAsync()
    {
        await Inner.InitializeAsync();
        // Obtain the fixture's tenant connection using its established factory-backed repository connection.
        connection=(string)typeof(EncounterDiagnosisSqlDatabase).GetProperty("Connection",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(Inner)!;
        foreach(var target in new[] {connection,Inner.OtherConnection})
        {
            await using var sql=new SqlConnection(target);await sql.OpenAsync();
            await Batches(sql,"""
                INSERT dbo.ApplicationUser VALUES(8,N'Clinician B');
                ALTER TABLE dbo.PatientEncounter ADD TemplateVersionUid uniqueidentifier,TemplateUid uniqueidentifier,StructuredDataJson nvarchar(max),
                    EncounterDateUtc datetime2 DEFAULT SYSUTCDATETIME(),EncounterType nvarchar(100) DEFAULT 'Office',ReasonForVisit nvarchar(500),LocationName nvarchar(200),ProviderName nvarchar(200),
                    CreatedBy bigint,CreatedByDisplayName nvarchar(200),CreatedAt datetime2 DEFAULT SYSUTCDATETIME(),
                    Status nvarchar(50),SignedAt datetime2,SignedBy bigint,AppointmentUid uniqueidentifier,EncounterNotes nvarchar(max),
                    ObjectiveNote nvarchar(max),AssessmentNote nvarchar(max),PlanNote nvarchar(max);
                CREATE TABLE dbo.DocumentTemplateVersion(TemplateVersionUid uniqueidentifier PRIMARY KEY,DefinitionJson nvarchar(max));
                CREATE TABLE dbo.ScheduleAppointment(AppointmentUid uniqueidentifier,AppointmentStatus nvarchar(50),IsDeleted bit,UpdatedAt datetime2,UpdatedBy bigint);
                GO
                CREATE OR ALTER PROCEDURE dbo.PatientEncounter_GetByUid @EncounterUid uniqueidentifier AS
                SELECT e.*,u.DisplayName AS SignedByDisplayName FROM dbo.PatientEncounter e LEFT JOIN dbo.ApplicationUser u ON u.UserId=e.SignedBy
                WHERE EncounterUid=@EncounterUid;
                GO
                """);
            var encounterSql=File.ReadAllText(Path.Combine(Root(),"db/patient_encounter_stored_procedures.sql"));
            foreach(var procedure in new[] {"PatientEncounter_Sign","PatientEncounter_UpdateSoapNote"})
                await Batches(sql,Regex.Split(encounterSql,@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase)
                    .Single(x=>x.TrimStart().StartsWith("CREATE OR ALTER PROCEDURE dbo."+procedure+"\n",StringComparison.Ordinal)
                        || x.TrimStart().StartsWith("CREATE OR ALTER PROCEDURE dbo."+procedure+"\r\n",StringComparison.Ordinal)));
            await Batches(sql,File.ReadAllText(Path.Combine(Root(),"db/patient_encounter_addendum_stored_procedures.sql")));
            await Batches(sql,File.ReadAllText(Path.Combine(Root(),"db/tenant-clinical/migrations/0071-encounter-contribution-identity.sql")));
            await Batches(sql,File.ReadAllText(Path.Combine(Root(),"db/tenant-clinical/migrations/0072-encounter-field-attribution.sql")));
            await Batches(sql,File.ReadAllText(Path.Combine(Root(),"db/tenant-clinical/migrations/0072-encounter-field-attribution.sql")));
            // Verify migration rerun is additive and idempotent.
        }
    }
    public Task<(Guid,Guid)> Seed()=>Inner.Seed();
    public async Task<bool> Save(Guid p,Guid e,string content,long actor,Guid? uid=null,string? version=null,string section="subjective",string? field=null)
    {
        version ??= (await Notes.GetByUidAsync(e))!.RowVersion;
        return await new EncounterContributionRepository(new EncounterDiagnosisSqlTests.Factory(connection),NullLogger<EncounterContributionRepository>.Instance)
            .SaveAsync(p,e,new() {ContributionUid=uid,SectionKey=section,FieldKey=field,Content=content,RowVersion=version},actor);
    }
    private static async Task Batches(SqlConnection sql,string text)
    {foreach(var batch in Regex.Split(text,@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase).Where(x=>!string.IsNullOrWhiteSpace(x)))await new SqlCommand(batch,sql).ExecuteNonQueryAsync();}
    private static string Root()
    {var dir=new DirectoryInfo(AppContext.BaseDirectory);while(dir is not null && !File.Exists(Path.Combine(dir.FullName,"AGENTS.md")))dir=dir.Parent;return dir?.FullName??throw new DirectoryNotFoundException();}
    public Task DisposeAsync()=>Inner.DisposeAsync();
}
