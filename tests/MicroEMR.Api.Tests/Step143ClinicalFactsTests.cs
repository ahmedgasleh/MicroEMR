using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.PatientMedications.Contracts;
using MicroEMR.Application.PatientPrescriptions;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class Step143ContractTests
{
    [Theory]
    [InlineData("Compliant")] [InlineData("Not compliant")] [InlineData("Unknown")] [InlineData("Not assessed")] [InlineData(null)]
    public void AssessmentStatesAndClearAreExplicit(string? state)
    {
        new MedicationComplianceAssessment { State=state }.EnsureValid();
        Assert.Throws<ArgumentException>(()=>new MedicationComplianceAssessment { State="Inferred" }.EnsureValid());
    }

    [Theory]
    [InlineData("{\"PaperCopyRequiredBeforeDispense\":false}")]
    [InlineData("{\"MinimumDispenseIntervalDays\":0}")]
    [InlineData("{\"MinimumDispenseIntervalDays\":-1}")]
    [InlineData("{\"MinimumDispenseIntervalDays\":1.5}")]
    [InlineData("{\"MinimumDispenseIntervalDays\":1e1}")]
    [InlineData("{\"MinimumDispenseIntervalDays\":2147483648}")]
    [InlineData("{\"MinimumDispenseIntervalDays\":\"2\"}")]
    public void InvalidOrderClinicalWireValuesAreRejected(string json)=>Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<PrescriptionOrderFields>(json));

    [Fact]
    public void PresenceAndTechnicalLimitsAreIndependent()
    {
        var absent=JsonSerializer.Serialize(new PrescriptionOrderFields { TreatmentType="Independent" });
        Assert.DoesNotContain("MinimumDispenseIntervalDays",absent);Assert.DoesNotContain("PaperCopyRequiredBeforeDispense",absent);
        var clear=JsonSerializer.Serialize(new PrescriptionOrderFields { MinimumDispenseIntervalDays=null,PaperCopyRequiredBeforeDispense=null });
        Assert.Contains("\"MinimumDispenseIntervalDays\":null",clear);
        Assert.Equal(int.MaxValue,JsonSerializer.Deserialize<PrescriptionOrderFields>("{\"MinimumDispenseIntervalDays\":2147483647}")!.MinimumDispenseIntervalDays);
        new MedicationComplianceAssessment { State="Unknown",Explanation=new string('A',1000) }.EnsureValid();
        Assert.Throws<ArgumentException>(()=>new MedicationComplianceAssessment { State="Unknown",Explanation=new string('A',1001) }.EnsureValid());
        Assert.Throws<ArgumentException>(()=>new MedicationComplianceAssessment { State=null,Explanation="Unattributed" }.EnsureValid());
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<MedicationComplianceAssessment>("{\"State\":\"Compliant\",\"AssessorUserId\":7}"));
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<MedicationComplianceAssessment>("{\"State\":\"Unknown\",\"AssessmentAt\":\"2026-10-22T12:00:00\"}"));
    }
}

public sealed class Step143ComplianceTests(Step143PlanDatabase fixture):IClassFixture<Step143PlanDatabase>
{
    private MedicationClassificationDatabase Db=>fixture.Db;

    [Theory]
    [InlineData("state")] [InlineData("length")] [InlineData("actor")] [InlineData("time")]
    public async Task DirectSqlRejectsInvalidAssessmentWithoutMutation(string invalid)
    {
        var patient=await Db.Clinical.SeedPatient(false);var actor=await Db.Clinical.SeedUser(false);
        var json=invalid switch {
            "state"=>"{\"State\":\"Compliant \"}",
            "length"=>JsonSerializer.Serialize(new{State="Compliant",Explanation=new string('A',1001)}),
            "actor"=>"{\"State\":\"Compliant\",\"AssessorUserId\":999}",
            _=>"{\"State\":\"Unknown\",\"AssessmentAt\":\"2026-10-22T09:30:00\"}"
        };
        await using var c=await Db.Clinical.Open(false);
        await using var q=new SqlCommand("EXEC dbo.PatientMedication_Create @PatientUid=@p,@MedicationName=N'TEST ONLY invalid assessment',@CreatedBy=@a,@ComplianceAssessment=@json",c);
        q.Parameters.AddWithValue("@p",patient);q.Parameters.AddWithValue("@a",actor.Actor);q.Parameters.Add("@json",SqlDbType.NVarChar,-1).Value=json;
        Assert.Equal(53100,(await Assert.ThrowsAsync<SqlException>(()=>q.ExecuteNonQueryAsync())).Number);
        Assert.Empty(await Db.Repository().GetByPatientUidAsync(patient));
    }

    [Fact]
    public async Task PartialAssessmentAndEmptyGroupPreserveUnspecifiedFields()
    {
        var patient=await Db.Clinical.SeedPatient(false);var actor=await Db.Clinical.SeedUser(false);
        var instant=DateTimeOffset.Parse("2026-10-22T09:30:00-04:00");
        var row=await Db.Repository().CreateAsync(patient,new(){MedicationName="TEST ONLY partial assessment",ComplianceAssessment=new(){State="Compliant",Explanation="Original",AssessmentAt=instant}},actor.Actor,null);
        var edit=MedicationClassificationTests.Update(row,"Acute");edit.ComplianceAssessment=new(){Explanation="Revised"};
        var saved=(await Db.Repository().UpdateAsync(patient,row.MedicationUid,edit,actor.Actor))!;
        Assert.Equal("Compliant",saved.ComplianceAssessment!.State);Assert.Equal(instant,saved.ComplianceAssessment.AssessmentAt);Assert.Equal("Revised",saved.ComplianceAssessment.Explanation);
        edit.RowVersion=saved.RowVersion;edit.ComplianceAssessment=new();
        saved=(await Db.Repository().UpdateAsync(patient,row.MedicationUid,edit,actor.Actor))!;
        Assert.Equal(2,saved.ComplianceHistory.Count);Assert.Equal("Revised",saved.ComplianceAssessment!.Explanation);
        edit.RowVersion=saved.RowVersion;edit.ComplianceAssessment=new(){State=null};
        saved=(await Db.Repository().UpdateAsync(patient,row.MedicationUid,edit,actor.Actor))!;
        Assert.Null(saved.ComplianceAssessment);Assert.Equal(3,saved.ComplianceHistory.Count);
    }

    [Theory]
    [InlineData("Compliant")] [InlineData("Not compliant")] [InlineData("Unknown")] [InlineData("Not assessed")]
    public async Task RealApiRecordsAssessmentActorTimesRevisionsOmissionAndClear(string state)
    {
        var patient=await Db.Clinical.SeedPatient(false);var writer=await Db.Clinical.SeedUser(false);var reviser=await Db.Clinical.SeedUser(false);
        await using var host=await FamilyHistoryAcceptanceHost.Start(Db.Clinical,false,writer,reviser);
        using var client=host.Client(writer);using var second=host.Client(reviser);
        var instant=DateTimeOffset.Parse("2026-10-22T09:30:00-04:00");
        var request=new CreatePatientMedicationRequest { MedicationName="TEST ONLY assessment",Classification="Acute",ComplianceAssessment=new(){State=state,Explanation="TEST ONLY <as assessed> & retained",AssessmentAt=instant} };
        var response=await client.PostAsJsonAsync(MedicationClassificationTests.Route(patient),request);
        Assert.True(response.StatusCode==HttpStatusCode.Created,await response.Content.ReadAsStringAsync());
        var row=(await response.Content.ReadFromJsonAsync<PatientMedicationDetailsResponse>())!;
        Assert.Equal(state,row.ComplianceAssessment!.State);Assert.Equal(writer.Actor,row.ComplianceAssessment.AssessorUserId);
        Assert.Equal(instant,row.ComplianceAssessment.AssessmentAt);Assert.Equal(instant.Offset,row.ComplianceAssessment.AssessmentAt!.Value.Offset);
        Assert.Single(row.ComplianceHistory);Assert.NotEqual(default,row.ComplianceAssessment.RecordedAtUtc);
        var edit=MedicationClassificationTests.Update(row,"Ongoing");
        response=await second.PutAsJsonAsync(MedicationClassificationTests.Route(patient)+"/"+row.MedicationUid,edit);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var saved=(await response.Content.ReadFromJsonAsync<PatientMedicationDetailsResponse>())!;
        Assert.Single(saved.ComplianceHistory);Assert.Equal(writer.Actor,saved.ComplianceAssessment!.AssessorUserId);
        edit.RowVersion=saved.RowVersion;edit.ComplianceAssessment=new(){State="Unknown",AssessmentAt=null};
        response=await second.PutAsJsonAsync(MedicationClassificationTests.Route(patient)+"/"+row.MedicationUid,edit);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);saved=(await response.Content.ReadFromJsonAsync<PatientMedicationDetailsResponse>())!;
        Assert.Equal(reviser.Actor,saved.ComplianceAssessment!.AssessorUserId);Assert.Equal(2,saved.ComplianceHistory.Count);Assert.Null(saved.ComplianceAssessment.AssessmentAt);
        Assert.Equal(HttpStatusCode.Conflict,(await second.PutAsJsonAsync(MedicationClassificationTests.Route(patient)+"/"+row.MedicationUid,edit)).StatusCode);
        edit.RowVersion=saved.RowVersion;edit.ComplianceAssessment=new(){State=null};
        response=await second.PutAsJsonAsync(MedicationClassificationTests.Route(patient)+"/"+row.MedicationUid,edit);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);saved=(await response.Content.ReadFromJsonAsync<PatientMedicationDetailsResponse>())!;
        Assert.Null(saved.ComplianceAssessment);Assert.Equal(3,saved.ComplianceHistory.Count);Assert.Null(saved.ComplianceHistory[0].State);
        var audit=(await Db.Clinical.Audit(false,row.MedicationUid,"PatientMedication")).Last();
        Assert.Contains("Unknown",audit.Old!);Assert.Contains("ComplianceAssessment",audit.New);
    }

    [Fact]
    public async Task DeniedInactiveForeignAndFailedAuditLeaveAssessmentAndHistoryUnchanged()
    {
        var patient=await Db.Clinical.SeedPatient(false);var otherPatient=await Db.Clinical.SeedPatient(false);
        var actor=await Db.Clinical.SeedUser(false);var reader=await Db.Clinical.SeedUser(false,false);var noRead=await Db.Clinical.SeedUser(false,true,false);var foreign=await Db.Clinical.SeedUser(true);
        var row=await Db.Repository().CreateAsync(patient,new(){MedicationName="TEST ONLY authorization",ComplianceAssessment=new(){State="Not assessed"}},actor.Actor,null);
        var edit=MedicationClassificationTests.Update(row,"Acute");edit.ComplianceAssessment=new(){State="Compliant"};
        await using var host=await FamilyHistoryAcceptanceHost.Start(Db.Clinical,false,actor,reader,noRead);
        using var client=host.Client(actor);using var denied=host.Client(reader);using var hidden=host.Client(noRead);
        var route=MedicationClassificationTests.Route(patient)+"/"+row.MedicationUid;
        Assert.Equal(HttpStatusCode.Forbidden,(await denied.PutAsJsonAsync(route,edit)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await hidden.PutAsJsonAsync(route,edit)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.PutAsJsonAsync(MedicationClassificationTests.Route(otherPatient)+"/"+row.MedicationUid,edit)).StatusCode);
        await using var other=await FamilyHistoryAcceptanceHost.Start(Db.Clinical,true,foreign);using var foreignClient=other.Client(foreign);
        Assert.Equal(HttpStatusCode.NotFound,(await foreignClient.PutAsJsonAsync(route,edit)).StatusCode);
        Assert.Null(await Db.Repository(true).GetByUidAsync(row.MedicationUid));
        var before=(await Db.Repository().GetByUidAsync(row.MedicationUid))!;
        await Db.Execute(false,"CREATE TRIGGER dbo.Step143AuditFail ON dbo.AuditLog AFTER INSERT AS BEGIN THROW 52997,'TEST ONLY audit failure',1; END");
        try { await Assert.ThrowsAsync<SqlException>(()=>Db.Repository().UpdateAsync(patient,row.MedicationUid,edit,actor.Actor)); }
        finally { await Db.Execute(false,"DROP TRIGGER dbo.Step143AuditFail"); }
        var after=(await Db.Repository().GetByUidAsync(row.MedicationUid))!;
        Assert.Equal(before.RowVersion,after.RowVersion);Assert.Single(after.ComplianceHistory);Assert.Equal("Not assessed",after.ComplianceAssessment!.State);
        await Db.Execute(false,$"UPDATE dbo.ApplicationUser SET IsActive=0 WHERE UserId={actor.Actor}");
        Assert.Equal(52901,(await Assert.ThrowsAsync<SqlException>(()=>Db.Repository().UpdateAsync(patient,row.MedicationUid,edit,actor.Actor))).Number);
        Assert.Equal(before.RowVersion,(await Db.Repository().GetByUidAsync(row.MedicationUid))!.RowVersion);
        Assert.Null((await Db.Repository().GetByUidAsync(Db.LegacyUid))!.ComplianceAssessment);
    }
}

public sealed class Step143PlanDatabase:IAsyncLifetime
{
    internal MedicationClassificationDatabase Db {get;}=new();
    public async Task InitializeAsync(){await Db.InitializeAsync();try{foreach(var other in new[]{false,true})await Db.Apply(other,"tenant-clinical/migrations/0089-medication-compliance-assessments.sql");}catch{await Db.DisposeAsync();throw;}}
    public Task DisposeAsync()=>Db.DisposeAsync();
}

public sealed class Step143OrderTests(Step143OrderDatabase fixture):IClassFixture<Step143OrderDatabase>
{
    [Fact]
    public async Task OrderOwnershipAndAuditFailurePreserveFactsAndDistinctCorrectionActor()
    {
        var db=fixture.Db;var repo=db.Repository();var patient=await db.Stores.SeedPatient();
        var request=PrescriptionDatePrecisionTests.Draft();request.OrderFields=new(){PaperCopyRequiredBeforeDispense=true,MinimumDispenseIntervalDays=3};
        var row=await repo.CreateAsync(patient,request,7);var edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=row.RowVersion;edit.OrderFields=new(){MinimumDispenseIntervalDays=9,PaperCopyRequiredBeforeDispense=null};
        await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(()=>repo.UpdateAsync(patient,row.PrescriptionUid,edit,8));
        var otherPatient=await db.Stores.SeedPatient();
        Assert.Null(await repo.UpdateAsync(otherPatient,row.PrescriptionUid,edit,7));
        await Assert.ThrowsAsync<PrescriberProviderMappingException>(()=>repo.UpdateAsync(patient,row.PrescriptionUid,edit,9));
        await using(var c=await db.Factory().OpenConnectionAsync())
            await PrescriptionPrivateNoteFixture.Batches(c,"CREATE TRIGGER dbo.Step143OrderAuditFail ON dbo.AuditLog AFTER INSERT AS BEGIN THROW 52997,'TEST ONLY audit failure',1; END");
        try { await Assert.ThrowsAsync<SqlException>(()=>repo.UpdateAsync(patient,row.PrescriptionUid,edit,7)); }
        finally { await using var c=await db.Factory().OpenConnectionAsync();await PrescriptionPrivateNoteFixture.Batches(c,"DROP TRIGGER dbo.Step143OrderAuditFail"); }
        var unchanged=(await repo.GetAsync(patient,row.PrescriptionUid))!;
        Assert.Equal(row.RowVersion,unchanged.RowVersion);Assert.True(unchanged.PaperCopyRequiredBeforeDispense);Assert.Equal(3,unchanged.MinimumDispenseIntervalDays);
        var final=(await repo.FinalizeAsync(patient,row.PrescriptionUid,row.RowVersion,7))!;var artifact=await repo.GetArtifactAsync(patient,row.PrescriptionUid);
        request.SupersedesPrescriptionUid=row.PrescriptionUid;
        var correction=await repo.CreateAsync(patient,request,8);
        Assert.Equal(7,final.PrescriberUserId);Assert.Equal(8,correction.CreatedBy);Assert.Equal(8,correction.PrescriberUserId);
        Assert.NotEqual(final.PrescriberProviderUid,correction.PrescriberProviderUid);Assert.Equal(final.PrescriptionUid,correction.SupersedesPrescriptionUid);
        Assert.True(correction.PaperCopyRequiredBeforeDispense);Assert.Equal(3,correction.MinimumDispenseIntervalDays);Assert.Equal(artifact,await repo.GetArtifactAsync(patient,row.PrescriptionUid));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task OrderFactsSurviveOmissionClearFinalizationAndCorrection(bool signed)
    {
        var db=fixture.Db;var patient=await db.Stores.SeedPatient();var repo=db.Repository();
        var request=PrescriptionDatePrecisionTests.Draft();request.OrderFields=new(){PaperCopyRequiredBeforeDispense=true,MinimumDispenseIntervalDays=int.MaxValue,SubstitutionProhibited=false,TreatmentType="Independent"};
        var row=await repo.CreateAsync(patient,request,7);Assert.True(row.PaperCopyRequiredBeforeDispense);Assert.Equal(int.MaxValue,row.MinimumDispenseIntervalDays);
        var edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=row.RowVersion;edit.OrderFields=new(){TreatmentType="Retained independent"};
        var saved=(await repo.UpdateAsync(patient,row.PrescriptionUid,edit,7))!;
        Assert.True(saved.PaperCopyRequiredBeforeDispense);Assert.Equal(int.MaxValue,saved.MinimumDispenseIntervalDays);
        edit.RowVersion=saved.RowVersion;edit.OrderFields=new(){PaperCopyRequiredBeforeDispense=null,MinimumDispenseIntervalDays=null};
        saved=(await repo.UpdateAsync(patient,row.PrescriptionUid,edit,7))!;Assert.Null(saved.PaperCopyRequiredBeforeDispense);Assert.Null(saved.MinimumDispenseIntervalDays);
        edit.RowVersion=saved.RowVersion;edit.OrderFields=request.OrderFields;saved=(await repo.UpdateAsync(patient,row.PrescriptionUid,edit,7))!;
        var final=signed?(await repo.SignAndFinalizeAsync(patient,row.PrescriptionUid,saved.RowVersion,7))!:(await repo.FinalizeAsync(patient,row.PrescriptionUid,saved.RowVersion,7))!;
        var artifact=(await repo.GetArtifactAsync(patient,row.PrescriptionUid))!;
        using(var doc=JsonDocument.Parse(artifact.Json)){Assert.True(doc.RootElement.GetProperty("PaperCopyRequiredBeforeDispense").GetBoolean());Assert.Equal(int.MaxValue,doc.RootElement.GetProperty("MinimumDispenseIntervalDays").GetInt32());}
        await using var host=await db.Host();using var client=host.Client();
        var correctionResponse=await client.PostAsJsonAsync($"api/patients/{patient}/prescriptions/{row.PrescriptionUid}/correction",new{final.RowVersion});
        Assert.Equal(HttpStatusCode.Created,correctionResponse.StatusCode);
        var correction=(await correctionResponse.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;
        Assert.True(correction.PaperCopyRequiredBeforeDispense);Assert.Equal(int.MaxValue,correction.MinimumDispenseIntervalDays);
        Assert.Equal(final.PrescriptionUid,correction.SupersedesPrescriptionUid);Assert.Equal(artifact,await repo.GetArtifactAsync(patient,row.PrescriptionUid));
        await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(()=>repo.UpdateAsync(patient,row.PrescriptionUid,edit,7));
        Assert.Null(await db.Repository(true).GetAsync(patient,row.PrescriptionUid));
        Assert.Null((await repo.GetAsync(db.Stores.PriorSignedPatient,db.Stores.PriorSignedPrescription))!.PaperCopyRequiredBeforeDispense);
        Assert.Equal(db.Stores.PriorSignedJson,(await repo.GetArtifactAsync(db.Stores.PriorSignedPatient,db.Stores.PriorSignedPrescription))!.Json);
    }

    [Theory]
    [InlineData("{\"PaperCopyRequiredBeforeDispense\":false}")]
    [InlineData("{\"MinimumDispenseIntervalDays\":0}")]
    [InlineData("{\"MinimumDispenseIntervalDays\":1.5}")]
    [InlineData("{\"MinimumDispenseIntervalDays\":2147483648}")]
    public async Task DirectSqlRejectsBeforeNumericNarrowing(string json)
    {
        var patient=await fixture.Db.Stores.SeedPatient();await using var c=await fixture.Db.Factory().OpenConnectionAsync();
        await using var q=new SqlCommand("EXEC dbo.PatientPrescription_CreateDraft @PatientUid=@p,@ProductName=N'TEST ONLY',@ProductDisplayText=N'TEST ONLY',@Route=N'Oral',@FrequencyCode=N'ONCE',@FrequencyDisplay=N'Once',@Prn=0,@Directions=N'TEST ONLY',@Quantity=1,@QuantityUnit=N'tablet',@AuthorizedRepeats=0,@PrescribedDate='2026-10-22',@ActorUserId=7,@OrderFields=@json",c);
        q.Parameters.AddWithValue("@p",patient);q.Parameters.Add("@json",SqlDbType.NVarChar,-1).Value=json;
        Assert.Equal(53000,(await Assert.ThrowsAsync<SqlException>(()=>q.ExecuteNonQueryAsync())).Number);Assert.Empty(await fixture.Db.Repository().ListAsync(patient));
    }
}

public sealed class Step143OrderDatabase:IAsyncLifetime
{
    internal PrescriptionDateDatabase Db {get;}=new();
    public async Task InitializeAsync(){await Db.InitializeAsync();try{foreach(var other in new[]{false,true}){await using var c=await Db.Factory(other).OpenConnectionAsync();await PrescriptionPrivateNoteFixture.Batches(c,PrescriptionPrivateNoteFixture.Sql("tenant-clinical/migrations/0090-prescription-paper-interval.sql"));}}catch{await Db.DisposeAsync();throw;}}
    public Task DisposeAsync()=>Db.DisposeAsync();
}
