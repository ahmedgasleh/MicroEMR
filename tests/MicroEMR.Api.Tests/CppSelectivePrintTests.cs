using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.ClinicConfiguration;
using MicroEMR.Application.PatientClinicalHistory;
using MicroEMR.Application.PatientCpp;
using MicroEMR.Application.PatientProblems.Contracts;
using MicroEMR.Application.PatientProblems.Services;
using MicroEMR.Application.Patients.Contracts;
using MicroEMR.Application.Patients.Repositories;
using MicroEMR.Application.Providers;
using MicroEMR.Application.ReadAudit;
using MicroEMR.Infrastructure.ClinicalOutput;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class CppSelectivePrintTests
{
    [Fact]
    public async Task OneOperationPrintsCompleteSelectedCategoriesInCatalogOrderWithRequiredContext()
    {
        var f = new Fixture();
        var bytes = await f.Print("History","Problems");
        var html = Encoding.UTF8.GetString(bytes!);
        Assert.Equal(1,f.RenderCalls); Assert.Equal(1,f.NumberCalls);
        Assert.Contains("Problem 11",html); Assert.Contains("Status",html);
        Assert.Contains("Long history line 599",html); Assert.Contains("&lt;script&gt;",html);
        Assert.DoesNotContain("<script>",html);
        Assert.DoesNotContain("Unselected allergen",html);
        Assert.True(html.IndexOf("Active Problems",StringComparison.Ordinal)<html.IndexOf("Past Medical and Surgical History",StringComparison.Ordinal));
        foreach (var text in new[] { "Clinic Legal Name","10 Clinic Street","416-111-1111","Dr Clinician","Physician","Family Medicine",
            "Ada Lee","1234567890 AB","20 Patient Street","Toronto","M1M1M1","416-999-9999","October 8, 2026" }) Assert.Contains(text,html);
        Assert.DoesNotContain("DOB",html); Assert.DoesNotContain(f.Patient.PatientUid.ToString(),html);
        Assert.DoesNotContain("SECRET-CHART",html);
    }

    [Fact]
    public async Task OneCategoryIncludesEveryCppFieldAndPreferencesCannotSuppressIt()
    {
        var f=new Fixture();
        // No preference dependency: print reads the complete projection, never the hidden DOM/summary.
        var html=Encoding.UTF8.GetString((await f.Print("Allergies"))!);
        foreach(var text in new[] {"Unselected allergen","Reaction","Recorded reaction","Severity","Recorded severity"}) Assert.Contains(text,html);
        Assert.DoesNotContain("Problem 11",html); Assert.DoesNotContain("Past Medical and Surgical History",html);
        Assert.Equal(new[] {"Allergies"},f.Requested);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Bogus")]
    [InlineData("problems")]
    public async Task InvalidCategoryFailsBeforeSourceRead(string key)
    {
        var f=new Fixture();
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Print(key==""?[]:[key]));
        Assert.Equal(0,f.SourceCalls); Assert.Equal(0,f.RenderCalls);
    }

    [Fact]
    public async Task DuplicateSelectionsAreRejected()
    { var f=new Fixture(); await Assert.ThrowsAsync<ArgumentException>(()=>f.Print("Problems","Problems")); Assert.Equal(0,f.RenderCalls); }

    [Theory]
    [InlineData("permission")]
    [InlineData("actor")]
    [InlineData("wrong-patient")]
    [InlineData("wrong-summary")]
    [InlineData("wrong-provider")]
    [InlineData("inactive-provider")]
    [InlineData("missing-clinic")]
    [InlineData("unavailable")]
    [InlineData("wrong-history-patient")]
    public async Task RestrictedUnavailableOrWrongContextProducesNoPdf(string failure)
    {
        var f=new Fixture {Failure=failure};
        await Assert.ThrowsAnyAsync<Exception>(()=>f.Print(failure=="wrong-history-patient"?"History":"Problems"));
        Assert.Equal(0,f.RenderCalls); Assert.Equal(0,f.NumberCalls);
    }

    [Fact]
    public async Task MissingPatientInAnotherTenantStoreCannotBePrinted()
    {
        var a=new Fixture(); var b=new Fixture();
        Assert.Null(await b.Service.PrintAsync(a.Patient.PatientUid,new() {ClinicianUid=b.ClinicianUid,Categories=["Problems"]},"trace"));
        Assert.Equal(0,b.RenderCalls);
    }

    [Theory]
    [InlineData("NotDocumented","No eligible records documented.")]
    [InlineData("ExplicitlyNone","No Known Allergies")]
    public async Task EmptyAndExplicitNoneRemainDistinct(string state,string expected)
    {
        var f=new Fixture(); f.Data=f.Data with {Allergies=new(state,[],0)};
        Assert.Contains(expected,Encoding.UTF8.GetString((await f.Print("Allergies"))!));
    }

    [Fact]
    public async Task MissingContactFieldsAreHonestAndOrphanHealthCardVersionIsNotInvented()
    {
        var f=new Fixture(); f.Patient.PhoneNumber=null; f.Patient.HealthCardNumber=null;
        f.Patient.AddressLine1=null; f.Patient.City=null; f.Patient.Province=null; f.Patient.PostalCode=null;
        var html=Encoding.UTF8.GetString((await f.Print("Problems"))!);
        Assert.Contains("Patient address:</dt><dd>Not recorded",html);
        Assert.Contains("Patient phone:</dt><dd>Not recorded",html);
        Assert.DoesNotContain("Not recorded AB",html);
    }

    [Fact]
    public async Task FullReadLoadsOnlyRequestedSourceRemovesFiveRecordCapAndAuditsOnce()
    {
        var patient=Guid.NewGuid(); var audits=0; var reads=0;
        var rows=Enumerable.Range(0,12).Select(i=>new PatientProblemResponse {PatientUid=patient,PatientProblemUid=Guid.NewGuid(),
            ProblemName=$"Diagnosis {i}",ProblemStatus="Active",OnsetDate=new DateTime(2026,1,1).AddDays(i)}).ToArray();
        var service=Source(patient,Stub<IPatientProblemService>((method,args)=>
        { Assert.Equal("GetByPatientUidAsync",method); Assert.Equal(patient,args[0]); reads++; return Task.FromResult<IReadOnlyList<PatientProblemResponse>>(rows); }),
            Stub<IPatientChartReadAuditService>((_,args)=> { Assert.Equal(patient,args[0]); Assert.Equal("trace",args[1]); audits++; return Task.FromResult(Guid.NewGuid()); }));
        var print=(await service.GetForPrintAsync(patient,["Problems"],"trace"))!;
        Assert.Equal(12,print.Problems.Items.Count); Assert.Equal(12,print.Problems.TotalCount);
        Assert.Equal("Diagnosis 11",print.Problems.Items[0].DisplayName);
        Assert.Equal(1,reads); Assert.Equal(1,audits);
        Assert.Empty(print.Allergies.Items); Assert.Empty(print.Documents.Items);
        Assert.All(rows,x=>Assert.Equal("Active",x.ProblemStatus));
    }

    [Fact]
    public async Task AuditFailureAndRestrictedSourceStopBeforeClinicalRead()
    {
        var patient=Guid.NewGuid();var reads=0;
        var problem=Stub<IPatientProblemService>((_,_)=>{reads++;throw new Exception("Should not read");});
        var badAudit=Stub<IPatientChartReadAuditService>((_,_)=>throw new InvalidOperationException("Audit unavailable"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Source(patient,problem,badAudit).GetForPrintAsync(patient,["Problems"],"trace"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Source(patient,problem,badAudit).GetForPrintAsync(patient,["Documents"],"trace"));
        Assert.Equal(0,reads);
    }

    [Fact]
    public async Task SourceOwnershipFailureCannotBecomeCompletePrintableContent()
    {
        var patient=Guid.NewGuid();
        var source=Source(patient,Stub<IPatientProblemService>((_,_)=>Task.FromResult<IReadOnlyList<PatientProblemResponse>>(
            [new() {PatientUid=Guid.NewGuid(),ProblemStatus="Active",ProblemName="Other patient"}])),
            Stub<IPatientChartReadAuditService>((_,_)=>Task.FromResult(Guid.NewGuid())));
        var result=(await source.GetForPrintAsync(patient,["Problems"],"trace"))!;
        Assert.Equal(PatientCppSectionStates.Unavailable,result.Problems.State); Assert.Empty(result.Problems.Items);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(12)]
    public async Task RealPdfPaginationUsesActualPageTotalAndPreservesEveryPageContent(int count)
    {
        var builder=new PdfDocumentBuilder();var font=builder.AddStandard14Font(Standard14Font.Helvetica);
        for(var i=1;i<=count;i++) builder.AddPage(PageSize.Letter).AddText($"Complete page {i} ending",12,new PdfPoint(40,100),font);
        var bytes=await new PdfPageNumberer(NullLogger<PdfPageNumberer>.Instance).NumberAsync(builder.Build());
        using var document=PdfDocument.Open(bytes);
        Assert.Equal(count,document.NumberOfPages);
        for(var i=1;i<=count;i++)
        {
            var page=document.GetPage(i);
            Assert.Contains($"Complete page {i} ending",page.Text);Assert.EndsWith($"{i}/{count}",page.Text);
            Assert.Equal(612,page.Width);Assert.Equal(792,page.Height);
            Assert.All(page.Letters.Where(x=>x.StartBaseLine.Y<40),x=>Assert.InRange(x.StartBaseLine.Y,10,35));
        }
    }

    [Fact]
    public async Task InvalidPdfCannotReturnMisleadingNumberedOutput()
    { await Assert.ThrowsAsync<PdfRenderingException>(()=>new PdfPageNumberer(NullLogger<PdfPageNumberer>.Instance).NumberAsync("Not PDF"u8.ToArray())); }

    [Fact]
    public void RoutesRemainAuthenticatedPatientScopedAndWebPrintRequiresAntiforgery()
    {
        var api=typeof(MicroEMR.Api.Controllers.CppPrintController);
        Assert.Contains("{patientUid:guid}",api.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.NotEmpty(api.GetCustomAttributes<AuthorizeAttribute>());
        Assert.NotEmpty(typeof(MicroEMR.Web.Controllers.CppPrintController).GetCustomAttributes<AuthorizeAttribute>());
        Assert.NotNull(typeof(MicroEMR.Web.Controllers.CppPrintController).GetMethod("Print")!.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public async Task ApiReturnsOnePdfAndMapsUnavailableContentWithoutDisclosure()
    {
        var f=new Fixture();
        var controller=new MicroEMR.Api.Controllers.CppPrintController(f.Service,NullLogger<MicroEMR.Api.Controllers.CppPrintController>.Instance)
            {ControllerContext=new() {HttpContext=new DefaultHttpContext()}};
        Assert.IsType<FileContentResult>(await controller.Print(f.Patient.PatientUid,new() {Categories=["Problems"],ClinicianUid=f.ClinicianUid},default));
        f.Failure="unavailable";
        var result=Assert.IsType<ObjectResult>(await controller.Print(f.Patient.PatientUid,new() {Categories=["Problems"],ClinicianUid=f.ClinicianUid},default));
        Assert.Equal(503,result.StatusCode);
    }

    private static PatientCppService Source(Guid patient,IPatientProblemService problems,IPatientChartReadAuditService audit) => new(
        Stub<IPatientRepository>((_,args)=>Task.FromResult<PatientDetailsResponse?>((Guid)args[0]! == patient ? new() {PatientUid=patient}:null)),
        problems,null!,null!,null!,null!,null!,null!,null!,null!,null!,
        Stub<ICurrentUserPermissionService>((_,_)=>Task.FromResult<IReadOnlySet<string>>(new HashSet<string> {PermissionKeys.PatientsView})),audit,
        NullLogger<PatientCppService>.Instance);
    public class Proxy : DispatchProxy
    { public Func<string,object?[],object> Call=null!; protected override object Invoke(MethodInfo? method,object?[]? args)=>Call(method!.Name,args!); }
    private static T Stub<T>(Func<string,object?[],object> call) where T:class
    { var proxy=DispatchProxy.Create<T,Proxy>();((Proxy)(object)proxy).Call=call;return proxy; }

    private sealed class Fixture
    {
        public PatientDetailsResponse Patient=new() {PatientUid=Guid.NewGuid(),FirstName="Ada",LastName="Lee",DateOfBirth=new(1980,1,1),
            ChartNumber="SECRET-CHART",HealthCardNumber="1234567890",HealthCardVersion="AB",AddressLine1="20 Patient Street",City="Toronto",Province="ON",PostalCode="M1M1M1",PhoneNumber="416-999-9999"};
        public Guid ClinicianUid=Guid.NewGuid();public string Failure="";public int RenderCalls,NumberCalls,SourceCalls;public IReadOnlyList<string> Requested=[];
        public PatientCppSummaryResponse Data;
        public CppPrintService Service;
        public Fixture()
        {
            Data=new(Patient.PatientUid,new("Ada Lee",null,new(1980,1,1),46,null,null,null),
                PatientCppSection<PatientCppProblem>.From(Enumerable.Range(0,12).Select(i=>new PatientCppProblem(Guid.NewGuid(),$"Problem {i}","Active",new(2026,1,1))).ToArray(),12),
                PatientCppSection<PatientCppAllergy>.From([new(Guid.NewGuid(),"Unselected allergen","Active","Recorded reaction","Recorded severity")],1),
                PatientCppSection<PatientCppMedication>.From([],0),PatientCppSection<PatientCppPrescription>.From([],0),
                PatientCppSection<PatientCppImmunization>.From([],0),PatientCppSection<PatientCppResult>.From([],0),PatientCppSection<PatientCppVitals>.From([],0),
                PatientCppSection<PatientCppEncounter>.From([],0),PatientCppSection<PatientCppReferral>.From([],0),PatientCppSection<PatientCppDocument>.From([],0));
            Service=new(Stub<IPatientCppService>((_,args)=>
                {
                    SourceCalls++;Requested=(IReadOnlyList<string>)args[1]!;
                    return Task.FromResult<PatientCppSummaryResponse?>((Guid)args[0]! != Patient.PatientUid?null:
                        Failure=="wrong-summary"?Data with {PatientUid=Guid.NewGuid()}:Failure=="unavailable"?Data with {Problems=PatientCppSection<PatientCppProblem>.Unavailable()}:Data);
                }),Stub<IPatientRepository>((_,_)=>Task.FromResult<PatientDetailsResponse?>(Failure=="wrong-patient"?new() {PatientUid=Guid.NewGuid()}:Patient)),
                Stub<IProviderAdministrationRepository>((_,_)=>Task.FromResult<ProviderAdministrationItem?>(new(
                    Failure=="wrong-provider"?Guid.NewGuid():ClinicianUid,"Clinician","Name","Dr Clinician","Physician",null,"Family Medicine",null,null,null,
                    Failure!="inactive-provider",DateTime.UtcNow,null,null,null,null,null,null,"version"))),
                Stub<IClinicConfigurationService>((_,_)=>Task.FromResult(new ClinicConfigurationResponse(Failure=="missing-clinic"?"":"Clinic","America/Toronto",Failure=="missing-clinic"?null:"Clinic Legal Name",
                    "416-111-1111",null,null,"10 Clinic Street",null,"Toronto","ON","M1M1M1","CA",null,null,null,null))),
                Stub<IPatientClinicalHistoryService>((method,args)=>
                {
                    Assert.Equal("ListAsync",method);Assert.Equal("Active",args[1]);
                    return Task.FromResult<IReadOnlyList<PatientClinicalHistoryResponse>>([new() {PatientUid=Failure=="wrong-history-patient"?Guid.NewGuid():Patient.PatientUid,
                        HistoryType="Medical",Status="Active",Description=string.Join("\n",Enumerable.Range(0,600).Select(i=>$"Long history line {i}"))+" <script>",RowVersion="version"}]);
                }),Stub<ICurrentUserPermissionService>((_,_)=>Task.FromResult<IReadOnlySet<string>>(Failure=="permission"?new HashSet<string>():new HashSet<string> {PermissionKeys.PatientsView})),
                Stub<IAuthenticatedClinicalUserAccessor>((_,_)=>Failure=="actor"?throw new ClinicalUserResolutionException("Unavailable actor"):Task.FromResult(7L)),
                new ClinicalPrintLayoutRenderer(),Stub<IPdfRenderer>((_,args)=>{RenderCalls++;return Task.FromResult(Encoding.UTF8.GetBytes((string)args[0]!));}),
                Stub<IPdfPageNumberer>((_,args)=>{NumberCalls++;return Task.FromResult((byte[])args[0]!);}),new Clock());
        }
        public Task<byte[]?> Print(params string[] categories)=>Service.PrintAsync(Patient.PatientUid,new() {ClinicianUid=ClinicianUid,Categories=categories.ToList()},"trace");
    }
    private sealed class Clock:TimeProvider {public override DateTimeOffset GetUtcNow()=>new(2026,10,8,17,0,0,TimeSpan.Zero);}
}
