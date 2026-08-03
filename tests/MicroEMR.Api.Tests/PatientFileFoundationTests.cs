using Microsoft.Extensions.Configuration;
using MicroEMR.Application.PatientFiles;
using MicroEMR.Infrastructure.PatientFiles;
using MicroEMR.Infrastructure.Provisioning;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PatientFileFoundationTests
{
    [Fact]
    public async Task MigrationIsMetadataOnlyPatientScopedActiveAuditedAndConcurrent()
    {
        var source=new FileTenantDatabaseMigrationSource(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"TenantProvisioning:SqlAssetsPath",Path.Combine(AppContext.BaseDirectory,"database")}}).Build());
        var sql=Assert.Single(await source.GetAvailableMigrationsAsync(),x=>x.MigrationId=="0025-patient-files-foundation").Script;
        Assert.Contains("CREATE TABLE dbo.PatientFile",sql);Assert.Contains("Status IN(N'Active',N'Archived')",sql);Assert.Contains("RowVersion ROWVERSION",sql);Assert.Contains("FOREIGN KEY(PatientUid)",sql);Assert.Contains("ORDER BY UploadedAt DESC",sql);Assert.Contains("PatientUid=@PatientUid AND FileUid=@FileUid",sql);Assert.Contains("N'Status=Active'",sql);Assert.DoesNotContain("VARBINARY(MAX)",sql,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LocalStorageSupportsOpaqueNestedLifecycleAndRejectsTraversal()
    {
        var root=Path.Combine(Path.GetTempPath(),"microemr-file-test-"+Guid.NewGuid().ToString("N"));
        try
        {
            var storage=new LocalPatientFileStorage(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"PatientFileStorage:LocalRootPath",root}}).Build());
            var key=PatientFileNaming.StorageKey(Guid.NewGuid(),Guid.NewGuid());var bytes="harmless test"u8.ToArray();
            await storage.SaveAsync(new MemoryStream(bytes),key);Assert.True(await storage.ExistsAsync(key));
            await using(var opened=await storage.OpenReadAsync(key)){using var copy=new MemoryStream();await opened.CopyToAsync(copy);Assert.Equal(bytes,copy.ToArray());}
            await storage.DeleteAsync(key);Assert.False(await storage.ExistsAsync(key));
            await Assert.ThrowsAsync<ArgumentException>(()=>storage.ExistsAsync("../escape"));
            await Assert.ThrowsAsync<FileNotFoundException>(async()=>await storage.OpenReadAsync(key));
        }
        finally { if(Directory.Exists(root))Directory.Delete(root,true); }
    }

    [Fact]
    public void NamingUsesOpaqueIdentifiersAndStripsUserPaths()
    {
        var patient=Guid.NewGuid();var file=Guid.NewGuid();var key=PatientFileNaming.StorageKey(patient,file);
        Assert.Contains(patient.ToString("N"),key);Assert.Contains(file.ToString("N"),key);Assert.DoesNotContain("report.pdf",key);
        Assert.Equal("report.pdf",PatientFileNaming.OriginalFileName("../folder/report.pdf".Replace('/',Path.DirectorySeparatorChar)));
    }
}
