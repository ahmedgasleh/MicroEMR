using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.PatientPrescriptions;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class Step138DescriptionTests
{
    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("  ")]
    [InlineData("one\ntwo")] [InlineData("one\rtwo")] [InlineData("one\u0085two")]
    [InlineData("one\u2028two")] [InlineData("one\u2029two")]
    public async Task InvalidDescriptionIsRejectedBeforeRepository(string? text)
    {
        var calls=0;
        var repo=CppSelectivePrintTests.Stub<IPatientPrescriptionRepository>((_,_)=>{calls++;throw new InvalidOperationException();});
        var service=new PatientPrescriptionService(repo,new PrescriptionDatePrecisionTests.Capture());
        var request=PrescriptionDatePrecisionTests.Draft();request.ProductDisplayText=text!;
        await Assert.ThrowsAsync<ArgumentException>(()=>service.CreateAsync(Guid.NewGuid(),request,7));
        Assert.Equal(0,calls);
    }
    [Fact]
    public void BoundaryUsesUtf16AndPreservesInternalText()
    {
        var max=new string('a',298)+"😀";
        Assert.Equal(300,max.Length);PrescriptionTextContract.Validate(max);
        Assert.Throws<ArgumentException>(()=>PrescriptionTextContract.Validate(max+"x"));
        PrescriptionTextContract.Validate("Ingredient A  1%; ingredient B 2 mg/mL; preparation <text> & base");
    }
    [Fact]
    public void OmittedSubstitutionAndExplicitClearHaveDifferentWireContracts()
    {
        Assert.DoesNotContain("SubstitutionProhibited",JsonSerializer.Serialize(new PrescriptionOrderFields{TreatmentType="TEST ONLY"}));
        var json=JsonSerializer.Serialize(new PrescriptionOrderFields{SubstitutionProhibited=null});
        Assert.Contains("\"SubstitutionProhibited\":null",json);
        Assert.True(JsonSerializer.Deserialize<PrescriptionOrderFields>(json)!.SubstitutionProhibitedSpecified);
    }
}

public sealed class Step138PrescriptionTests(Step138PrescriptionDatabase fixture):IClassFixture<Step138PrescriptionDatabase>
{
    private PrescriptionDateDatabase Db=>fixture.Db;
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task DescriptionTreatmentAndNativeIdentitySurviveFinalizationAndCorrection(bool signed)
    {
        var patient=await Db.Stores.SeedPatient();
        var request=PrescriptionDatePrecisionTests.Draft();
        request.ProductName="TEST ONLY independent brand";request.GenericName="TEST ONLY independent generic";
        request.ProductDisplayText="TEST ONLY A  1%; B 2 mg/mL; preparation <text> & base";
        request.OrderFields=new(){TreatmentType="TEST ONLY supplied category",SubstitutionProhibited=true};
        var repo=Db.Repository();var service=new PatientPrescriptionService(repo,new PrescriptionDatePrecisionTests.Capture());
        var row=await service.CreateAsync(patient,request,7);
        Assert.Equal(request.ProductDisplayText,row.ProductDisplayText);Assert.Equal(request.ProductName,row.ProductName);
        Assert.Equal("Native",row.PrescriberFirstNameSnapshot);Assert.Equal("Prescriber",row.PrescriberLastNameSnapshot);Assert.Equal("001234",row.PrescriberOhipSnapshot);
        var edit=PrescriptionDatePrecisionTests.Draft();edit.ProductDisplayText=request.ProductDisplayText;edit.RowVersion=row.RowVersion;
        var saved=(await service.UpdateAsync(patient,row.PrescriptionUid,edit,7))!;
        Assert.Equal(request.OrderFields.TreatmentType,saved.TreatmentType);
        Assert.True(saved.SubstitutionProhibited);
        edit.RowVersion=saved.RowVersion;edit.OrderFields=new(){SubstitutionProhibited=null};
        var cleared=(await service.UpdateAsync(patient,row.PrescriptionUid,edit,7))!;Assert.Null(cleared.TreatmentType);Assert.Null(cleared.SubstitutionProhibited);
        edit.RowVersion=cleared.RowVersion;edit.OrderFields=request.OrderFields;
        saved=(await service.UpdateAsync(patient,row.PrescriptionUid,edit,7))!;
        var final=signed?(await repo.SignAndFinalizeAsync(patient,row.PrescriptionUid,saved.RowVersion,7))!:(await repo.FinalizeAsync(patient,row.PrescriptionUid,saved.RowVersion,7))!;
        var artifact=(await repo.GetArtifactAsync(patient,row.PrescriptionUid))!;
        using(var json=JsonDocument.Parse(artifact.Json))
        {
            Assert.Equal(request.ProductDisplayText,json.RootElement.GetProperty("ProductDisplayText").GetString());
            Assert.Equal(request.OrderFields.TreatmentType,json.RootElement.GetProperty("TreatmentType").GetString());
            Assert.True(json.RootElement.GetProperty("SubstitutionProhibited").GetBoolean());
            Assert.Equal("001234",json.RootElement.GetProperty("PrescriberOhip").GetString());
            Assert.Equal("Native",json.RootElement.GetProperty("PrescriberFirstName").GetString());
        }
        await using var host=await Db.Host();using var client=host.Client();
        var response=await client.PostAsJsonAsync($"api/patients/{patient}/prescriptions/{row.PrescriptionUid}/correction",new{final.RowVersion});
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var correction=(await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;
        Assert.Equal(final.PrescriptionUid,correction.SupersedesPrescriptionUid);Assert.Equal(final.TreatmentType,correction.TreatmentType);
        Assert.True(correction.SubstitutionProhibited);
        Assert.Equal(final.ProductDisplayText,correction.ProductDisplayText);
        Assert.Equal(artifact,await repo.GetArtifactAsync(patient,row.PrescriptionUid));
        await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(()=>service.UpdateAsync(patient,row.PrescriptionUid,edit,7));
        Assert.Null(await Db.Repository(true).GetAsync(patient,row.PrescriptionUid));
    }
    [Fact]
    public async Task LegacyIdentityIsNotEnrichedAndDirectSqlRejectsAboveLimit()
    {
        var old=(await Db.Repository().GetAsync(Db.Stores.PriorSignedPatient,Db.Stores.PriorSignedPrescription))!;
        Assert.Null(old.PrescriberFirstNameSnapshot);Assert.Null(old.PrescriberOhipSnapshot);
        Assert.Equal(Db.Stores.PriorSignedRowVersion,old.RowVersion);
        Assert.Equal(Db.Stores.PriorSignedJson,(await Db.Repository().GetArtifactAsync(old.PatientUid,old.PrescriptionUid))!.Json);
        var patient=await Db.Stores.SeedPatient();
        await using var c=await Db.Factory().OpenConnectionAsync();
        await using var q=new SqlCommand("dbo.PatientPrescription_CreateDraft",c){CommandType=CommandType.StoredProcedure};
        q.Parameters.AddWithValue("@PatientUid",patient);q.Parameters.AddWithValue("@ProductName","TEST ONLY");
        q.Parameters.Add("@ProductDisplayText",SqlDbType.NVarChar,-1).Value=new string('x',301);
        q.Parameters.AddWithValue("@Route","Oral");q.Parameters.AddWithValue("@FrequencyCode","ONCE");q.Parameters.AddWithValue("@FrequencyDisplay","Once");
        q.Parameters.AddWithValue("@Prn",false);q.Parameters.AddWithValue("@Directions","TEST ONLY");q.Parameters.AddWithValue("@Quantity",1);
        q.Parameters.AddWithValue("@QuantityUnit","tablet");q.Parameters.AddWithValue("@AuthorizedRepeats",0);q.Parameters.AddWithValue("@ActorUserId",7);
        Assert.Equal(53000,(await Assert.ThrowsAsync<SqlException>(()=>q.ExecuteNonQueryAsync())).Number);
        Assert.Empty(await Db.Repository().ListAsync(patient));
    }
}

public sealed class Step138PrescriptionDatabase:IAsyncLifetime
{
    internal PrescriptionDateDatabase Db{get;}=new();
    public async Task InitializeAsync()
    {
        await Db.InitializeAsync();
        try
        {
            foreach(var other in new[]{false,true})
            {
                await using var c=await Db.Factory(other).OpenConnectionAsync();
                await PrescriptionPrivateNoteFixture.Batches(c,"UPDATE dbo.Provider SET BillingNumber=N'001234';");
            }
        }
        catch{await Db.DisposeAsync();throw;}
    }
    public Task DisposeAsync()=>Db.DisposeAsync();
}
