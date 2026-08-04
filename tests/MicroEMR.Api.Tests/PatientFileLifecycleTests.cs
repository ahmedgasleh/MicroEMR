using System.Reflection;
using MicroEMR.Application.PatientFiles;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.Patients.Contracts;
using MicroEMR.Application.Patients.Repositories;
using MicroEMR.Application.Tenancy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PatientFileLifecycleTests
{
    [Fact]
    public async Task ApplicationArchivesAndRestoresWithActorConcurrencyAndNoStorageMutation()
    {
        var repository=new Repository();var service=Service(repository);
        var uploadedBy=repository.Current.UploadedBy;var key=repository.Current.StorageKey;
        var archived=await service.ArchiveAsync(repository.Current.PatientUid,repository.Current.FileUid,repository.Current.RowVersion);
        Assert.Equal("Archived",archived.Status);Assert.Equal(99,archived.UpdatedBy);Assert.NotNull(archived.UpdatedAtUtc);Assert.Equal(uploadedBy,archived.UploadedBy);Assert.Equal(key,repository.Current.StorageKey);
        var restored=await service.RestoreAsync(repository.Current.PatientUid,repository.Current.FileUid,archived.RowVersion);
        Assert.Equal("Active",restored.Status);Assert.Equal(99,restored.UpdatedBy);Assert.NotEqual(archived.RowVersion,restored.RowVersion);Assert.Equal(2,repository.Mutations);
    }

    [Fact]
    public async Task ApplicationRejectsDuplicateAndStaleTransitionsWithoutMutation()
    {
        var repository=new Repository();var service=Service(repository);var stale=repository.Current.RowVersion;
        await service.ArchiveAsync(repository.Current.PatientUid,repository.Current.FileUid,stale);
        await Assert.ThrowsAsync<PatientFileInvalidTransitionException>(()=>service.ArchiveAsync(repository.Current.PatientUid,repository.Current.FileUid,repository.Current.RowVersion));
        await Assert.ThrowsAsync<PatientFileConcurrencyException>(()=>repository.RestoreAsync(repository.Current.PatientUid,repository.Current.FileUid,stale,99));
        Assert.Equal(1,repository.Mutations);
    }
    [Fact]
    public void MigrationDefinesOnlyAtomicPatientScopedArchiveAndRestore()
    {
        var sql=File.ReadAllText(Path.Combine(Root(),"db","tenant-clinical","migrations","0026-patient-file-lifecycle-security.sql"));
        Assert.Contains("PatientFile_Archive",sql);Assert.Contains("PatientFile_Restore",sql);
        Assert.Equal(2,Count(sql,"WITH(UPDLOCK,HOLDLOCK)"));Assert.Equal(2,Count(sql,"@Version<>@ExpectedRowVersion"));
        Assert.Equal(2,Count(sql,"INSERT dbo.AuditLog"));Assert.Contains("N'Status=Active',N'Status=Archived'",sql);Assert.Contains("N'Status=Archived',N'Status=Active'",sql);
        Assert.DoesNotContain("DELETE",sql,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("StorageKey=",sql,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ContractsExposeNarrowLifecycleWithoutStorageMutation()
    {
        var request=typeof(PatientFileLifecycleRequest).GetProperties();
        Assert.Equal("RowVersion",Assert.Single(request).Name);
        var repository=typeof(IPatientFileRepository).GetMethods().Select(x=>x.Name).ToArray();
        Assert.Contains("ArchiveAsync",repository);Assert.Contains("RestoreAsync",repository);
        Assert.DoesNotContain("DeleteAsync",repository);Assert.DoesNotContain("ReplaceAsync",repository);
        var response=typeof(PatientFileResponse).GetProperties().Select(x=>x.Name).ToArray();
        Assert.Contains("UpdatedAtUtc",response);Assert.Contains("UpdatedBy",response);Assert.DoesNotContain("StorageKey",response);
    }

    [Fact]
    public void WebUiShowsExactlyOneLifecycleActionFromPersistedStatus()
    {
        var script=File.ReadAllText(Path.Combine(Root(),"src","MicroEMR.Web","ClientApp","patients","patient-files.ts"));
        Assert.Contains("archived?\"Restore\":\"Archive\"",script);Assert.Contains("data-row-version",script);
        Assert.Contains("changed by another user",File.ReadAllText(Path.Combine(Root(),"src","MicroEMR.Web","Services","PatientFiles","PatientFileApiClient.cs")));
        Assert.DoesNotContain(">Delete<",script);Assert.DoesNotContain(">Purge<",script);Assert.DoesNotContain(">Replace<",script);
    }

    private static int Count(string value,string token)=>(value.Length-value.Replace(token,"",StringComparison.OrdinalIgnoreCase).Length)/token.Length;
    private static string Root([System.Runtime.CompilerServices.CallerFilePath]string file="")=>Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!,"..",".."));
    private static PatientFileService Service(Repository r)=>new(r,new NoStorage(),new NoPatients(),new Actor(),new Tenant(),Options.Create(new PatientFileUploadOptions()),NullLogger<PatientFileService>.Instance);
    private sealed class Repository:IPatientFileRepository
    {
        public PatientFile Current{get;private set;}=new(){FileUid=Guid.NewGuid(),PatientUid=Guid.NewGuid(),OriginalFileName="safe.pdf",StorageKey="tenants/a/patients/b/file",ContentType="application/pdf",FileSizeBytes=10,Status=PatientFileStatus.Active,UploadedAtUtc=DateTime.UtcNow.AddDays(-1),UploadedBy=7,RowVersion=Convert.ToBase64String(new byte[8])};public int Mutations{get;private set;}
        public Task<IReadOnlyList<PatientFile>>GetByPatientUidAsync(Guid p,CancellationToken c=default)=>Task.FromResult<IReadOnlyList<PatientFile>>([Current]);
        public Task<PatientFile?>GetByUidAsync(Guid p,Guid f,CancellationToken c=default)=>Task.FromResult<PatientFile?>(p==Current.PatientUid&&f==Current.FileUid?Current:null);
        public Task<PatientFile>CreateAsync(Guid p,CreatePatientFileMetadata m,long a,CancellationToken c=default)=>throw new NotSupportedException();
        public Task<PatientFile>ArchiveAsync(Guid p,Guid f,string v,long a,CancellationToken c=default)=>Change(p,f,v,a,PatientFileStatus.Active,PatientFileStatus.Archived);
        public Task<PatientFile>RestoreAsync(Guid p,Guid f,string v,long a,CancellationToken c=default)=>Change(p,f,v,a,PatientFileStatus.Archived,PatientFileStatus.Active);
        private Task<PatientFile>Change(Guid p,Guid f,string v,long a,PatientFileStatus expected,PatientFileStatus next){if(v!=Current.RowVersion)throw new PatientFileConcurrencyException();if(Current.Status!=expected)throw new PatientFileInvalidTransitionException();Mutations++;Current=new(){FileUid=Current.FileUid,PatientUid=Current.PatientUid,OriginalFileName=Current.OriginalFileName,StorageKey=Current.StorageKey,ContentType=Current.ContentType,FileSizeBytes=Current.FileSizeBytes,Status=next,UploadedAtUtc=Current.UploadedAtUtc,UploadedBy=Current.UploadedBy,UpdatedAtUtc=DateTime.UtcNow,UpdatedBy=a,RowVersion=Convert.ToBase64String([0,0,0,0,0,0,0,(byte)Mutations])};return Task.FromResult(Current);}
    }
    private sealed class NoStorage:IPatientFileStorage{public Task SaveAsync(Stream c,string k,CancellationToken t=default)=>throw new Xunit.Sdk.XunitException("Lifecycle called storage.");public Task<Stream>OpenReadAsync(string k,CancellationToken t=default)=>throw new Xunit.Sdk.XunitException("Lifecycle called storage.");public Task<bool>ExistsAsync(string k,CancellationToken t=default)=>throw new Xunit.Sdk.XunitException("Lifecycle called storage.");public Task DeleteAsync(string k,CancellationToken t=default)=>throw new Xunit.Sdk.XunitException("Lifecycle called storage.");}
    private sealed class Actor:IAuthenticatedClinicalUserAccessor{public Task<long>GetRequiredUserIdAsync(CancellationToken c=default)=>Task.FromResult(99L);}
    private sealed class Tenant:ITenantContext{public Guid TenantUid=>Guid.NewGuid();public string TenantKey=>"test";public string DisplayName=>"Test";}
    private sealed class NoPatients:IPatientRepository
    {public Task<PatientSearchResponse>SearchAsync(string?s,DateOnly?d,int p,int z,bool i,CancellationToken c=default)=>throw new NotSupportedException();public Task<PatientDetailsResponse?>GetByUidAsync(Guid p,CancellationToken c=default)=>throw new NotSupportedException();public Task<PatientDetailsResponse>CreateAsync(CreatePatientRequest r,long?u,CancellationToken c=default)=>throw new NotSupportedException();public Task<PatientDetailsResponse?>UpdateDemographicsAsync(Guid p,UpdatePatientDemographicsRequest r,long?u,CancellationToken c=default)=>throw new NotSupportedException();}
}
