using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.ClinicConfiguration;
using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.PatientDocuments.Repositories;
using MicroEMR.Application.PatientDocuments.Services;
using MicroEMR.Application.Patients.Contracts;
using MicroEMR.Application.Patients.Services;
using MicroEMR.Application.ReadAudit;
using MicroEMR.Application.Templates.Output;
using MicroEMR.Application.Templates.Runtime;
using MicroEMR.Application.Templates.Serialization;
using MicroEMR.Application.Templates.Validation;
using MicroEMR.Application.Templates.Variables;
using Xunit;
using static MicroEMR.Api.Tests.ConsultationSigningTests;

namespace MicroEMR.Api.Tests;

public sealed class ConsultationFinalPdfTests
{
    [Fact]
    public async Task FinalRendererIncludesEncodedSavedRecipientsSignerAndTrustedTime()
    {
        var sql = File.ReadAllText(Path.Combine(Root(), "db/tenant-clinical/migrations/0063-consultation-report-template.sql"));
        var definitionJson = Regex.Match(sql, @"DECLARE @DefinitionJson NVARCHAR\(MAX\) = N'(?<json>.*?)';", RegexOptions.Singleline).Groups["json"].Value;
        var serializer = new TemplateDefinitionSerializer(new TemplateDefinitionValidator());
        var definition = serializer.Process(definitionJson).Definition!;
        var runtime = new TemplateInstanceRuntime(serializer);
        var document = new PatientDocumentDetailsResponse
        {
            DocumentUid = Guid.NewGuid(), PatientUid = Guid.NewGuid(), TemplateUid = Guid.NewGuid(), TemplateVersionUid = Guid.NewGuid(),
            Title = "Consultation Report", Status = "Draft", StructuredDataJson = runtime.CreateInitial(definition).Json
        };
        var versions = Proxy<IDocumentTemplateVersionRepository>((_, _) => Task.FromResult<DocumentTemplateVersionResponse?>(new()
        { TemplateUid = document.TemplateUid.Value, TemplateVersionUid = document.TemplateVersionUid.Value, DefinitionJson = definitionJson }));
        var patients = Proxy<IPatientService>((_, _) => Task.FromResult<PatientDetailsResponse?>(new()
        { FirstName = "Jane", LastName = "Patient", DateOfBirth = new(1970, 1, 5), ChartNumber = "C100" }));
        var clinic = Proxy<IClinicConfigurationService>((_, _) => Task.FromResult(new ClinicConfigurationResponse(
            "Test Clinic", "UTC", null, null, null, null, null, null, null, null, null, null, null, null, null, null)));
        string? rendered = null;
        var pdf = Proxy<IPdfRenderer>((_, args) => { rendered = (string)args![0]!; return Task.FromResult("%PDF-test"u8.ToArray()); });
        var service = new ClinicalPdfPreviewService(null!, null!, versions, patients, clinic, serializer, runtime,
            new TemplateOutputBuilder(new TemplateVariableResolver()), new TemplateHtmlRenderer(), new ClinicalPrintLayoutRenderer(),
            pdf, TimeProvider.System, NullLogger<ClinicalPdfPreviewService>.Instance);
        var snapshot = new ConsultationSigningContext(document,
            [new(Guid.NewGuid(), 1, "TO", Guid.NewGuid(), "Saved <Recipient>", "Saved Clinic", "416-555-0100")],
            Guid.NewGuid(), "Dr. <Signer>", new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc));
        await service.RenderConsultationFinalAsync(snapshot);
        Assert.Contains("Test Clinic", rendered);
        Assert.Contains("Jane Patient", rendered);
        Assert.Contains("Saved &lt;Recipient&gt;", rendered);
        Assert.Contains("Saved Clinic", rendered);
        Assert.Contains("416-555-0100", rendered);
        Assert.Contains("Dr. &lt;Signer&gt;", rendered);
        Assert.Contains("2026", rendered);
        Assert.Contains("TO", rendered);
        Assert.DoesNotContain("<Recipient>", rendered);
    }

    [Theory]
    [InlineData(false, ReadAuditActions.PatientDocumentViewed)]
    [InlineData(true, ReadAuditActions.PatientDocumentDownloaded)]
    public async Task FinalPdfReadAuditsCorrectActionBeforeReturningBytes(bool download, string expectedAction)
    {
        var patient = Guid.NewGuid(); var document = Guid.NewGuid(); var recorded = false;
        var audit = Proxy<IStructuredReadAuditService>((_, args) =>
        {
            Assert.Equal(expectedAction, args![0]); Assert.Equal(ReadAuditResourceTypes.PatientDocument, args[1]);
            Assert.Equal(document, args[2]); Assert.Equal(patient, args[3]); recorded = true;
            return Task.FromResult(Guid.NewGuid());
        });
        var signing = Proxy<IConsultationSigningService>((_, _) => Task.FromResult<ClinicalArtifactContent?>(
            new(new MemoryStream("%PDF-final"u8.ToArray()), "final.pdf", "application/pdf", 10)));
        var controller = new MicroEMR.Api.Controllers.PatientDocumentsController(null!, null!, null!, null!, audit, null!)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var result = Assert.IsType<FileStreamResult>(await controller.FinalPdf(patient, document, download, signing, default));
        Assert.True(recorded);
        Assert.Equal(download ? "final.pdf" : "", result.FileDownloadName);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task AuditFailurePreventsPdfDisclosureAndDisposesStream()
    {
        var stream = new MemoryStream("%PDF-final"u8.ToArray());
        var signing = Proxy<IConsultationSigningService>((_, _) => Task.FromResult<ClinicalArtifactContent?>(
            new(stream, "final.pdf", "application/pdf", 10)));
        var audit = Proxy<IStructuredReadAuditService>((_, _) => Task.FromException<Guid>(new IOException("Audit unavailable")));
        var controller = new MicroEMR.Api.Controllers.PatientDocumentsController(null!, null!, null!, null!, audit, null!)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        await Assert.ThrowsAsync<IOException>(() => controller.FinalPdf(Guid.NewGuid(), Guid.NewGuid(), true, signing, default));
        Assert.False(stream.CanRead);
    }

    [Fact]
    public async Task EditPermissionDoesNotAuthorizeSigning()
    {
        var requirement = new PermissionRequirement(PermissionKeys.DocumentsSign);
        var permissions = Proxy<ICurrentUserPermissionService>((_, args) => Task.FromResult((string)args![0]! == PermissionKeys.DocumentsManage));
        var handler = new PermissionAuthorizationHandler(permissions, NullLogger<PermissionAuthorizationHandler>.Instance);
        var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity([], "test"));
        var context = new AuthorizationHandlerContext([requirement], principal, null);
        await handler.HandleAsync(context);
        Assert.False(context.HasSucceeded);
        var method = typeof(MicroEMR.Api.Controllers.PatientDocumentsController).GetMethod("SignConsultation")!;
        Assert.Contains(method.GetCustomAttributes(typeof(RequirePermissionAttribute), true).Cast<RequirePermissionAttribute>(),
            x => x.Policy == PermissionPolicyProvider.Prefix + PermissionKeys.DocumentsSign);
        var migration = File.ReadAllText(Path.Combine(Root(), "db/platform/025_document_sign_permission.sql"));
        Assert.Contains("N'Documents.Sign'", migration);
        Assert.DoesNotContain("AccessProfile_SeedDefaults", migration);
    }
}
