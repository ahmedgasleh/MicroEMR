using Microsoft.Extensions.Configuration;
using MicroEMR.Application.PatientReferrals;
using MicroEMR.Infrastructure.PatientReferrals;
using MicroEMR.Infrastructure.Provisioning;
using MicroEMR.Infrastructure.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PatientReferralFoundationTests
{
    private static async Task<string> ScriptAsync()
    {
        var source = new FileTenantDatabaseMigrationSource(
            new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["TenantProvisioning:SqlAssetsPath"] =
                        Path.Combine(AppContext.BaseDirectory, "database")
                }).Build());

        return Assert.Single(
            await source.GetAvailableMigrationsAsync(),
            item => item.MigrationId == "0021-patient-referrals-foundation").Script;
    }

    [Fact]
    public void StatusModelContainsOnlyCanonicalFoundationStatuses()
    {
        Assert.Equal(
            ["Draft", "Sent", "ResponseReceived", "Closed"],
            Enum.GetNames<ReferralStatus>());
    }

    [Fact]
    public void CreateContractCannotChooseStatusOrServerGeneratedFields()
    {
        var properties = typeof(CreatePatientReferralRequest)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("Status", properties);
        Assert.DoesNotContain("ReferralUid", properties);
        Assert.DoesNotContain("CreatedAt", properties);
        Assert.DoesNotContain("SentAt", properties);
        Assert.DoesNotContain("ResponseReceivedAt", properties);
        Assert.DoesNotContain("ClosedAt", properties);
    }

    [Fact]
    public async Task SchemaHasPatientActorStatusAndConcurrencyConstraints()
    {
        var sql = await ScriptAsync();

        Assert.Contains("CREATE TABLE dbo.PatientReferral", sql);
        Assert.Contains("FOREIGN KEY (PatientUid)", sql);
        Assert.Contains("REFERENCES dbo.Patient(PatientUid)", sql);
        Assert.Contains("FOREIGN KEY (CreatedBy)", sql);
        Assert.Contains("Status IN (N'Draft', N'Sent', N'ResponseReceived', N'Closed')", sql);
        Assert.Contains("RowVersion ROWVERSION NOT NULL", sql);
        Assert.Contains("IX_PatientReferral_PatientUid_CreatedAt", sql);
        Assert.Contains("IX_PatientReferral_PatientUid_Status", sql);
    }

    [Fact]
    public async Task CreateForcesDraftValidatesPatientAndActorAndWritesAudit()
    {
        var sql = await ScriptAsync();
        var create = sql[(sql.IndexOf("CREATE OR ALTER PROCEDURE dbo.PatientReferral_Create", StringComparison.Ordinal))..];

        Assert.DoesNotContain("@Status", create);
        Assert.Contains("DEFAULT N'Draft'", sql);
        Assert.Contains("WHERE p.PatientUid = @PatientUid", create);
        Assert.Contains("AND p.IsDeleted = 0", create);
        Assert.Contains("THROW 51500, 'Patient not found.'", create);
        Assert.Contains("WHERE UserId = @CreatedBy", create);
        Assert.Contains("INSERT dbo.AuditLog", create);
        Assert.Contains("N'Status=Draft'", create);
        Assert.Contains("EXEC dbo.PatientReferral_GetByUid", create);
    }

    [Fact]
    public async Task QueriesArePatientScopedAndListNewestFirst()
    {
        var sql = await ScriptAsync();

        Assert.Contains("WHERE r.PatientUid = @PatientUid", sql);
        Assert.Contains("AND r.ReferralUid = @ReferralUid", sql);
        Assert.Contains("ORDER BY r.CreatedAt DESC, r.PatientReferralId DESC", sql);
    }

    [Fact]
    public async Task ReturnedProjectionIncludesAllFieldsAndRowVersion()
    {
        var sql = await ScriptAsync();
        string[] columns =
        [
            "r.ReferralUid", "r.PatientUid", "r.RecipientName",
            "r.RecipientOrganization", "r.RecipientPhone", "r.RecipientFax",
            "r.Reason", "r.ClinicalSummary", "r.Status", "r.CreatedAt",
            "r.CreatedBy", "r.UpdatedAt", "r.UpdatedBy", "r.SentAt",
            "r.ResponseReceivedAt", "r.ClosedAt", "r.RowVersion"
        ];

        foreach (var column in columns)
        {
            Assert.Contains(column, sql);
        }
    }

    [Fact]
    public void RepositoryUsesTenantConnectionAndPatientScopedUidLookup()
    {
        var constructor = Assert.Single(typeof(PatientReferralRepository).GetConstructors());
        Assert.Contains(
            constructor.GetParameters(),
            parameter => parameter.ParameterType == typeof(ITenantSqlConnectionFactory));

        var getByUid = typeof(IPatientReferralRepository).GetMethod(nameof(IPatientReferralRepository.GetByUidAsync));
        Assert.NotNull(getByUid);
        Assert.Equal(
            [typeof(Guid), typeof(Guid), typeof(CancellationToken)],
            getByUid.GetParameters().Select(parameter => parameter.ParameterType));
    }
}
