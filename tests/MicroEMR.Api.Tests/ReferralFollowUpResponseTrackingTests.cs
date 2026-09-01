using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientReferrals;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ReferralFollowUpResponseTrackingTests
{
    private static readonly string Sql = File.ReadAllText(Path.Combine(Root(), "db", "tenant-clinical", "migrations", "0058-referral-followup-response-tracking.sql"));

    [Fact]
    public void Migration0058IsUniqueAndLast()
    {
        using var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(),"db","tenant-clinical","manifest.json")));
        var ids=manifest.RootElement.EnumerateArray().Select(x=>x.GetProperty("migrationId").GetString()).ToArray();
        Assert.Equal(59,ids.Length);
        Assert.Equal("0057-provider-management-foundation",ids[^2]);
        Assert.Equal("0058-referral-followup-response-tracking",ids[^1]);
        Assert.Single(ids,x=>x=="0058-referral-followup-response-tracking");
    }

    [Fact]
    public void FollowUpIsManualConcurrentAndAudited()
    {
        Assert.Contains("@FollowUpDueAt DATETIME2(0)=NULL",Sql);
        Assert.DoesNotContain("DATEADD",Sql,StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@Version<>@ExpectedRowVersion",Sql);
        Assert.Contains("ReferralFollowUpScheduled",Sql);
        Assert.Contains("ReferralFollowUpChanged",Sql);
        Assert.Contains("ReferralFollowUpCleared",Sql);
        Assert.Contains("@Status NOT IN(N'Draft',N'Sent')",Sql);
    }

    [Theory]
    [InlineData(ReferralStatus.Sent,true)]
    [InlineData(ReferralStatus.Draft,false)]
    [InlineData(ReferralStatus.ResponseReceived,false)]
    [InlineData(ReferralStatus.Closed,false)]
    public void OverdueIsDerivedOnlyForSent(ReferralStatus status,bool expected)
    {
        Assert.Equal(expected,ReferralFollowUpRule.IsOverdue(
            new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc),status,
            new DateTime(2026,1,2,0,0,0,DateTimeKind.Utc)));
    }

    [Fact]
    public void ResponseAndCloseAreServerTimedExactlyOnceAndAtomic()
    {
        Assert.Contains("@ChangedAt DATETIME2(0)=SYSUTCDATETIME()",Sql);
        Assert.Contains("IF @Status<>N'Sent'",Sql);
        Assert.Contains("N'ReferralResponseReceived'",Sql);
        Assert.Contains("IF @Status<>N'ResponseReceived'",Sql);
        Assert.Contains("N'ReferralClosed'",Sql);
        Assert.Contains("SET XACT_ABORT ON",Sql);
        Assert.Contains("BEGIN TRANSACTION",Sql);
    }

    [Fact]
    public void ResponseDocumentIsPatientScopedAndDoesNotCopyContent()
    {
        Assert.Contains("PatientDocumentUid=@DocumentUid AND PatientUid=@PatientUid AND IsDeleted=0",Sql);
        Assert.Contains("FK_PatientReferral_ResponseDocument",Sql);
        Assert.DoesNotContain("VARBINARY",Sql,StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ReferralResponseDocumentLinked",Sql);
        Assert.Contains("ReferralResponseDocumentUnlinked",Sql);
    }

    [Fact]
    public void MutationEndpointsUseExistingManagePermission()
    {
        foreach(var name in new[]{nameof(PatientReferralsController.SetFollowUp),nameof(PatientReferralsController.SetResponseDocument),nameof(PatientReferralsController.ClearResponseDocument)})
        {
            var method=typeof(PatientReferralsController).GetMethod(name)!;
            var permission=method.GetCustomAttribute<RequirePermissionAttribute>();
            Assert.NotNull(permission);
            Assert.Equal(PermissionPolicyProvider.Prefix+PermissionKeys.ReferralsManage,permission!.Policy);
        }
        Assert.Contains(typeof(PatientReferralsController).GetCustomAttributes<AuthorizeAttribute>(),
            attribute=>string.IsNullOrEmpty(attribute.Policy));
    }

    [Fact]
    public void Step36ADoesNotContainStep36BFields()
    {
        var artifactSql=File.ReadAllText(Path.Combine(Root(),"db","tenant-clinical","migrations","0056-referral-letter-artifact.sql"));
        Assert.DoesNotContain("FollowUpDueAt",artifactSql);
        Assert.DoesNotContain("ResponseDocumentUid",artifactSql);
    }

    private static string Root()
    {
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null&&!File.Exists(Path.Combine(directory.FullName,"MicroEMR.slnx")))directory=directory.Parent;
        return directory?.FullName??throw new DirectoryNotFoundException();
    }
}
