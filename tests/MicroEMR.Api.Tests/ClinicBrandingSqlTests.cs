using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.ClinicConfiguration;
using MicroEMR.Infrastructure.ClinicConfiguration;
using MicroEMR.Infrastructure.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ClinicBrandingSqlTests(NextAvailableSqlDatabase db) : IClassFixture<NextAvailableSqlDatabase>
{
    [SchedulingSearchSqlFact]
    public async Task TenantPrivateLogoReplacementRemovalConcurrencyAndAuditRollbackPreserveContacts()
    {
        foreach(var connection in new[]{db.Connection,db.OtherConnection})
        {
            await Execute(connection,"ALTER TABLE dbo.ApplicationUser ADD IsActive bit NOT NULL DEFAULT 1;");
            foreach(var file in new[]{"0028-clinic-configuration-foundation.sql","0104-clinic-branding-logo.sql"})
            {
                var source=await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"database/tenant-clinical/migrations",file));
                foreach(var batch in Regex.Split(source,@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase).Where(x=>!string.IsNullOrWhiteSpace(x)))
                    await Execute(connection,batch);
            }
        }
        var write=new Writer(db.Connection);
        var repo=new ClinicLogoRepository(new NextAvailableSqlDatabase.Factory(db.Connection),write);
        var other=new ClinicLogoRepository(new NextAvailableSqlDatabase.Factory(db.OtherConnection),new Writer(db.OtherConnection));
        var contacts=new ClinicProfileRepository(new NextAvailableSqlDatabase.Factory(db.Connection),write,NullLogger<ClinicProfileRepository>.Instance);
        var original=await contacts.SaveAsync(new(){LegalName="Tenant A Clinic",Phone="555-0100"},7);
        var png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aWQAAAABJRU5ErkJggg==");
        var logo=new ClinicLogo(png,1,1);
        Assert.Null(await repo.GetAsync()); Assert.Null(await other.GetAsync());
        var first=await repo.SaveAsync(logo,null,7);
        Assert.Equal(png,(await repo.GetAsync())!.Logo!.PngBytes); Assert.Null(await other.GetAsync());
        await Assert.ThrowsAsync<ClinicConfigurationConcurrencyException>(()=>other.SaveAsync(logo,first.RowVersion,7));
        await other.SaveAsync(logo,null,7);
        var next=await repo.SaveAsync(logo,first.RowVersion,7);
        await Assert.ThrowsAsync<ClinicConfigurationConcurrencyException>(()=>repo.SaveAsync(null,first.RowVersion,7));
        await Execute(db.Connection,"CREATE TRIGGER dbo.BrandingAuditFailure ON dbo.AuditLog AFTER INSERT AS IF EXISTS(SELECT 1 FROM inserted WHERE EntityName='ClinicBrandingLogo') THROW 51999,'Test audit failure',1;");
        try
        {
            await Assert.ThrowsAsync<SqlException>(()=>repo.SaveAsync(null,next.RowVersion,7));
            Assert.Equal(next.RowVersion,(await repo.GetAsync())!.RowVersion);
        }
        finally {await Execute(db.Connection,"DROP TRIGGER dbo.BrandingAuditFailure;");}
        var removed=await repo.SaveAsync(null,next.RowVersion,7);
        Assert.Null(removed.Logo); Assert.NotNull((await other.GetAsync())!.Logo);
        await Assert.ThrowsAsync<ClinicConfigurationConcurrencyException>(()=>repo.SaveAsync(logo,null,7));
        Assert.Equal(original,await contacts.GetAsync());
        await using var sql=new SqlConnection(db.Connection);await sql.OpenAsync();
        await using var command=new SqlCommand("SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityName='ClinicBrandingLogo' AND PatientId IS NULL AND UserId=7 AND NewValue NOT LIKE '%PngBytes%';",sql);
        Assert.Equal(3,(int)(await command.ExecuteScalarAsync())!);
    }

    private static async Task Execute(string connection,string text)
    { await using var sql=new SqlConnection(connection);await sql.OpenAsync();await using var command=new SqlCommand(text,sql);await command.ExecuteNonQueryAsync(); }
    private sealed class Writer(string connection) : IAdministrativeTenantSqlConnectionFactory
    {public async Task<SqlConnection> OpenConnectionAsync(CancellationToken token=default){var sql=new SqlConnection(connection);await sql.OpenAsync(token);return sql;}}
}
