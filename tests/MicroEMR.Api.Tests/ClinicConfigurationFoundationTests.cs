using System.Reflection;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.ClinicConfiguration;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Infrastructure.Provisioning;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ClinicConfigurationFoundationTests
{
    [Fact]
    public async Task MigrationCreatesSingletonAuditedConcurrentProfileWithStoredProcedures()
    {
        var source = new FileTenantDatabaseMigrationSource(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenantProvisioning:SqlAssetsPath"] = Path.Combine(AppContext.BaseDirectory, "database")
            }).Build());
        var migration = Assert.Single(await source.GetAvailableMigrationsAsync(),
            x => x.MigrationId == "0028-clinic-configuration-foundation");
        var sql = migration.Script;

        Assert.Contains("CREATE TABLE dbo.ClinicProfile", sql);
        Assert.Contains("CHECK (ClinicProfileId = 1)", sql);
        Assert.Contains("RowVersion ROWVERSION", sql);
        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.ClinicProfile_Get", sql);
        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.ClinicProfile_Save", sql);
        Assert.Contains("THROW 51801", sql);
        Assert.Contains("INSERT dbo.AuditLog", sql);
        Assert.DoesNotContain("TenantUid", sql);
        Assert.DoesNotContain("ClinicName", sql);
        Assert.DoesNotContain("TimeZoneId", sql);
    }

    [Fact]
    public void ApiRequiresEffectiveClinicSettingsPermissionAndRequestIsNarrow()
    {
        var authorize = typeof(ClinicConfigurationController).GetCustomAttributes<AuthorizeAttribute>();
        Assert.Contains(authorize, x => x.Policy is null);
        Assert.Contains(authorize, x => x.Policy == PermissionPolicyProvider.Prefix + PermissionKeys.ClinicSettingsManage);

        var names = typeof(SaveClinicConfigurationRequest).GetProperties()
            .Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("TenantUid", names);
        Assert.DoesNotContain("TenantKey", names);
        Assert.DoesNotContain("ClinicName", names);
        Assert.DoesNotContain("TimeZoneId", names);
        Assert.Contains("RowVersion", names);
    }

    [Fact]
    public void RequestValidatesEmailLengthsAndAppointmentDuration()
    {
        var request = new SaveClinicConfigurationRequest
        {
            Email = "not-an-email",
            Phone = new string('1', 51),
            DefaultAppointmentDurationMinutes = 241
        };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, true));
        Assert.Contains(results, x => x.MemberNames.Contains(nameof(request.Email)));
        Assert.Contains(results, x => x.MemberNames.Contains(nameof(request.Phone)));
        Assert.Contains(results, x => x.MemberNames.Contains(nameof(request.DefaultAppointmentDurationMinutes)));
    }
}
