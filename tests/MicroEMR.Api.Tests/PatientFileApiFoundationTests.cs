using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.PatientFiles;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PatientFileApiFoundationTests
{
    [Fact]
    public void ControllerIsAuthenticatedPatientScopedAndUploadIsBounded()
    {
        var type=typeof(PatientFilesController);
        Assert.NotNull(type.GetCustomAttributes(typeof(AuthorizeAttribute),true).SingleOrDefault());
        Assert.Equal("api/patients/{patientUid:guid}/files",type.GetCustomAttributes(typeof(RouteAttribute),true).Cast<RouteAttribute>().Single().Template);
        var upload=type.GetMethod(nameof(PatientFilesController.Upload))!;
        Assert.Single(upload.GetCustomAttributes(typeof(RequestSizeLimitAttribute),true));
        Assert.DoesNotContain(upload.GetParameters(),p=>p.Name is "tenantUid" or "uploadedBy" or "storageKey");
    }

    [Fact]
    public void ApiResponseNeverExposesStorageKeyOrPhysicalPath()
    {
        var names=typeof(PatientFileResponse).GetProperties().Select(x=>x.Name).ToHashSet();
        Assert.DoesNotContain("StorageKey",names);Assert.DoesNotContain("StoragePath",names);
        Assert.Contains("Sha256Hash",names);Assert.Contains("RowVersion",names);
    }

    [Fact]
    public void ServiceRequiresTenantActorStorageAndRepositoryDependencies()
    {
        var parameters=Assert.Single(typeof(PatientFileService).GetConstructors()).GetParameters().Select(x=>x.ParameterType).ToArray();
        Assert.Contains(typeof(IPatientFileRepository),parameters);Assert.Contains(typeof(IPatientFileStorage),parameters);
        Assert.Contains(parameters,x=>x.Name=="ITenantContext");
        Assert.Contains(parameters,x=>x.Name=="IAuthenticatedClinicalUserAccessor");
        var input=typeof(UploadPatientFileInput).GetProperties().Select(x=>x.Name).ToHashSet();
        Assert.DoesNotContain("PatientUid",input);Assert.DoesNotContain("StorageKey",input);Assert.DoesNotContain("UploadedBy",input);
    }
}
