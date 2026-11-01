using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.ClinicConfiguration;
using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.PatientDocuments.Repositories;
using MicroEMR.Application.PatientEncounters;
using MicroEMR.Application.PatientEncounters.Contracts;
using MicroEMR.Application.PatientEncounters.Repositories;
using MicroEMR.Application.PatientFiles;
using MicroEMR.Application.Patients.Contracts;
using MicroEMR.Application.Patients.Services;
using MicroEMR.Application.Templates.Output;
using MicroEMR.Application.Templates.Runtime;
using MicroEMR.Application.Templates.Serialization;
using MicroEMR.Application.Templates.Validation;
using MicroEMR.Application.Templates.Variables;
using MicroEMR.Infrastructure.ClinicalOutput;
using UglyToad.PdfPig;
using Xunit;
using static MicroEMR.Api.Tests.ConsultationSigningTests;

namespace MicroEMR.Api.Tests;

public sealed class BrandedChartPdfTests
{
    private static readonly IReadOnlySet<string> EncounterAccess = new HashSet<string> {PermissionKeys.EncountersView, PermissionKeys.EncountersEdit};
    private static ClinicConfigurationResponse Clinic(bool missing = false, bool longName = false) => new("Synthetic Clinic", "UTC",
        longName ? "Synthetic Regional Teaching and Community Primary Care Clinic with Extended Multidisciplinary Services" : "Synthetic Clinic Letterhead",
        missing ? null : "555-0100", missing ? null : "555-0101", missing ? null : "clinic@example.invalid",
        missing ? null : "100 Synthetic Street", null, missing ? null : "Test City", missing ? null : "ON",
        missing ? null : "T0T 0T0", missing ? null : "Canada", null, null, null, null);

    private static readonly Lazy<Task<byte[]>> Logo = new(async () =>
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() {Headless = true});
        var page = await browser.NewPageAsync();
        return Convert.FromBase64String(await page.EvaluateAsync<string>("()=>{const c=document.createElement('canvas');c.width=240;c.height=80;const x=c.getContext('2d');x.fillStyle='#176b7c';x.fillRect(0,0,240,80);x.fillStyle='white';x.font='bold 20px Arial';x.fillText('SYNTHETIC CLINIC',16,46);return c.toDataURL('image/png').split(',')[1]}"));
    });
    private static async Task Seed(ClinicBrandingTests.Fixture brand, bool logo)
    { if (logo) await brand.Repository.SaveAsync(new(await Logo.Value,240,80),null,7); }

    // Native samples exercise the actual HTML from the three approved application services.
    [Theory]
    [InlineData("cpp", "logo")]
    [InlineData("cpp", "no-logo")]
    [InlineData("cpp", "long")]
    [InlineData("immunization", "logo")]
    [InlineData("immunization", "no-logo")]
    [InlineData("immunization", "long")]
    [InlineData("encounter", "logo")]
    [InlineData("encounter", "no-logo")]
    [InlineData("encounter", "long")]
    public async Task NativeSamplesPreserveIdentityContentAttributionAndPageBounds(string kind, string variant)
    {
        var longContent = variant == "long"; var hasLogo = variant != "no-logo";
        var configuration = Clinic(!hasLogo,longContent);
        var brand = BrandedClinicalPdfTests.Branding(configuration,kind == "encounter" ? EncounterAccess : new HashSet<string> {PermissionKeys.PatientsView});
        await Seed(brand,hasLogo);
        string html; string[] expected;
        if (kind == "cpp")
        {
            var f = new CppSelectivePrintTests.Fixture(brand);
            html = Encoding.UTF8.GetString((await f.Print(longContent ? ["Problems","History"] : ["Problems","Allergies"]))!);
            expected = ["Cumulative Patient Profile","Ada Lee","1234567890 AB","Dr Clinician","Physician","Family Medicine","Problem 11",longContent ? "Long history line 599" : "Unselected allergen"];
            Assert.DoesNotContain("SECRET-CHART",html); Assert.DoesNotContain("DOB:",html);
            Assert.Equal(1,f.NumberCalls);
        }
        else if (kind == "immunization")
        {
            var f = new ImmunizationSummaryTests.Fixture(brand);
            f.History = Enumerable.Range(0,longContent ? 65 : 3).Select(i => f.Item($"Synthetic vaccine {i:D3}",new(2025,1,1),"Dr Synthetic"))
                .Append(new() {PatientUid=f.PatientUid,ImmunizationUid=Guid.NewGuid(),VaccineName="Synthetic refused vaccine",Status="Refused",ClinicalFields=new(){Refused=true,RefusalDate=new(){Year=2001}},RowVersion="test"})
                .Append(new() {PatientUid=f.PatientUid,ImmunizationUid=Guid.NewGuid(),VaccineName="Synthetic corrected vaccine",Status="EnteredInError",Dose=new(){Amount=0.5m,UnitCode="mL"},ClinicalFields=new(){AdministrationDate=new(){Year=2000,Month=2},DIN="00123456",Manufacturer="Synthetic manufacturer"},RowVersion="test"}).ToArray();
            await f.Service.GenerateAsync(f.PatientUid,"trace"); html=f.Pdf.Html!;
            expected = ["Immunization Summary","Immunization History","Jane Example","1234567890 AB","Dr Synthetic",longContent ? "Synthetic vaccine 064" : "Synthetic vaccine 002","Refusal: 2001","Administration: 2000-02","Entered in error","0.5 mL","00123456","Synthetic manufacturer"];
            Assert.Equal(new[]{"audit","history","pdf"},f.Events); Assert.Equal("All",f.RequestedStatus);
            Assert.DoesNotContain("2000-02-01",html);
        }
        else
        {
            var capture = new HtmlCapture(); var f = new Encounter(configuration,brand,capture);
            if (longContent)
            {
                // Keep each saved template value within existing template limits; narrative contributions are separate.
                f.Record.StructuredDataJson = Values(string.Join("\n",Enumerable.Range(0,55).Select(i=>$"Synthetic saved finding line {i:D3}")));
                for (var i=0;i<3;i++) f.Record.Contributions.Add(f.Narrative(i,
                    string.Join("\n",Enumerable.Range(0,55).Select(n=>$"Synthetic contributor {i} line {n:D3}"))));
            }
            await f.Preview(); html=capture.Html!;
            expected = ["Synthetic encounter template","Synthetic Patient","SYNTHETIC-01","1234567890 AB","Dr Synthetic Provider","Synthetic Author",
                "Tracked Author","Revision Editor","Narrative Author",longContent ? "Synthetic contributor 2 line 054" : "Synthetic saved finding"];
            Assert.Equal("Open",f.Record.Status); Assert.Null(f.Record.SignedAt);
            Assert.Equal(new[]{"encounter","version","patient","clinic"},f.Reads);
        }
        Assert.Single(Regex.Matches(html,"class=\"clinic-letterhead\""));
        Assert.DoesNotContain("<div class=\"clinic-identity\">",html);
        Assert.Single(Regex.Matches(html,"<h1"));
        if (hasLogo)
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new() {Headless=true});
            var page = await browser.NewPageAsync();
            await page.SetViewportSizeAsync(816,1056);
            await page.SetContentAsync(html);
            await page.EmulateMediaAsync(new(){Media=Media.Print});
            var ratio = await page.EvaluateAsync<double>("()=>{const r=document.querySelector('.clinic-letterhead-logo').getBoundingClientRect();return r.width/r.height}");
            Assert.Equal(3,ratio,2);
        }
        await using var renderer = new PlaywrightPdfRenderer(NullLogger<PlaywrightPdfRenderer>.Instance);
        var bytes = await renderer.RenderAsync(html);
        if (kind == "cpp") bytes = await new PdfPageNumberer(NullLogger<PdfPageNumberer>.Instance).NumberAsync(bytes);
        using var pdf = PdfDocument.Open(bytes);
        Assert.Equal(hasLogo ? 1 : 0,pdf.GetPages().Sum(p=>p.NumberOfImages));
        if (hasLogo)
        {
            var image = Assert.Single(pdf.GetPage(1).GetImages());
            // Chromium quantizes the PDF image transform; permit one CSS pixel in height.
            Assert.InRange(Math.Abs(image.Bounds.Height-image.Bounds.Width/3),0,0.75);
            Assert.InRange(image.Bounds.Width,1,111); Assert.InRange(image.Bounds.Height,1,66);
        }
        if (longContent) Assert.True(pdf.NumberOfPages > 1);
        var text = string.Join(" ",pdf.GetPages().SelectMany(p=>p.GetWords()).Select(w=>w.Text));
        foreach (var value in expected) Assert.Contains(value,text);
        Assert.Contains(configuration.LegalName!,text);
        if (hasLogo) foreach (var value in new[]{"100 Synthetic Street","555-0100","555-0101","clinic@example.invalid","Canada"}) Assert.Contains(value,text);
        foreach (var page in pdf.GetPages())
        {
            Assert.InRange(page.Width,611,613); Assert.InRange(page.Height,791,793);
            foreach (var word in page.GetWords())
            {
                Assert.InRange(word.BoundingBox.Left,30,page.Width-30);
                Assert.InRange(word.BoundingBox.Right,30,page.Width-30);
                Assert.InRange(word.BoundingBox.Bottom,15,page.Height-30);
                Assert.InRange(word.BoundingBox.Top,15,page.Height-30);
            }
            if (kind=="cpp") Assert.Contains($"{page.Number}/{pdf.NumberOfPages}",page.Text);
        }
        var directory=Path.Combine(Root(),".tmp/product-improvement-009/step04"); Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory,$"{kind}-{variant}.pdf"),bytes);
        File.WriteAllText(Path.Combine(directory,$"{kind}-{variant}.html"),html);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Encounters.View")]
    [InlineData("Encounters.Edit")]
    [InlineData("Patients.View,ClinicSettings.Manage")]
    public async Task EncounterBrandingDeniesIncompleteOperationBeforeAnyHeaderRead(string keys)
    {
        var brand=Brand(keys); var f=new Encounter(Clinic(),brand,new HtmlCapture());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Preview());
        Assert.Equal(0,brand.ConfigurationReads); Assert.Equal(0,brand.Repository.Reads);
    }

    [Fact]
    public async Task EncounterOnlyReadCannotAccessGeneralBrandingAdministrationOrOtherOperations()
    {
        var brand=Brand("Encounters.View,Encounters.Edit"); var f=new Encounter(Clinic(),brand,new HtmlCapture());
        Assert.NotNull(await f.Preview());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>brand.Service.GetPrintHeaderAsync("General"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>brand.Service.GetStateAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>brand.Service.SaveLogoAsync([1],false,null));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>brand.Service.GetPrintHeaderForDocumentAsync(ClinicBrandingDocumentOperation.ConsultationPreview,"Other"));
        Assert.Equal(0,brand.Repository.Writes);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("subject")]
    public async Task MissingTrustedContextDeniesEncounterHeaderBeforeStorage(string missing)
    {
        var brand=missing=="subject" ? new ClinicBrandingTests.Fixture(effectivePermissions:EncounterAccess,
            subject:new AuthenticatedSubjectAccessor(new HttpContextAccessor())) : Brand("Encounters.View,Encounters.Edit");
        if(missing=="tenant") brand.Tenant.TenantUid=Guid.Empty;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>brand.Service.GetPrintHeaderForDocumentAsync(ClinicBrandingDocumentOperation.EncounterPreview,"Preview"));
        Assert.Equal(0,brand.ConfigurationReads);Assert.Equal(0,brand.Repository.Reads);
    }

    [Theory]
    [InlineData("Signed")]
    [InlineData("Completed")]
    [InlineData("Draft")]
    [InlineData("schema")]
    [InlineData("unsaved")]
    [InlineData("version")]
    [InlineData("patient")]
    public async Task IneligibleOrUnsavedEncounterFailsBeforeBranding(string failure)
    {
        var brand=Brand("Encounters.View,Encounters.Edit");var f=new Encounter(Clinic(),brand,new HtmlCapture());
        if(failure=="schema") f.Record.TemplateUid=null;
        else if(failure=="version") f.Version.TemplateUid=Guid.NewGuid();
        else if(failure=="patient") f.PatientAvailable=false;
        else if(failure!="unsaved") f.Record.Status=failure;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.PreviewEncounterAsync(f.Record.EncounterUid,
            new(failure=="unsaved" ? Values("unsaved change") : f.Record.StructuredDataJson)));
        Assert.Equal(0,brand.ConfigurationReads);Assert.Equal(0,brand.Repository.Reads);
    }

    [Theory]
    [InlineData("wrong-patient")]
    [InlineData("cross-tenant")]
    public async Task ScopedEncounterReadCannotDiscloseAnotherPatientOrTenant(string scenario)
    {
        var brand=Brand("Encounters.View,Encounters.Edit");var f=new Encounter(Clinic(),brand,new HtmlCapture());
        // No patient/tenant identifier can be supplied in TemplatePreviewRequest. An unowned UID is absent in the tenant-scoped repository.
        Assert.Null(await f.Service.PreviewEncounterAsync(Guid.NewGuid(),new(f.Record.StructuredDataJson)));
        Assert.Equal(0,brand.ConfigurationReads);Assert.Equal(0,brand.Repository.Reads);
        Assert.Equal(new[]{"encounter"},f.Reads);Assert.NotEmpty(scenario);
    }

    [Fact]
    public async Task TrustedTenantSwitchCannotReuseAnotherTenantsHeaderOrLogo()
    {
        var brand=Brand("Encounters.View,Encounters.Edit"); var tenantA=brand.Tenant.TenantUid;
        brand.Configuration=brand.Configuration with {LegalName=null};
        await brand.Repository.SaveAsync(new([1],1,1),null,7);
        var a=await brand.Service.GetPrintHeaderForDocumentAsync(ClinicBrandingDocumentOperation.EncounterPreview,"Preview");
        brand.Tenant.TenantUid=Guid.NewGuid();brand.Tenant.DisplayName="Tenant B";
        var b=await brand.Service.GetPrintHeaderForDocumentAsync(ClinicBrandingDocumentOperation.EncounterPreview,"Preview");
        Assert.Null(b.Logo);Assert.Equal("Tenant B",b.Clinic.Name);Assert.NotEqual(a.Clinic.Name,b.Clinic.Name);
        brand.Tenant.TenantUid=tenantA;
        Assert.Equal(new byte[]{1},(await brand.Service.GetPrintHeaderForDocumentAsync(ClinicBrandingDocumentOperation.EncounterPreview,"Preview")).Logo!.PngBytes);
    }

    [Theory]
    [InlineData("",false)]
    [InlineData("Encounters.View",false)]
    [InlineData("Encounters.Edit",false)]
    [InlineData("Encounters.View,Encounters.Edit",true)]
    public async Task ExistingPreviewEndpointStillRequiresBothEncounterPermissions(string keys,bool allowed)
    {
        var type=typeof(MicroEMR.Api.Controllers.PatientEncountersController);
        var metadata=type.GetCustomAttributes<AuthorizeAttribute>().Concat(type.GetMethod("PreviewPdf")!.GetCustomAttributes<AuthorizeAttribute>());
        var policy=await AuthorizationPolicy.CombineAsync(new PermissionPolicyProvider(Options.Create(new AuthorizationOptions())),metadata);
        var access=keys.Split(',',StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var permissions=Proxy<ICurrentUserPermissionService>((method,args)=>method.Name=="HasPermissionAsync" ? Task.FromResult(access.Contains((string)args![0]!)) : Task.FromResult<IReadOnlySet<string>>(access));
        var context=new AuthorizationHandlerContext(policy!.Requirements,new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub","synthetic")],"test")),null);
        await new PermissionAuthorizationHandler(permissions,NullLogger<PermissionAuthorizationHandler>.Instance).HandleAsync(context);
        await new Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement().HandleAsync(context);
        Assert.Equal(allowed,context.HasSucceeded);
        var brandingActions=typeof(MicroEMR.Api.Controllers.ClinicBrandingController).GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly);
        Assert.DoesNotContain(brandingActions,m=>m.GetParameters().Any(p=>p.ParameterType==typeof(ClinicBrandingDocumentOperation)));
        Assert.DoesNotContain(typeof(TemplatePreviewRequest).GetProperties(),p=>p.Name.Contains("Tenant")||p.Name.Contains("Patient"));
    }

    [Fact]
    public async Task SignedRenderingKeepsOriginalLayoutAndNeverReadsBranding()
    {
        var brand=Brand("");brand.ConfigurationFailure=new InvalidOperationException("Signed generation must not read branding");
        var capture=new HtmlCapture();var f=new Encounter(Clinic(),brand,capture);
        f.Record.Status="Signed";f.Record.SignedByDisplayName="Synthetic Signer";f.Record.SignedAt=new(2026,10,2,10,0,0,DateTimeKind.Utc);
        await f.Service.RenderSignedEncounterAsync(f.Record.EncounterUid);
        Assert.Contains("<div class=\"clinic-identity\">",capture.Html!);Assert.DoesNotContain("class=\"clinic-letterhead\"",capture.Html!);
        foreach(var value in new[]{"Synthetic Signer","Synthetic saved finding","Tracked Author","Revision Editor","Narrative Author"})Assert.Contains(value,capture.Html!);
        Assert.Equal(0,brand.ConfigurationReads);Assert.Equal(0,brand.Repository.Reads);
    }

    [Fact]
    public async Task PreservedSignedDownloadReturnsIdenticalBytesAfterBrandingChangeWithoutRenderRepairOrWrites()
    {
        var brand=Brand("Encounters.View,Encounters.Edit");var capture=new HtmlCapture();var f=new Encounter(Clinic(),brand,capture);
        var original=Encoding.ASCII.GetBytes("%PDF-synthetic-preserved-signed-artifact");var hash=SHA256.HashData(original);
        var artifact=new ClinicalOutputArtifact(Guid.NewGuid(),f.Record.PatientUid,"Encounter",f.Record.EncounterUid,f.Record.TemplateVersionUid!.Value,"FinalPdf","FileSystem","synthetic.pdf","application/pdf",original.Length,Convert.ToHexString(hash),"Ready",7,DateTime.UtcNow);
        var repository=Proxy<IClinicalOutputArtifactRepository>((method,_)=>method.Name=="GetFinalBySourceAsync" ? Task.FromResult<ClinicalOutputArtifact?>(artifact) : throw new InvalidOperationException("Artifact mutation forbidden"));
        var storage=Proxy<IPatientFileStorage>((method,_)=>method.Name switch {
            "ExistsAsync"=>Task.FromResult(true),"OpenReadAsync"=>Task.FromResult<Stream>(new MemoryStream(original,false)),_=>throw new InvalidOperationException("Storage mutation forbidden")});
        var service=new ClinicalArtifactService(repository,f.Repository,Proxy<IClinicalPdfPreviewService>((_,_)=>throw new InvalidOperationException("Render/repair forbidden")),storage,brand.Tenant,NullLogger<ClinicalArtifactService>.Instance);
        async Task<byte[]> Read(){var opened=await service.OpenEncounterFinalPdfAsync(f.Record.EncounterUid);await using var stream=opened!.Content;using var output=new MemoryStream();await stream.CopyToAsync(output);return output.ToArray();}
        Assert.Equal(original,await Read());
        await f.Preview();var previewCalls=capture.Calls;
        brand.Configuration=brand.Configuration with {LegalName="Changed current clinic"};await brand.Repository.SaveAsync(new([9],1,1),null,7);
        f.Record.Status="Signed";
        var beforeReads=brand.Repository.Reads;var returned=await Read();
        Assert.Equal(original,returned);Assert.Equal(hash,SHA256.HashData(returned));
        Assert.Equal(beforeReads,brand.Repository.Reads);Assert.Equal(previewCalls,capture.Calls);
    }

    private static ClinicBrandingTests.Fixture Brand(string keys)=>BrandedClinicalPdfTests.Branding(Clinic(),keys.Split(',',StringSplitOptions.RemoveEmptyEntries).ToHashSet());
    private static string Values(string findings)=>JsonSerializer.Serialize(new{schemaVersion=1,values=new{findings}});
    private static string Root(){var directory=new DirectoryInfo(AppContext.BaseDirectory);while(directory is not null&&!File.Exists(Path.Combine(directory.FullName,"AGENTS.md")))directory=directory.Parent;return directory!.FullName;}
    private sealed class HtmlCapture : IPdfRenderer
    {
        public string? Html;public int Calls;
        public Task<byte[]> RenderAsync(string html,CancellationToken token=default){Html=html;Calls++;return Task.FromResult(Encoding.UTF8.GetBytes(html));}
    }
    private sealed class Encounter
    {
        public readonly PatientEncounterDetailsResponse Record=new(){EncounterUid=Guid.NewGuid(),PatientUid=Guid.NewGuid(),Status="Open",TemplateUid=Guid.NewGuid(),TemplateVersionUid=Guid.NewGuid(),TemplateName="Synthetic encounter template",EncounterType="Office visit",ProviderName="Dr Synthetic Provider",CreatedByDisplayName="Synthetic Author",CreatedAt=new(2026,10,1,9,0,0,DateTimeKind.Utc),EncounterDateUtc=new(2026,10,1,9,0,0,DateTimeKind.Utc),StructuredDataJson=Values("Synthetic saved finding")};
        public DocumentTemplateVersionResponse Version;
        public IPatientEncounterRepository Repository;
        public ClinicalPdfPreviewService Service;
        public List<string> Reads=[];
        public bool PatientAvailable=true;
        public Encounter(ClinicConfigurationResponse clinic,ClinicBrandingTests.Fixture brand,IPdfRenderer pdf)
        {
            const string definition="""
                {"schemaVersion":1,"sections":[{"id":"clinical","key":"clinical","title":"Clinical findings","order":0,"fields":[{"id":"findings","key":"findings","type":"TextArea","label":"Findings","order":0,"required":false}]}]}
                """;
            Version=new(){TemplateUid=Record.TemplateUid.Value,TemplateVersionUid=Record.TemplateVersionUid.Value,DefinitionJson=definition};
            Record.Contributions.Add(new(){ContributionUid=Guid.NewGuid(),EncounterUid=Record.EncounterUid,PatientUid=Record.PatientUid,SectionKey="clinical",FieldKey="findings",FieldLabel="Findings",SectionTitle="Clinical findings",ValueKind="Value",OriginalContributorId=7,OriginalContributorDisplayName="Tracked Author",CreatedAtUtc=Record.CreatedAt,Revisions=[new(1,"initial",7,"Tracked Author",Record.CreatedAt),new(2,"Synthetic saved finding",8,"Revision Editor",Record.CreatedAt.AddMinutes(5))]});
            Record.Contributions.Add(Narrative(0,"Synthetic narrative contribution"));
            Repository=Proxy<IPatientEncounterRepository>((method,args)=>{
                if(method.Name!="GetByUidAsync")throw new InvalidOperationException("Encounter mutation/repair forbidden");
                Reads.Add("encounter");return Task.FromResult<PatientEncounterDetailsResponse?>((Guid)args![0]! == Record.EncounterUid ? Record : null);});
            var serializer=new TemplateDefinitionSerializer(new TemplateDefinitionValidator());
            Service=new(null!,Repository,Proxy<IDocumentTemplateVersionRepository>((method,_)=>{
                if(method.Name!="GetByUidAsync")throw new InvalidOperationException("Template mutation forbidden");Reads.Add("version");return Task.FromResult<DocumentTemplateVersionResponse?>(Version);}),
                Proxy<IPatientService>((method,args)=>{
                    if(method.Name!="GetByUidAsync" || (Guid)args![0]! != Record.PatientUid)throw new InvalidOperationException("Patient context mismatch");
                    Reads.Add("patient");return Task.FromResult<PatientDetailsResponse?>(PatientAvailable ? new(){PatientUid=Record.PatientUid,FirstName="Synthetic",LastName="Patient",DateOfBirth=new(1980,1,1),HealthCardNumber="1234567890",HealthCardVersion="AB",ChartNumber="SYNTHETIC-01"} : null);}),
                Proxy<IClinicConfigurationService>((_,_)=>{Reads.Add("clinic");return Task.FromResult(clinic);}),serializer,new TemplateInstanceRuntime(serializer),new TemplateOutputBuilder(new TemplateVariableResolver()),new TemplateHtmlRenderer(),new ClinicalPrintLayoutRenderer(),pdf,TimeProvider.System,NullLogger<ClinicalPdfPreviewService>.Instance,brand.Service);
        }
        public EncounterContributionResponse Narrative(int index,string content)=>new(){ContributionUid=Guid.NewGuid(),EncounterUid=Record.EncounterUid,PatientUid=Record.PatientUid,SectionKey="clinical",SectionTitle="Clinical findings",ValueKind="Narrative",OriginalContributorId=20+index,OriginalContributorDisplayName=$"Narrative Author {index}",CreatedAtUtc=Record.CreatedAt.AddMinutes(index+10),Revisions=[new(1,content,20+index,$"Narrative Author {index}",Record.CreatedAt.AddMinutes(index+10))]};
        public Task<byte[]?> Preview()=>Service.PreviewEncounterAsync(Record.EncounterUid,new(Record.StructuredDataJson));
    }
}
