using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.PatientCpp;
using MicroEMR.Application.PatientMedications.Contracts;
using MicroEMR.Application.PatientMedications.Services;
using MicroEMR.Application.ReadAudit;
using MicroEMR.Infrastructure.PatientMedications;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class MedicationClassificationCppTests(MedicationClassificationDatabase db) : IClassFixture<MedicationClassificationDatabase>
{
    [Fact]
    public async Task SqlSourceDefaultsToOngoingAndExplicitPrintScopesIncludeAllMatchingActiveRows()
    {
        var f=new CppSelectivePrintTests.Fixture();
        // Use the CPP fixture patient UID in the isolated real medication store.
        await db.Execute(false,$"INSERT dbo.Patient(PatientUid) VALUES('{f.Patient.PatientUid}')");
        var actor=await db.Clinical.SeedUser(false);
        var medications=new PatientMedicationService(db.Repository());
        foreach(var classification in new string?[]{"Ongoing","Acute",null})
            for(var i=0;i<7;i++) await medications.CreateAsync(f.Patient.PatientUid,new(){MedicationName=$"TEST ONLY - {classification ?? "Unknown"} {i}",Classification=classification},actor.Actor,null);
        var stopped=await medications.CreateAsync(f.Patient.PatientUid,new(){MedicationName="TEST ONLY - stopped",Classification="Ongoing"},actor.Actor,null);
        await medications.DiscontinueAsync(f.Patient.PatientUid,stopped.MedicationUid,new(){RowVersion=stopped.RowVersion},actor.Actor);
        var source=CppSelectivePrintTests.Source(f.Patient.PatientUid,null!,Stub<IPatientChartReadAuditService>((_,_)=>Task.FromResult(Guid.NewGuid())),medication:medications);
        f.ActualSource=source;
        var initial=(await source.GetAsync(f.Patient.PatientUid,"step102"))!;
        Assert.Equal(5,initial.Medications.Items.Count);Assert.Equal(7,initial.Medications.TotalCount);
        Assert.All(initial.Medications.Items,x=>Assert.Equal("Ongoing",x.Classification));
        var before=await db.Snapshot(false);
        foreach(var scope in new[]{"Ongoing","Acute","Unclassified","AllActive"})
        {
            var data=(await source.GetForPrintAsync(f.Patient.PatientUid,["Medications"],"step102",default,scope))!;
            Assert.Equal(scope=="AllActive"?21:7,data.Medications.Items.Count);
            if(scope!="AllActive") Assert.All(data.Medications.Items,x=>Assert.Equal(scope,x.Classification));
            var html=Encoding.UTF8.GetString((await f.Service.PrintAsync(f.Patient.PatientUid,new(){ClinicianUid=f.ClinicianUid,Categories=["Medications"],MedicationScope=scope},"step102"))!);
            Assert.Contains($"Medications - {MedicationPrintScopes.Label(scope)}",html);
            Assert.DoesNotContain("TEST ONLY - stopped",html);
            foreach(var label in new[]{"Ongoing","Acute","Unknown"})
            {
                if(scope=="AllActive" || scope==(label=="Unknown"?"Unclassified":label)) Assert.Contains($"TEST ONLY - {label} 6",html);
                else Assert.DoesNotContain($"TEST ONLY - {label}",html);
            }
        }
        Assert.Equal(before,await db.Snapshot(false));
        Assert.Equal(22,(await medications.GetByPatientUidAsync(f.Patient.PatientUid)).Count);
        await Assert.ThrowsAsync<ArgumentException>(()=>source.GetForPrintAsync(f.Patient.PatientUid,["Medications"],"step102",default,"invalid"));
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.PrintAsync(f.Patient.PatientUid,new(){ClinicianUid=f.ClinicianUid,Categories=["Medications"],MedicationScope="invalid"},"step102"));
    }

    [Fact]
    public async Task UnknownOnlyPatientHasSpecificOngoingEmptyStateAndFullListRemainsReadable()
    {
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);
        var service=new PatientMedicationService(db.Repository());
        await service.CreateAsync(patient,new(){MedicationName="TEST ONLY - unknown"},actor.Actor,null);
        var source=CppSelectivePrintTests.Source(patient,null!,Stub<IPatientChartReadAuditService>((_,_)=>Task.FromResult(Guid.NewGuid())),medication:service);
        Assert.Empty((await source.GetAsync(patient,"step102"))!.Medications.Items);
        Assert.Single((await source.GetForPrintAsync(patient,["Medications"],"step102",default,"Unclassified"))!.Medications.Items);
        Assert.Null(Assert.Single(await service.GetByPatientUidAsync(patient)).Classification);
        var f=new CppSelectivePrintTests.Fixture();
        var html=Encoding.UTF8.GetString((await f.Print("Medications"))!);
        Assert.Contains("No ongoing medications recorded.",html);
        Assert.DoesNotContain("No medications recorded",html);
    }
    private static T Stub<T>(Func<string,object?[],object> call) where T:class
    {var value=DispatchProxy.Create<T,CppSelectivePrintTests.Proxy>();((CppSelectivePrintTests.Proxy)(object)value).Call=call;return value;}
}

public sealed class MedicationClassificationPrescriptionTests
{
    [Fact]
    public async Task AdditiveUpgradeAndListClassificationLeaveFinalizedSignedPrescriptionAndArtifactUntouched()
    {
        var db=new PrescriptionRefillSqlDatabase();
        try
        {
            await db.InitializeAsync();
            var patient=await db.SeedPatient();
            var draft=await db.Repository.CreateAsync(patient,PrescriptionRefillInstructionsTests.Draft(),7);
            var finalized=(await db.Repository.SignAndFinalizeAsync(patient,draft.PrescriptionUid,draft.RowVersion,7))!;
            var originalArtifact=(await db.Repository.GetArtifactAsync(patient,finalized.PrescriptionUid))!;
            var original=await db.Read<string>("SELECT (SELECT * FROM dbo.PatientPrescription WHERE PrescriptionUid=@Uid FOR JSON PATH,INCLUDE_NULL_VALUES)",finalized.PrescriptionUid);
            foreach(var file in new[]{"patient_medication_stored_procedures.sql","tenant-clinical/migrations/0059-medication-discontinuation-concurrency.sql","tenant-clinical/migrations/0077-medication-classification.sql"})
                foreach(var batch in Regex.Split(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"database",file)),@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase))
                    if(!string.IsNullOrWhiteSpace(batch)) await db.Execute(batch);
            var medications=new PatientMedicationRepository(db.MedicationFactory,NullLogger<PatientMedicationRepository>.Instance);
            var row=await medications.CreateAsync(patient,new(){MedicationName="TEST ONLY - safe list",Classification="Ongoing"},7,null);
            await medications.UpdateAsync(patient,row.MedicationUid,MedicationClassificationTests.Update(row,"Acute"),7);
            Assert.Equal(original,await db.Read<string>("SELECT (SELECT * FROM dbo.PatientPrescription WHERE PrescriptionUid=@Uid FOR JSON PATH,INCLUDE_NULL_VALUES)",finalized.PrescriptionUid));
            Assert.Equal(originalArtifact,await db.Repository.GetArtifactAsync(patient,finalized.PrescriptionUid));
            var forbidden=PrescriptionRefillInstructionsTests.Draft();forbidden.RowVersion=finalized.RowVersion;
            await Assert.ThrowsAsync<MicroEMR.Application.PatientPrescriptions.PatientPrescriptionConcurrencyException>(()=>db.Repository.UpdateAsync(patient,finalized.PrescriptionUid,forbidden,7));
        }
        finally {await db.DisposeAsync();}
    }
}
