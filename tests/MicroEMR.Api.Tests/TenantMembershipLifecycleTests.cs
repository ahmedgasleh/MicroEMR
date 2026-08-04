using System.Reflection;
using MicroEMR.Api.Controllers;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class TenantMembershipLifecycleTests
{
    [Fact]
    public void PlatformLifecycleUsesExplicitTransitionsConcurrencyAuditAndSafetyGuards()
    {
        var sql = File.ReadAllText(FindRepositoryFile("db", "platform", "007_membership_activation_lifecycle.sql"));
        Assert.Contains("PlatformMembership_Deactivate", sql);
        Assert.Contains("PlatformMembership_Activate", sql);
        Assert.Contains("@Status<>'Active'", sql);
        Assert.Contains("@Status<>'Inactive'", sql);
        Assert.Contains("@CurrentRowVersion<>@ExpectedRowVersion", sql);
        Assert.Contains("@UserId=@ActorUserId", sql);
        Assert.Contains("RoleName=N'ClinicAdministrator'", sql);
        Assert.Contains("m.UserId<>@UserId", sql);
        Assert.Contains("MembershipStatus='Inactive'", sql);
        Assert.Contains("MembershipStatus='Active'", sql);
        Assert.Contains("MembershipDeactivated", sql);
        Assert.Contains("MembershipActivated", sql);
        Assert.Contains("TenantUid=@TenantUid", sql);
        Assert.DoesNotContain("UPDATE dbo.AspNetUsers", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ApplicationUser", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MutationContractContainsOnlyRowVersion()
    {
        var properties = typeof(MembershipRowVersionRequest).GetProperties();
        Assert.Single(properties);
        Assert.Equal("RowVersion", properties[0].Name);
        Assert.DoesNotContain(properties, x => x.Name.Contains("Tenant", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, x => x.Name.Contains("Actor", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, x => x.Name.Contains("Status", StringComparison.OrdinalIgnoreCase));
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. parts]);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }
}
