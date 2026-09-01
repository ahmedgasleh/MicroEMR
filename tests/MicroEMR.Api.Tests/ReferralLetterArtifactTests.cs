using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ReferralLetterArtifactTests
{
    private static readonly string Root=FindRoot();
    private static string Source(params string[] parts)=>File.ReadAllText(Path.Combine([Root,..parts]));
    private static readonly string Migration=Source("db","tenant-clinical","migrations","0056-referral-letter-artifact.sql");

    [Fact] public void Migration0056IsUniqueAndManifestedAfter0055()
    {
        var manifest=Source("db","tenant-clinical","manifest.json");
        Assert.Single(Directory.GetFiles(Path.Combine(Root,"db","tenant-clinical","migrations"),"0056-*.sql"));
        Assert.Contains("0055-verified-negative-allergy-assertion",manifest);
        Assert.Contains("\"migrationId\": \"0056-referral-letter-artifact\"",manifest);
    }

    [Fact] public void DraftEditIsProviderStructuredActorBoundAndConcurrent()
    {
        var procedure=Procedure("PatientReferral_UpdateDraft");
        Assert.Contains("@ReferringProviderUid UNIQUEIDENTIFIER",procedure);
        Assert.Contains("Status=N'Draft' AND RowVersion=@ExpectedRowVersion",procedure);
        Assert.Contains("UpdatedBy=@UpdatedBy",procedure);
        Assert.Contains("Active referring provider not found",procedure);
        Assert.DoesNotContain("@CreatedBy",procedure);
    }

    [Fact] public void SendPersistsExactlyOneImmutableArtifactAndStatusAtomically()
    {
        Assert.Contains("CONSTRAINT UQ_PatientReferralArtifact_Referral UNIQUE(ReferralUid)",Migration);
        Assert.Contains("PdfContent VARBINARY(MAX) NOT NULL",Migration);
        Assert.Contains("SnapshotJson NVARCHAR(MAX) NOT NULL",Migration);
        var procedure=Procedure("PatientReferral_Send");
        Assert.Contains("WITH(UPDLOCK,HOLDLOCK)",procedure);
        Assert.Contains("IF @Status<>N'Draft'",procedure);
        Assert.Contains("IF @Version<>@ExpectedRowVersion",procedure);
        Assert.Contains("INSERT dbo.PatientReferralArtifact",procedure);
        Assert.Contains("Status=N'Sent',SentAt=@ChangedAt",procedure);
        Assert.Contains("N'ReferralSent'",procedure);
        Assert.Contains("BEGIN TRANSACTION",procedure);
        Assert.Contains("COMMIT",procedure);
    }

    [Fact] public void ArtifactLookupIsPatientAndReferralScoped()
    {
        var procedure=Procedure("PatientReferralArtifact_Get");
        Assert.Contains("r.PatientUid=a.PatientUid",procedure);
        Assert.Contains("a.PatientUid=@PatientUid AND a.ReferralUid=@ReferralUid",procedure);
    }

    [Fact] public void SnapshotCompositionIsBoundedAndDoesNotIncludeCpp()
    {
        var service=Source("src","MicroEMR.Application","PatientReferrals","PatientReferralService.cs");
        Assert.Contains("PatientName=patient.FullName",service);
        Assert.Contains("ProviderName=provider.DisplayName",service);
        Assert.Contains("SupportingDocuments=documents.Select",service);
        Assert.Contains("SHA256.HashData(bytes)",service);
        Assert.DoesNotContain("PatientCpp",service);
        Assert.DoesNotContain("ClinicalSummary",Source("src","MicroEMR.Web","SafeRequestTelemetryMiddleware.cs"));
    }

    [Fact] public void ExistingPermissionsProtectAllNewApiOperations()
    {
        var controller=Source("src","MicroEMR.Api","Controllers","PatientReferralsController.cs");
        Assert.Contains("[RequirePermission(PermissionKeys.ReferralsView)]",controller);
        Assert.Contains("[HttpPut(\"{referralUid:guid}\"), RequirePermission(PermissionKeys.ReferralsManage)]",controller);
        Assert.Contains("letter/preview\"), RequirePermission(PermissionKeys.ReferralsManage)",controller);
        Assert.Contains("[HttpGet(\"{referralUid:guid}/letter\")]",controller);
    }

    [Fact] public void SupportingDocumentMutationsAdvanceAggregateVersion()
    {
        Assert.Equal(2,Count(Migration,"UPDATE dbo.PatientReferral SET UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@Actor"));
    }

    [Fact] public void PatientChartOffersDraftAndFinalLetterActionsWithoutTransmission()
    {
        var web=Source("src","MicroEMR.Web","ClientApp","patients","patient-referrals.ts");
        Assert.Contains("Edit Draft",web);Assert.Contains("Preview Letter",web);Assert.Contains("View Referral Letter",web);
        Assert.DoesNotContain("Send Fax",web);Assert.DoesNotContain("Send Email",web);Assert.DoesNotContain("Ocean",web);
    }

    private static string Procedure(string name)
    {
        var start=Migration.IndexOf($"CREATE OR ALTER PROCEDURE dbo.{name}",StringComparison.Ordinal);
        Assert.True(start>=0);var end=Migration.IndexOf("\nGO",start,StringComparison.Ordinal);return Migration[start..end];
    }
    private static int Count(string value,string token)=>(value.Length-value.Replace(token,string.Empty,StringComparison.Ordinal).Length)/token.Length;
    private static string FindRoot(){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d is not null&&!File.Exists(Path.Combine(d.FullName,"MicroEMR.slnx")))d=d.Parent;return d?.FullName??throw new InvalidOperationException("Root not found.");}
}
