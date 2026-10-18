using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO.Compression;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.PatientImmunizations;
using MicroEMR.Application.PatientCpp;
using MicroEMR.Core.Domain;
using MicroEMR.Infrastructure.PatientImmunizations;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ImmunizationFieldSqlTests(ImmunizationFieldDatabase db) : IClassFixture<ImmunizationFieldDatabase>
{
    internal static string Route(Guid patient, Guid? uid=null)=>$"api/patients/{patient}/immunizations"+(uid is null ? "" : $"/{uid}");
    internal static CreatePatientImmunizationRequest Draft(bool refused=false)=>new()
    {
        VaccineName="TEST ONLY vaccine",SourceType=refused?null:"HistoricalExternal",Notes="TEST ONLY retained notes <script>",
        ClinicalFields=new(){Refused=refused,DIN=refused?null:"00123456",Manufacturer=refused?null:"TEST ONLY manufacturer",ImmunizationTypeCode="Inf",
            AdministrationDate=refused?null:new(){Year=2000,Month=2},RefusalDate=refused?new(){Year=2001}:null}
    };
    internal static UpdatePatientImmunizationRequest Edit(PatientImmunizationResponse row,ImmunizationClinicalFields? fields)=>new()
    {
        VaccineName=row.VaccineName,SourceType=row.SourceType,AdministeredByName=row.AdministeredByName,SourceDescription=row.SourceDescription,
        DoseNumber=row.DoseNumber,Route=row.Route,Site=row.Site,LotNumber=row.LotNumber,EncounterUid=row.EncounterUid,Notes=row.Notes,RowVersion=row.RowVersion,ClinicalFields=fields
    };

    [Theory]
    [InlineData(false,2000,null,null)] [InlineData(false,2000,2,null)] [InlineData(false,2000,2,29)] [InlineData(false,null,null,null)]
    [InlineData(true,2000,null,null)] [InlineData(true,2000,2,null)] [InlineData(true,2000,2,29)] [InlineData(true,null,null,null)]
    public async Task RealApiPersistsFieldsBothOutcomesAndEveryDatePrecision(bool refused,int? year,int? month,int? day)
    {
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);
        await using var host=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,actor);using var client=host.Client(actor);
        var draft=Draft(refused);var date=new MicroEMR.Application.PatientClinicalHistory.ClinicalPartialDate{Year=year,Month=month,Day=day};
        if(refused)draft.ClinicalFields!.RefusalDate=date;else draft.ClinicalFields!.AdministrationDate=date;
        var response=await client.PostAsJsonAsync(Route(patient),draft);Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var row=(await response.Content.ReadFromJsonAsync<PatientImmunizationResponse>())!;
        var read=(await db.Repository().GetAsync(patient,row.ImmunizationUid))!;
        Assert.Equal(row.RowVersion,read.RowVersion);Assert.Equal(refused?"Refused":"Completed",read.Status);Assert.Equal(refused,read.ClinicalFields!.Refused);
        var stored=refused?read.ClinicalFields.RefusalDate:read.EffectiveAdministrationDate;
        Assert.Equal(year,stored?.Year);Assert.Equal(month,stored?.Month);Assert.Equal(day,stored?.Day);
        Assert.Equal(!refused && day.HasValue,read.AdministrationDate.HasValue);
        Assert.Equal(refused?null:"00123456",read.ClinicalFields.DIN);Assert.Equal(refused?null:"TEST ONLY manufacturer",read.ClinicalFields.Manufacturer);
        Assert.Equal("Inf",read.ClinicalFields.ImmunizationTypeCode);Assert.Equal("Influenza",read.ClinicalFields.ImmunizationType);
        Assert.Equal(actor.Actor,read.CreatedBy);Assert.Equal(patient,read.PatientUid);
        var update=Edit(read,read.ClinicalFields);update.Notes="TEST ONLY amended notes";
        var save=await client.PutAsJsonAsync(Route(patient,row.ImmunizationUid),update);Assert.Equal(HttpStatusCode.OK,save.StatusCode);
        var edited=(await save.Content.ReadFromJsonAsync<PatientImmunizationResponse>())!;Assert.NotEqual(row.RowVersion,edited.RowVersion);
        var audits=await db.Clinical.Audit(false,row.ImmunizationUid,"PatientImmunization");Assert.Equal(2,audits.Count);
        using var before=JsonDocument.Parse(audits[1].Old!);using var after=JsonDocument.Parse(audits[1].New);
        Assert.Equal(draft.Notes,before.RootElement.GetProperty("Notes").GetString());Assert.Equal(update.Notes,after.RootElement.GetProperty("Notes").GetString());
        Assert.Equal(actor.Actor,audits[1].Actor);Assert.Equal(actor.Subject,audits[1].Subject);Assert.Equal(patient,audits[1].Patient);
        Assert.Equal(db.Clinical.Tenant(false).TenantUid,after.RootElement.GetProperty("TenantUid").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(after.RootElement.GetProperty("RequestCorrelationId").GetString()));
        Assert.Equal(edited.RowVersion,after.RootElement.GetProperty("RowVersion").GetString());
        var error=await client.PostAsJsonAsync(Route(patient,row.ImmunizationUid)+"/entered-in-error",new MarkImmunizationEnteredInErrorRequest{RowVersion=edited.RowVersion,Reason="TEST ONLY duplicate"});
        Assert.Equal(HttpStatusCode.OK,error.StatusCode);var terminal=(await error.Content.ReadFromJsonAsync<PatientImmunizationResponse>())!;
        Assert.Equal("EnteredInError",terminal.Status);Assert.Equal(edited.Notes,terminal.Notes);Assert.Equal(refused,terminal.ClinicalFields!.Refused);Assert.Equal(edited.AdministrationDate,terminal.AdministrationDate);
        var snapshot=await db.Snapshot(false);Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync(Route(patient,row.ImmunizationUid),Edit(terminal,terminal.ClinicalFields))).StatusCode);
        Assert.Equal(snapshot,await db.Snapshot(false));
    }

    [Fact]
    public async Task PinnedCodeNamesAndStatusMatchWorkbookAndSqlExactly()
    {
        var path=Environment.GetEnvironmentVariable("MICROEMR_CT020_SOURCE") ?? "D:/Development/Maui .Net 10/Ontario EMR Specification/Foundation/emr core data set standard (cds-s) - 5.2 dfu - 2026-07-10/EMR Core Data Set Standard (CDS-S) - Data Dictionary (v2.1).xlsx";
        using var zip=ZipFile.OpenRead(path);XNamespace ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XDocument Read(string name){using var input=zip.GetEntry(name)!.Open();return XDocument.Load(input);}
        var strings=Read("xl/sharedStrings.xml").Descendants(ns+"si").Select(x=>string.Concat(x.Descendants(ns+"t").Select(t=>t.Value))).ToArray();
        var expected=Read("xl/worksheets/sheet4.xml").Descendants(ns+"row").Where(r=>int.TryParse((string?)r.Attribute("r"),out var i)&&(i>=231&&i<=277||i>=279&&i<=289)).Select(r=>
        {
            string Value(string col){var c=r.Elements(ns+"c").Single(x=>(string?)x.Attribute("r")==col+(string?)r.Attribute("r"));return strings[int.Parse(c.Element(ns+"v")!.Value)];}
            return (Code:Value("B"),Name:Value("C"),Status:Value("F"));
        }).ToArray();
        Assert.Equal(58,expected.Length);Assert.Equal(expected,ImmunizationTypes.All.Select(x=>(x.Code,x.Name,x.SourceStatus)));
        await using var c=await db.Clinical.Open(false);await using var q=new SqlCommand("SELECT Code,Name,SourceStatus,SourceVersion FROM dbo.ImmunizationTypeReference ORDER BY SourceRow",c);
        await using var reader=await q.ExecuteReaderAsync();var actual=new List<(string,string,string)>();
        while(await reader.ReadAsync()){actual.Add((reader.GetString(0),reader.GetString(1),reader.GetString(2)));Assert.Equal(ImmunizationTypes.SourceVersion,reader.GetString(3));}
        Assert.Equal(expected,actual);Assert.Equal(9,ImmunizationTypes.All.Count(x=>x.SourceStatus=="R"));
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);
        foreach(var type in ImmunizationTypes.All){var draft=Draft();draft.ClinicalFields!.ImmunizationTypeCode=type.Code;var row=await db.Repository().CreateAsync(patient,draft,actor.Actor);Assert.Equal(type.Display,row.ClinicalFields!.ImmunizationType);}
    }

    [Fact]
    public async Task LegacyUpgradeOmissionAndExplicitClearingPreserveHistory()
    {
        Assert.Equal(db.LegacyAudit,await db.LegacyAuditSnapshot());
        var row=(await db.Repository().GetAsync(db.LegacyPatient,db.LegacyUid))!;
        Assert.Equal(db.LegacyToken,row.RowVersion);Assert.Equal("1998-03-04",row.AdministrationDisplay);Assert.Null(row.ClinicalFields!.Refused);Assert.Null(row.ClinicalFields.DIN);
        var actor=await db.Clinical.SeedUser(false);var fields=Draft().ClinicalFields!;
        var partial=(await db.Repository().UpdateAsync(db.LegacyPatient,row.ImmunizationUid,Edit(row,fields),actor.Actor))!;
        Assert.Null(partial.AdministrationDate);Assert.Equal("2000-02",partial.AdministrationDisplay);
        var omitted=(await db.Repository().UpdateAsync(db.LegacyPatient,row.ImmunizationUid,Edit(partial,null),actor.Actor))!;
        Assert.Equal("00123456",omitted.ClinicalFields!.DIN);Assert.Equal("2000-02",omitted.AdministrationDisplay);
        var before=await db.Snapshot(false);var legacyNull=Edit(omitted,null);legacyNull.AdministrationDate=null;
        await Assert.ThrowsAsync<PatientImmunizationValidationException>(()=>db.Repository().UpdateAsync(db.LegacyPatient,row.ImmunizationUid,legacyNull,actor.Actor));Assert.Equal(before,await db.Snapshot(false));
        var cleared=(await db.Repository().UpdateAsync(db.LegacyPatient,row.ImmunizationUid,Edit(omitted,new()),actor.Actor))!;
        Assert.Null(cleared.ClinicalFields!.DIN);Assert.Equal("Not recorded",cleared.AdministrationDisplay);Assert.Null(cleared.ClinicalFields.Refused);
        var oldError=(await db.Repository().GetAsync(db.LegacyPatient,db.ErrorUid))!;Assert.Equal(db.ErrorToken,oldError.RowVersion);Assert.Equal("EnteredInError",oldError.Status);
    }

    [Fact]
    public async Task InvalidClinicalInputsAndOutcomeConversionNeverMutateOrAuditSuccess()
    {
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);
        await using var host=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,actor);using var client=host.Client(actor);
        foreach(var invalid in new Action<CreatePatientImmunizationRequest>[]
        {
            x=>x.ClinicalFields!.ImmunizationTypeCode="invented",x=>x.ClinicalFields!.ImmunizationTypeCode="inf",x=>x.ClinicalFields!.DIN=new('1',101),
            x=>x.ClinicalFields!.AdministrationDate=new(){Year=2000,Month=2,Day=30},x=>x.ClinicalFields!.AdministrationDate=new(){Month=2},
            x=>x.ClinicalFields!.AdministrationDate=new(){Year=9999},x=>x.ClinicalFields!.RefusalDate=new(){Year=2000},
            x=>x.ClinicalFields!.Refused=true,x=>x.AdministrationDate=new(2000,1,1)
        })
        {
            var draft=Draft();invalid(draft);var before=await db.Snapshot(false);
            Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(Route(patient),draft)).StatusCode);Assert.Equal(before,await db.Snapshot(false));
        }
        var row=await db.Repository().CreateAsync(patient,Draft(),actor.Actor);var refusal=await db.Repository().CreateAsync(patient,Draft(true),actor.Actor);
        foreach(var target in new[]{row,refusal})
        {
            var edit=Edit(target,Draft(target.Status!="Refused").ClinicalFields);edit.SourceType=target.Status=="Refused"?"HistoricalExternal":null;
            var before=await db.Snapshot(false);Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync(Route(patient,target.ImmunizationUid),edit)).StatusCode);Assert.Equal(before,await db.Snapshot(false));
        }
        foreach(var sql in new[]{
            $"EXEC dbo.PatientImmunization_Update @PatientUid='{patient}',@ImmunizationUid='{row.ImmunizationUid}',@VaccineName=N'TEST ONLY',@AdministrationDate='2000-01-01',@SourceType=N'HistoricalExternal',@Actor={actor.Actor},@ExpectedRowVersion=NULL",
            $"EXEC dbo.PatientImmunization_MarkEnteredInError @PatientUid='{patient}',@ImmunizationUid='{row.ImmunizationUid}',@Reason=N'TEST ONLY',@Actor={actor.Actor},@ExpectedRowVersion=NULL",
            $"EXEC dbo.PatientImmunization_Create @PatientUid='{patient}',@VaccineName=N'TEST ONLY',@SourceType=N'HistoricalExternal',@Actor={actor.Actor},@ClinicalFields=N'{{\"ImmunizationTypeCode\":\"invented\"}}'"
        }){var before=await db.Snapshot(false);await Assert.ThrowsAsync<SqlException>(()=>db.Execute(false,sql));Assert.Equal(before,await db.Snapshot(false));}
    }

    [Theory]
    [InlineData(false,false)] [InlineData(false,true)] [InlineData(true,true)]
    public async Task ConcurrentEditAndErrorAllowExactlyOneSuccess(bool firstError,bool secondError)
    {
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);var row=await db.Repository().CreateAsync(patient,Draft(true),actor.Actor);
        await using var host=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,actor);using var client=host.Client(actor);
        Task<HttpResponseMessage> Save(bool error)=>error?client.PostAsJsonAsync(Route(patient,row.ImmunizationUid)+"/entered-in-error",new MarkImmunizationEnteredInErrorRequest{RowVersion=row.RowVersion,Reason="TEST ONLY correction"}):client.PutAsJsonAsync(Route(patient,row.ImmunizationUid),Edit(row,row.ClinicalFields));
        var results=await Task.WhenAll(Save(firstError),Save(secondError));Assert.Single(results,x=>x.StatusCode==HttpStatusCode.OK);Assert.Single(results,x=>x.StatusCode==HttpStatusCode.Conflict);
        Assert.Equal(2,(await db.Clinical.Audit(false,row.ImmunizationUid,"PatientImmunization")).Count);
        var before=await db.Snapshot(false);Assert.Equal(HttpStatusCode.Conflict,(await Save(false)).StatusCode);Assert.Equal(before,await db.Snapshot(false));
    }

    [Fact]
    public async Task AuthorizationTrustedActorPatientAndBothTenantDirectionsAreEnforced()
    {
        var patient=await db.Clinical.SeedPatient(false);var foreignPatient=await db.Clinical.SeedPatient(true);var otherPatient=await db.Clinical.SeedPatient(false);
        var writer=await db.Clinical.SeedUser(false);var reader=await db.Clinical.SeedUser(false,false);var hidden=await db.Clinical.SeedUser(false,true,false);var inactive=await db.Clinical.SeedUser(false);var writerB=await db.Clinical.SeedUser(true);
        await db.Execute(false,$"UPDATE dbo.ApplicationUser SET IsActive=0 WHERE UserId={inactive.Actor}");
        var unresolved=new FamilyHistoryAcceptanceDatabase.User("immunization-unresolved-"+Guid.NewGuid(),long.MaxValue,true);
        await using var ha=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,writer,reader,hidden,inactive,unresolved);await using var hb=await FamilyHistoryAcceptanceHost.Start(db.Clinical,true,writerB);
        using var a=ha.Client(writer);using var b=hb.Client(writerB);using var read=ha.Client(reader);using var denied=ha.Client(hidden);using var disabled=ha.Client(inactive);using var missing=ha.Client(unresolved);
        var response=await a.PostAsJsonAsync(Route(patient),new{VaccineName="TEST ONLY trusted",SourceType="HistoricalExternal",AdministrationDate="2000-01-01",Actor=long.MaxValue,RequestCorrelationId="spoofed"});
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);var row=(await response.Content.ReadFromJsonAsync<PatientImmunizationResponse>())!;Assert.Equal(writer.Actor,row.CreatedBy);
        Assert.DoesNotContain("spoofed",Assert.Single(await db.Clinical.Audit(false,row.ImmunizationUid,"PatientImmunization")).New);
        var foreign=await db.Repository(true).CreateAsync(foreignPatient,Draft(true),writerB.Actor);
        var before=await db.Snapshot(false);var beforeB=await db.Snapshot(true);
        Assert.Equal(HttpStatusCode.OK,(await read.GetAsync(Route(patient))).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await denied.GetAsync(Route(patient))).StatusCode);
        foreach(var client in new[]{read,disabled,missing})
        {
            Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync(Route(patient),Draft())).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.PutAsJsonAsync(Route(patient,row.ImmunizationUid),Edit(row,null))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync(Route(patient,row.ImmunizationUid)+"/entered-in-error",new MarkImmunizationEnteredInErrorRequest{RowVersion=row.RowVersion,Reason="TEST ONLY"})).StatusCode);
        }
        foreach(var (client,p,uid,record) in new[]{(a,otherPatient,row.ImmunizationUid,row),(b,patient,row.ImmunizationUid,row),(a,foreignPatient,foreign.ImmunizationUid,foreign)})
        {
            Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(Route(p,uid))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await client.PutAsJsonAsync(Route(p,uid),Edit(record,record.ClinicalFields))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync(Route(p,uid)+"/entered-in-error",new MarkImmunizationEnteredInErrorRequest{RowVersion=record.RowVersion,Reason="TEST ONLY"})).StatusCode);
            Assert.Empty((await client.GetFromJsonAsync<List<PatientImmunizationResponse>>(Route(p)))!);
        }
        var noToken=Edit(row,null);noToken.RowVersion="";Assert.Equal(HttpStatusCode.BadRequest,(await a.PutAsJsonAsync(Route(patient,row.ImmunizationUid),noToken)).StatusCode);
        noToken.RowVersion="not-base64";Assert.Equal(HttpStatusCode.BadRequest,(await a.PutAsJsonAsync(Route(patient,row.ImmunizationUid),noToken)).StatusCode);
        Assert.Equal(before,await db.Snapshot(false));Assert.Equal(beforeB,await db.Snapshot(true));
        var encounter=Guid.NewGuid();await db.Execute(false,$"INSERT dbo.PatientEncounter VALUES('{encounter}','{otherPatient}')");var wrongEncounter=Draft();wrongEncounter.EncounterUid=encounter;
        Assert.Equal(HttpStatusCode.NotFound,(await a.PostAsJsonAsync(Route(patient),wrongEncounter)).StatusCode);
        await db.Execute(false,$"UPDATE dbo.Patient SET IsDeleted=1 WHERE PatientUid='{patient}'");before=await db.Snapshot(false);
        Assert.Empty((await a.GetFromJsonAsync<List<PatientImmunizationResponse>>(Route(patient)))!);Assert.Equal(HttpStatusCode.NotFound,(await a.GetAsync(Route(patient,row.ImmunizationUid))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await a.PutAsJsonAsync(Route(patient,row.ImmunizationUid),Edit(row,null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await a.PostAsJsonAsync(Route(patient),Draft())).StatusCode);Assert.Equal(before,await db.Snapshot(false));
    }

    [Fact]
    public async Task CppUsesOwnedFullSourceAndPrintsSeparateRefusalsWithoutLosingPrecision()
    {
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);
        for(var i=0;i<8;i++){var draft=Draft();draft.VaccineName=$"TEST ONLY print {i}";draft.Notes=string.Join("\n",Enumerable.Range(0,18).Select(n=>$"Line {n}"))+$"\nTail{i} <script>";await db.Repository().CreateAsync(patient,draft,actor.Actor);}
        var refusal=Draft(true);refusal.VaccineName="TEST ONLY refusal";await db.Repository().CreateAsync(patient,refusal,actor.Actor);
        var terminal=await db.Repository().CreateAsync(patient,Draft(),actor.Actor);await db.Repository().MarkEnteredInErrorAsync(patient,terminal.ImmunizationUid,new(){RowVersion=terminal.RowVersion,Reason="TEST ONLY excluded"},actor.Actor);
        var calls=0;var auditCalls=0;var repository=db.Repository();
        var service=CppSelectivePrintTests.Stub<IPatientImmunizationService>((_,args)=>{calls++;return repository.ListAsync((Guid)args[0]!, (string)args[1]!);});
        var source=CppSelectivePrintTests.Source(patient,null!,CppSelectivePrintTests.Stub<MicroEMR.Application.ReadAudit.IPatientChartReadAuditService>((_,_)=>{auditCalls++;return Task.FromResult(Guid.NewGuid());}),immunizations:service);
        var summary=(await source.GetAsync(patient,"test"))!;Assert.Equal(5,summary.Immunizations.Items.Count);Assert.Equal(8,summary.Immunizations.TotalCount);
        Assert.All(summary.Immunizations.Items,x=>{Assert.Equal("2000-02",x.AdministrationDisplay);Assert.Equal("00123456",x.DIN);Assert.Equal("Influenza",x.ImmunizationType);});
        Assert.Null(summary.ImmunizationsForPrint);Assert.DoesNotContain("Tail",JsonSerializer.Serialize(summary));
        var fixture=new CppSelectivePrintTests.Fixture{ActualSource=source};fixture.Patient.PatientUid=patient;
        var prior=calls;var priorAudit=auditCalls;var html=System.Text.Encoding.UTF8.GetString((await fixture.Print("Immunizations"))!);
        Assert.Equal(prior+1,calls);Assert.Equal(priorAudit+1,auditCalls);
        foreach(var text in new[]{"TEST ONLY print 7","TEST ONLY manufacturer","00123456","Influenza","2000-02","Refused immunizations","TEST ONLY refusal","2001","Tail7","&lt;script&gt;"})Assert.Contains(text,html);
        Assert.DoesNotContain("TEST ONLY excluded",html);Assert.DoesNotContain("2000-02-01",html);Assert.DoesNotContain("Row Version",html);Assert.DoesNotContain("<script>",html);
        var details=(await source.GetForPrintAsync(patient,["Immunizations"],"test"))!.ImmunizationsForPrint!;
        Assert.Equal(9,details.TotalCount);Assert.Single(details.Items,x=>x.Outcome=="Refused");Assert.Equal("Not recorded",details.Items.Single(x=>x.Outcome=="Refused").AdministrationDate);
        await using(var renderer=new MicroEMR.Infrastructure.ClinicalOutput.PlaywrightPdfRenderer(Microsoft.Extensions.Logging.Abstractions.NullLogger<MicroEMR.Infrastructure.ClinicalOutput.PlaywrightPdfRenderer>.Instance))
        {
            using var pdf=UglyToad.PdfPig.PdfDocument.Open(await renderer.RenderAsync(html));Assert.True(pdf.NumberOfPages>1);
            var words=pdf.GetPages().SelectMany(x=>x.GetWords()).ToArray();Assert.Contains(words,x=>x.Text=="Tail7");Assert.Contains(words,x=>x.Text=="Refused");
        }
        var unselected=calls;await source.GetForPrintAsync(patient,["Allergies"],"test");Assert.Equal(unselected,calls);
        var foreignService=CppSelectivePrintTests.Stub<IPatientImmunizationService>((_,_)=>Task.FromResult<IReadOnlyList<PatientImmunizationResponse>>([new(){PatientUid=Guid.NewGuid(),ImmunizationUid=Guid.NewGuid(),VaccineName="FOREIGN",Status="EnteredInError",RowVersion="token"}]));
        var foreign=CppSelectivePrintTests.Source(patient,null!,CppSelectivePrintTests.Stub<MicroEMR.Application.ReadAudit.IPatientChartReadAuditService>((_,_)=>Task.FromResult(Guid.NewGuid())),immunizations:foreignService);
        Assert.Equal(PatientCppSectionStates.Unavailable,(await foreign.GetAsync(patient,"test"))!.Immunizations.State);
        fixture.ActualSource=foreign;await Assert.ThrowsAsync<CppPrintUnavailableException>(()=>fixture.Print("Immunizations"));
    }

    [Fact]
    public async Task RequiredAuditFailureRollsBackAllThreeMutationsAndCannotReportSuccess()
    {
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);var row=await db.Repository().CreateAsync(patient,Draft(true),actor.Actor);
        await using var host=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,actor);using var client=host.Client(actor);
        var before=await db.Snapshot(false);
        await db.Execute(false,"CREATE TRIGGER dbo.ImmunizationAuditFailure ON dbo.AuditLog AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE EntityName=N'PatientImmunization') THROW 52997,'Synthetic audit failure.',1; END;");
        try
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.PostAsJsonAsync(Route(patient),Draft())).StatusCode);
            Assert.Equal(before,await db.Snapshot(false));
            Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.PutAsJsonAsync(Route(patient,row.ImmunizationUid),Edit(row,row.ClinicalFields))).StatusCode);
            Assert.Equal(before,await db.Snapshot(false));
            Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.PostAsJsonAsync(Route(patient,row.ImmunizationUid)+"/entered-in-error",new MarkImmunizationEnteredInErrorRequest{RowVersion=row.RowVersion,Reason="TEST ONLY"})).StatusCode);
            Assert.Equal(before,await db.Snapshot(false));
        }
        finally{await db.Execute(false,"DROP TRIGGER dbo.ImmunizationAuditFailure");}
        await db.Execute(false,"EXEC sp_rename N'dbo.AuditLog',N'ImmunizationHeldAudit';");
        try{await Assert.ThrowsAsync<SqlException>(()=>db.Repository().CreateAsync(patient,Draft(),actor.Actor));}
        finally{await db.Execute(false,"EXEC sp_rename N'dbo.ImmunizationHeldAudit',N'AuditLog';");}
        Assert.Equal(before,await db.Snapshot(false));
        await db.Execute(false,$"BEGIN TRANSACTION; EXEC dbo.PatientImmunization_Create @PatientUid='{patient}',@VaccineName=N'TEST ONLY outer rollback',@AdministrationDate='2000-01-01',@SourceType=N'HistoricalExternal',@Actor={actor.Actor}; ROLLBACK;");
        Assert.Equal(before,await db.Snapshot(false));
    }
}

public sealed class ImmunizationFieldDatabase : IAsyncLifetime
{
    internal FamilyHistoryAcceptanceDatabase Clinical {get;}=new();
    internal Guid LegacyPatient,LegacyUid,ErrorUid;internal string LegacyToken="",ErrorToken="",LegacyAudit="";
    public async Task InitializeAsync()
    {
        await Clinical.InitializeAsync();
        try
        {
            foreach(var other in new[]{false,true})
            {
                await Execute(other,"CREATE TABLE dbo.PatientEncounter(EncounterUid UNIQUEIDENTIFIER PRIMARY KEY,PatientUid UNIQUEIDENTIFIER NOT NULL);");
                foreach(var file in new[]{"0043-patient-chart-read-audit.sql","0041-patient-clinical-history.sql","0078-clinical-history-fields.sql","0047-patient-immunization-history.sql"})await Apply(other,file);
                if(!other)
                {
                    LegacyPatient=await Clinical.SeedPatient(false);var actor=await Clinical.SeedUser(false);
                    (LegacyUid,LegacyToken)=await LegacyCreate(actor.Actor,false);
                    (ErrorUid,ErrorToken)=await LegacyCreate(actor.Actor,true);
                    LegacyAudit=await LegacyAuditSnapshot();
                }
                await Apply(other,"0080-immunization-fields.sql");
            }
        }
        catch{await Clinical.DisposeAsync();throw;}
    }
    public Task DisposeAsync()=>Clinical.DisposeAsync();
    private async Task<(Guid,string)> LegacyCreate(long actor,bool error)
    {
        await using var c=await Clinical.Open(false);
        await using var q=new SqlCommand("EXEC dbo.PatientImmunization_Create @PatientUid=@Patient,@VaccineName=N'TEST ONLY legacy',@AdministrationDate='1998-03-04',@SourceType=N'HistoricalExternal',@Actor=@Actor",c);
        q.Parameters.AddWithValue("@Patient",LegacyPatient);q.Parameters.AddWithValue("@Actor",actor);
        Guid uid;string version;
        await using(var r=await q.ExecuteReaderAsync()){await r.ReadAsync();uid=r.GetGuid(r.GetOrdinal("ImmunizationUid"));version=Convert.ToBase64String((byte[])r["RowVersion"]);}
        if(error)
        {
            await using var change=new SqlCommand("EXEC dbo.PatientImmunization_MarkEnteredInError @PatientUid=@Patient,@ImmunizationUid=@Uid,@Reason=N'TEST ONLY old correction',@ExpectedRowVersion=@Version,@Actor=@Actor",c);
            change.Parameters.AddWithValue("@Patient",LegacyPatient);change.Parameters.AddWithValue("@Uid",uid);change.Parameters.AddWithValue("@Version",Convert.FromBase64String(version));change.Parameters.AddWithValue("@Actor",actor);
            await using var r=await change.ExecuteReaderAsync();await r.ReadAsync();version=Convert.ToBase64String((byte[])r["RowVersion"]);
        }
        return(uid,version);
    }
    internal PatientImmunizationRepository Repository(bool other=false)=>new(Clinical.Factory(other));
    internal async Task Apply(bool other,string file)
    {
        var script=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"database","tenant-clinical","migrations",file));
        foreach(var batch in Regex.Split(script,@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase))if(!string.IsNullOrWhiteSpace(batch))await Execute(other,batch);
    }
    internal async Task Execute(bool other,string sql){await using var c=await Clinical.Open(other);await using var q=new SqlCommand(sql,c);await q.ExecuteNonQueryAsync();}
    internal async Task<string> LegacyAuditSnapshot()
    {
        await using var c=await Clinical.Open(false);await using var q=new SqlCommand("SELECT (SELECT * FROM dbo.AuditLog WHERE EntityName=N'PatientImmunization' AND PatientId=(SELECT PatientId FROM dbo.Patient WHERE PatientUid=@Patient) ORDER BY AuditLogId FOR JSON PATH,INCLUDE_NULL_VALUES)",c);q.Parameters.AddWithValue("@Patient",LegacyPatient);return(string)(await q.ExecuteScalarAsync())!;
    }
    internal async Task<string> Snapshot(bool other)
    {
        await using var c=await Clinical.Open(other);await using var q=new SqlCommand("SELECT (SELECT * FROM dbo.PatientImmunization ORDER BY ImmunizationUid FOR JSON PATH,INCLUDE_NULL_VALUES) Clinical,(SELECT * FROM dbo.AuditLog ORDER BY AuditLogId FOR JSON PATH,INCLUDE_NULL_VALUES) Audit FOR JSON PATH,WITHOUT_ARRAY_WRAPPER",c);
        await using var r=await q.ExecuteReaderAsync();var text=new System.Text.StringBuilder();while(await r.ReadAsync())text.Append(r.GetString(0));return text.ToString();
    }
}
