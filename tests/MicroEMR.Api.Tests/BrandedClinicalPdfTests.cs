using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.ClinicConfiguration;
using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.PatientDocuments.Repositories;
using MicroEMR.Application.PatientDocuments.Services;
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

public sealed class BrandedClinicalPdfTests
{
    internal static ClinicBrandingTests.Fixture Branding(ClinicConfigurationResponse configuration,IReadOnlySet<string> permissions)
    {
        var f=new ClinicBrandingTests.Fixture(effectivePermissions:permissions){Configuration=configuration};
        f.Tenant.DisplayName=configuration.ClinicName;return f;
    }
    private static ClinicConfigurationResponse Clinic(bool missing=false)=>new("Synthetic Clinic","UTC","Synthetic Clinic Letterhead",
        missing?null:"555-0100",missing?null:"555-0101",missing?null:"clinic@example.invalid",missing?null:"100 Synthetic Street",null,
        missing?null:"Test City",missing?null:"ON",missing?null:"T0T 0T0",missing?null:"Canada",null,null,null,null);
    private static readonly IReadOnlySet<string> ReferralAccess=new HashSet<string>{PermissionKeys.PatientsView,PermissionKeys.ReferralsView,PermissionKeys.ReferralsManage};
    private static readonly IReadOnlySet<string> PreviewAccess=new HashSet<string>{PermissionKeys.DocumentsView,PermissionKeys.DocumentsManage};
    private static readonly IReadOnlySet<string> SignAccess=new HashSet<string>{PermissionKeys.DocumentsView,PermissionKeys.DocumentsSign};
    private static readonly Lazy<Task<byte[]>> Logo=new(async()=>{
        using var playwright=await Playwright.CreateAsync();await using var browser=await playwright.Chromium.LaunchAsync(new(){Headless=true});
        var page=await browser.NewPageAsync();
        var encoded=await page.EvaluateAsync<string>("()=>{const c=document.createElement('canvas');c.width=240;c.height=80;const x=c.getContext('2d');x.fillStyle='#176b7c';x.fillRect(0,0,240,80);x.fillStyle='white';x.font='bold 20px Arial';x.fillText('SYNTHETIC CLINIC',16,46);return c.toDataURL('image/png').split(',')[1]}");
        return Convert.FromBase64String(encoded);
    });
    private static async Task SeedLogo(ClinicBrandingTests.Fixture f,bool logo)
    {if(logo)await f.Repository.SaveAsync(new(await Logo.Value,240,80),null,7);}

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReferralPdfPreservesClinicalContentAndFrozenBytesAfterBrandingChanges(bool logo)
    {
        var configuration=Clinic(missing:!logo);var brand=Branding(configuration,ReferralAccess);await SeedLogo(brand,logo);
        await using var pdf=new Capture();var f=new ReferralFinalizationTests.Fixture(renderer:pdf,brandingFixture:brand,configuration:configuration);
        await f.Send();var original=f.Artifact!.PdfContent.ToArray();
        using(var document=PdfDocument.Open(original))
        {
            AssertLogo(document,logo);var text=Text(document);
            foreach(var expected in new[]{"Referral Letter","Synthetic Clinic Letterhead","Ada Lee","Dr Referrer","Dr Recipient","Reason","Clinical summary","Complete report body"})Assert.Contains(expected,text);
            if(logo)foreach(var field in new[]{"100 Synthetic Street","555-0100","555-0101","clinic@example.invalid"})Assert.Contains(field,text);
        }
        Assert.Single(Regex.Matches(pdf.Html!,"class=\"clinic-letterhead\""));Assert.DoesNotContain("<div class=\"clinic-identity\">",pdf.Html!);
        Assert.Single(Regex.Matches(pdf.Html!,"<h1"));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(original)).ToLowerInvariant(),f.Artifact.Sha256);
        Save(logo?"referral-branded":"referral-text-only",original,pdf.Html!);
        brand.Configuration=configuration with{LegalName="Changed branding after send"};
        var reads=brand.Repository.Reads;
        var stored=await f.Service.OpenArtifactAsync(f.Patient.PatientUid,f.Source.ReferralUid);
        using var returned=new MemoryStream();await stored!.Content.CopyToAsync(returned);
        Assert.Equal(original,returned.ToArray());Assert.Equal(1,pdf.Calls);Assert.Equal(reads,brand.Repository.Reads);
        await Assert.ThrowsAsync<MicroEMR.Application.PatientReferrals.PatientReferralTransitionException>(()=>f.Service.PreviewLetterAsync(f.Patient.PatientUid,f.Source.ReferralUid));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConsultationPdfPreservesClinicalContentSnapshotsAndStoredSignedBytes(bool logo)
    {
        var configuration=Clinic(missing:!logo);var brand=Branding(configuration,SignAccess);await SeedLogo(brand,logo);
        await using var pdf=new Capture();var f=new Consultation(configuration,brand,pdf);
        var original=await f.Service.RenderConsultationFinalAsync(f.Snapshot);
        using(var document=PdfDocument.Open(original))
        {
            AssertLogo(document,logo);var text=Text(document);
            foreach(var expected in new[]{"Consultation Report","Synthetic Clinic Letterhead","Synthetic Patient","Synthetic examination findings","Synthetic assessment","Synthetic recommendations","Frozen Recipient","Frozen Office","Dr Frozen Signer","Electronically signed by"})Assert.Contains(expected,text);
        }
        Assert.Single(Regex.Matches(pdf.Html!,"class=\"clinic-letterhead\""));Assert.Single(Regex.Matches(pdf.Html!,"<h1"));
        Assert.Equal("Dr Frozen Signer",f.Snapshot.SignerDisplayName);Assert.Equal(new DateTime(2026,10,3,12,0,0,DateTimeKind.Utc),f.Snapshot.SignedAtUtc);
        Save(logo?"consultation-branded":"consultation-text-only",original,pdf.Html!);
        f.Document.Status="Signed";f.Document.FinalizedAt=f.Snapshot.SignedAtUtc;
        var key=$"tenants/{brand.Tenant.TenantUid:N}/clinical-artifacts/patient-documents/{f.Document.PatientUid:N}/{f.Document.DocumentUid:N}/preserved.pdf";
        var artifact=new ClinicalOutputArtifact(Guid.NewGuid(),f.Document.PatientUid,"PatientDocument",f.Document.DocumentUid,f.Document.TemplateVersionUid!.Value,"FinalPdf","FileSystem",key,"application/pdf",original.Length,Convert.ToHexString(SHA256.HashData(original)),"Available",7,f.Snapshot.SignedAtUtc);
        var storage=Proxy<IPatientFileStorage>((method,_)=>method.Name=="OpenReadAsync"?Task.FromResult<Stream>(new MemoryStream(original)):throw new InvalidOperationException("Historical storage must not mutate"));
        var signing=new ConsultationSigningService(null!,f.Documents,f.Service,Proxy<IClinicalOutputArtifactRepository>((_,_)=>Task.FromResult<ClinicalOutputArtifact?>(artifact)),storage,brand.Tenant,NullLogger<ConsultationSigningService>.Instance);
        brand.Configuration=configuration with{LegalName="Changed branding after signing"};var reads=brand.Repository.Reads;
        var stored=await signing.OpenFinalPdfAsync(f.Document.PatientUid,f.Document.DocumentUid);using var returned=new MemoryStream();await stored!.Content.CopyToAsync(returned);
        Assert.Equal(original,returned.ToArray());Assert.Equal(1,pdf.Calls);Assert.Equal(reads,brand.Repository.Reads);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.PreviewPatientDocumentAsync(f.Document.DocumentUid,new(f.Document.StructuredDataJson)));
    }

    [Fact]
    public async Task DraftConsultationPreviewUsesDocumentOnlyPermissionsAndNoPersistence()
    {
        var configuration=Clinic();var brand=Branding(configuration,PreviewAccess);await SeedLogo(brand,true);
        await using var pdf=new Capture();var f=new Consultation(configuration,brand,pdf);
        var bytes=await f.Service.PreviewPatientDocumentAsync(f.Document.DocumentUid,new(f.Document.StructuredDataJson));
        using var document=PdfDocument.Open(bytes!);AssertLogo(document,true);Assert.Contains("Synthetic assessment",Text(document));
        Assert.Equal("Draft",f.Document.Status);Assert.Null(f.Document.FinalizedAt);
        Assert.DoesNotContain("Electronically signed by",Text(document));Assert.Equal(0,brand.Repository.Writes-1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LongClinicAndMultipageClinicalContentRemainCompleteWithinPageMargins(bool referral)
    {
        var configuration=Clinic() with{LegalName="LONG CLINIC "+new string('X',210),AddressLine1=string.Join(" ",Enumerable.Repeat("Synthetic long address",9))};
        var brand=Branding(configuration,referral?ReferralAccess:SignAccess);await SeedLogo(brand,true);
        await using var pdf=new Capture();byte[] bytes;
        var content=string.Join("\n",Enumerable.Range(1,150).Select(i=>$"Synthetic line {i:D3}: Complete clinical narrative retained for review."));
        if(referral){var f=new ReferralFinalizationTests.Fixture(renderer:pdf,brandingFixture:brand,configuration:configuration,clinicalSummary:content);await f.Send();bytes=f.Artifact!.PdfContent;}
        else {var f=new Consultation(configuration,brand,pdf,content);bytes=await f.Service.RenderConsultationFinalAsync(f.Snapshot);}
        using var document=PdfDocument.Open(bytes);Assert.True(document.NumberOfPages>1);var text=Text(document);
        for(var i=1;i<=150;i++)Assert.Contains($"Synthetic line {i:D3}:",text);
        foreach(var page in document.GetPages())foreach(var letter in page.Letters.Where(l=>!string.IsNullOrWhiteSpace(l.Value)))
        {Assert.InRange(letter.GlyphRectangle.Left,44,568);Assert.InRange(letter.GlyphRectangle.Right,44,568);Assert.InRange(letter.GlyphRectangle.Bottom,40,752);}
        if(!referral)Assert.Contains("Electronically signed by",document.GetPage(document.NumberOfPages).Text);
        Save(referral?"referral-multipage":"consultation-multipage",bytes,pdf.Html!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnauthorizedConsultationOperationStopsBeforeBrandingOrPdf(bool signing)
    {
        var configuration=Clinic();var brand=Branding(configuration,PreviewAccess);
        if(!signing)brand=Branding(configuration,new HashSet<string>{PermissionKeys.DocumentsView});
        await using var pdf=new Capture();var f=new Consultation(configuration,brand,pdf);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async()=>{if(signing)await f.Service.RenderConsultationFinalAsync(f.Snapshot);else await f.Service.PreviewPatientDocumentAsync(f.Document.DocumentUid,new(f.Document.StructuredDataJson));});
        Assert.Equal(0,brand.Repository.Reads);Assert.Equal(0,brand.ConfigurationReads);Assert.Equal(0,pdf.Calls);
    }

    [Fact]
    public async Task WrongDocumentAndGenericDocumentDoNotInvokeConsultationBranding()
    {
        var configuration=Clinic();var brand=Branding(configuration,PreviewAccess);
        await using var pdf=new Capture();var f=new Consultation(configuration,brand,pdf){TemplateType="GENERAL_NOTE"};
        Assert.Null(await f.Service.PreviewPatientDocumentAsync(Guid.NewGuid(),new(f.Document.StructuredDataJson)));
        Assert.Equal(0,brand.Repository.Reads);
        var bytes=await f.Service.PreviewPatientDocumentAsync(f.Document.DocumentUid,new(f.Document.StructuredDataJson));
        Assert.NotNull(bytes);Assert.DoesNotContain("clinic-letterhead",pdf.Html!);Assert.Equal(0,brand.Repository.Reads);
    }

    [Fact]
    public async Task BrandingFailureDoesNotBecomeNoLogoFallbackOrCommitReferral()
    {
        var configuration=Clinic();var brand=Branding(configuration,ReferralAccess);brand.ConfigurationFailure=new IOException("Synthetic branding failure");
        await using var pdf=new Capture();var f=new ReferralFinalizationTests.Fixture(renderer:pdf,brandingFixture:brand,configuration:configuration);
        await Assert.ThrowsAsync<IOException>(()=>f.Send());Assert.Null(f.Artifact);Assert.Equal(0,pdf.Calls);Assert.Equal(0,f.StoreCalls);
    }

    [Theory]
    [InlineData(PermissionKeys.PatientsView)]
    [InlineData(PermissionKeys.ReferralsView)]
    [InlineData(PermissionKeys.ReferralsManage)]
    public async Task ExistingReferralAuthorizationDeniesBeforeBranding(string missing)
    {
        var configuration=Clinic();var brand=Branding(configuration,ReferralAccess);
        await using var pdf=new Capture();var f=new ReferralFinalizationTests.Fixture(renderer:pdf,brandingFixture:brand,configuration:configuration);
        f.Source.Permissions.Remove(missing);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Send());
        Assert.Equal(0,brand.Repository.Reads);Assert.Equal(0,pdf.Calls);Assert.Null(f.Artifact);
    }

    internal sealed class Consultation
    {
        public PatientDocumentDetailsResponse Document {get;}
        public ConsultationSigningContext Snapshot {get;}
        public IPatientDocumentRepository Documents {get;}
        public ClinicalPdfPreviewService Service {get;}
        public string TemplateType="CONSULTATION_REPORT";
        public Consultation(ClinicConfigurationResponse configuration,ClinicBrandingTests.Fixture brand,IPdfRenderer pdf,string? findings=null)
        {
            var source=File.ReadAllText(Path.Combine(Root(),"db/tenant-clinical/migrations/0063-consultation-report-template.sql"));
            var json=Regex.Match(source,@"DECLARE @DefinitionJson NVARCHAR\(MAX\) = N'(?<json>.*?)';",RegexOptions.Singleline).Groups["json"].Value;
            var serializer=new TemplateDefinitionSerializer(new TemplateDefinitionValidator());var runtime=new TemplateInstanceRuntime(serializer);
            Document=new(){DocumentUid=Guid.NewGuid(),PatientUid=Guid.NewGuid(),TemplateUid=Guid.NewGuid(),TemplateVersionUid=Guid.NewGuid(),Title="Synthetic consultation",DocumentType="Consultation Report",Status="Draft",CreatedAt=new(2026,10,1,9,0,0,DateTimeKind.Utc),CreatedByDisplayName="Synthetic Author",
                StructuredDataJson=JsonSerializer.Serialize(new{schemaVersion=1,values=new{findingsText=findings??"Synthetic examination findings",impressionText="Synthetic assessment",recommendationsText="Synthetic recommendations"}})};
            Documents=Proxy<IPatientDocumentRepository>((method,args)=>method.Name switch{
                "GetByUidAsync"=>Task.FromResult<PatientDocumentDetailsResponse?>((Guid)args![0]! == Document.DocumentUid?Document:null),
                "GetTemplateByUidAsync"=>Task.FromResult<DocumentTemplateDetailsResponse?>(new(){TemplateUid=Document.TemplateUid.Value,DocumentType=TemplateType}),
                _=>throw new InvalidOperationException("Unexpected document mutation")});
            Service=new(Documents,null!,Proxy<IDocumentTemplateVersionRepository>((_,_)=>Task.FromResult<DocumentTemplateVersionResponse?>(new(){TemplateUid=Document.TemplateUid.Value,TemplateVersionUid=Document.TemplateVersionUid.Value,DefinitionJson=json})),
                Proxy<IPatientService>((_,_)=>Task.FromResult<PatientDetailsResponse?>(new(){PatientUid=Document.PatientUid,FirstName="Synthetic",LastName="Patient",DateOfBirth=new(1980,6,15),ChartNumber="SYNTHETIC-01"})),
                Proxy<IClinicConfigurationService>((_,_)=>Task.FromResult(configuration)),serializer,runtime,new TemplateOutputBuilder(new TemplateVariableResolver()),new TemplateHtmlRenderer(),new ClinicalPrintLayoutRenderer(),pdf,TimeProvider.System,NullLogger<ClinicalPdfPreviewService>.Instance,brand.Service);
            Snapshot=new(Document,[new(Guid.NewGuid(),1,"TO",Guid.NewGuid(),"Frozen Recipient","Frozen Office","555-0199")],Guid.NewGuid(),"Dr Frozen Signer",new(2026,10,3,12,0,0,DateTimeKind.Utc));
        }
    }
    internal sealed class Capture : IPdfRenderer,IAsyncDisposable
    {
        private readonly PlaywrightPdfRenderer engine=new(NullLogger<PlaywrightPdfRenderer>.Instance);
        public string? Html;public int Calls;
        public Task<byte[]> RenderAsync(string html,CancellationToken token=default){Html=html;Calls++;return engine.RenderAsync(html,token);}
        public ValueTask DisposeAsync()=>engine.DisposeAsync();
    }
    private static string Root(){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d is not null&&!File.Exists(Path.Combine(d.FullName,"AGENTS.md")))d=d.Parent;return d!.FullName;}
    private static void Save(string name,byte[] bytes,string html){var dir=Path.Combine(Root(),".tmp/product-improvement-009/step02");Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,name+".pdf"),bytes);File.WriteAllText(Path.Combine(dir,name+".html"),html);}
    private static string Text(PdfDocument document)=>string.Join("\n",document.GetPages().Select(p=>p.Text));
    private static void AssertLogo(PdfDocument document,bool expected)
    {
        Assert.Equal(expected?1:0,document.GetPages().Sum(p=>p.NumberOfImages));
        if(expected){var image=Assert.Single(document.GetPage(1).GetImages());Assert.Equal(3,image.Bounds.Width/image.Bounds.Height,2);Assert.InRange(image.Bounds.Width,1,111);Assert.InRange(image.Bounds.Height,1,66);}
    }
}
