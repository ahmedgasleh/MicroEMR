using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.PatientReferrals;
using MicroEMR.Infrastructure.PatientReferrals;
using MicroEMR.Infrastructure.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ReferralFinalizationSqlTests
{
    [FinalizationSqlFact]
    public async Task ExistingSendTransactionGuardsPreservationScopeAndSentComposition()
    {
        await using var local=new Database(); await local.Initialize();
        await using var otherTenant=new Database(); await otherTenant.Initialize();
        var patient=Guid.NewGuid(); var provider=Guid.NewGuid(); var document=Guid.NewGuid();
        await local.Seed(patient,provider,document);
        var repo=local.Repository; var docs=new ReferralDocumentRepository(local);
        var draft=(await repo.CreateAsync(patient,new() {RecipientName="Recipient",Reason="Reason",ReferringProviderUid=provider},7));
        var originalVersion=draft.RowVersion;
        await docs.LinkAsync(patient,draft.ReferralUid,document,draft.RowVersion,7);
        draft=(await repo.GetByUidAsync(patient,draft.ReferralUid))!;
        var choices=(await repo.ReplaceDraftClinicalSelectionsAsync(patient,draft.ReferralUid,
            new() {RowVersion=draft.RowVersion,Selections=[new("CPP","PROBLEMS")]},7))!;
        var bytes=Encoding.ASCII.GetBytes("%PDF-1.7\nPreserved referral test output\n%%EOF");
        var artifact=new ReferralArtifactWrite(Guid.NewGuid(),new(2026,6,15,2,0,0,DateTimeKind.Utc),bytes,
            "referral.pdf",Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),"{}","Referrer",null);
        await Assert.ThrowsAsync<PatientReferralConcurrencyException>(()=>repo.SendWithArtifactAsync(patient,draft.ReferralUid,originalVersion,7,artifact));
        Assert.Null(await repo.GetArtifactAsync(patient,draft.ReferralUid));
        Assert.Equal(ReferralStatus.Draft,(await repo.GetByUidAsync(patient,draft.ReferralUid))!.Status);
        // Existing size constraint must roll back the entire transaction.
        await Assert.ThrowsAsync<SqlException>(()=>repo.SendWithArtifactAsync(patient,draft.ReferralUid,choices.RowVersion,7,artifact with {PdfContent=[]}));
        Assert.Null(await repo.GetArtifactAsync(patient,draft.ReferralUid));
        Assert.Equal(ReferralStatus.Draft,(await repo.GetByUidAsync(patient,draft.ReferralUid))!.Status);
        // Fail after artifact insertion: audit failure must roll back both artifact and Sent.
        await local.Execute("CREATE TRIGGER dbo.FailSendAudit ON dbo.AuditLog AFTER INSERT AS BEGIN THROW 51999,'Test finalization audit failure.',1; END");
        var failed=await Assert.ThrowsAsync<SqlException>(()=>repo.SendWithArtifactAsync(patient,draft.ReferralUid,choices.RowVersion,7,artifact));
        Assert.Equal(51999,failed.Number);
        Assert.Null(await repo.GetArtifactAsync(patient,draft.ReferralUid));
        Assert.Equal(ReferralStatus.Draft,(await repo.GetByUidAsync(patient,draft.ReferralUid))!.Status);
        await local.Execute("DROP TRIGGER dbo.FailSendAudit");
        // An ended supporting source must not silently disappear during final composition.
        await local.Execute("UPDATE dbo.PatientDocument SET IsDeleted=1 WHERE PatientDocumentUid=@Uid",document);
        await Assert.ThrowsAsync<ReferralClinicalSelectionRuleException>(()=>docs.GetByReferralUidAsync(patient,draft.ReferralUid,requireAllLinkedDocuments:true));
        await local.Execute("UPDATE dbo.PatientDocument SET IsDeleted=0 WHERE PatientDocumentUid=@Uid",document);
        Assert.Single(await docs.GetByReferralUidAsync(patient,draft.ReferralUid,requireAllLinkedDocuments:true));
        var sent=(await repo.SendWithArtifactAsync(patient,draft.ReferralUid,choices.RowVersion,7,artifact))!;
        Assert.Equal(ReferralStatus.Sent,sent.Status); Assert.NotEqual(choices.RowVersion,sent.RowVersion);
        Assert.Equal(artifact.ArtifactUid,sent.ArtifactUid);
        var preserved=(await repo.GetArtifactAsync(patient,draft.ReferralUid))!;
        Assert.Equal(bytes,preserved.PdfContent); Assert.Equal(artifact.Sha256,preserved.Sha256);
        Assert.Equal(bytes.LongLength,preserved.FileSizeBytes);
        Assert.Equal(1,await local.Scalar("SELECT COUNT(*) FROM dbo.PatientReferralArtifact"));
        Assert.Equal(1,await local.Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE ActionName=N'ReferralSent' AND UserId=7"));
        Assert.Null(await repo.GetArtifactAsync(Guid.NewGuid(),draft.ReferralUid));
        Assert.Null(await otherTenant.Repository.GetArtifactAsync(patient,draft.ReferralUid));
        await Assert.ThrowsAsync<PatientReferralTransitionException>(()=>repo.SendWithArtifactAsync(patient,draft.ReferralUid,sent.RowVersion,7,artifact with {ArtifactUid=Guid.NewGuid()}));
        await Assert.ThrowsAsync<PatientReferralConcurrencyException>(()=>repo.UpdateDraftAsync(patient,draft.ReferralUid,
            new() {RowVersion=sent.RowVersion,RecipientName="Changed",Reason="Changed",ReferringProviderUid=provider},7));
        await Assert.ThrowsAsync<PatientReferralTransitionException>(()=>repo.ReplaceDraftClinicalSelectionsAsync(patient,draft.ReferralUid,new() {RowVersion=sent.RowVersion},7));
        await Assert.ThrowsAsync<ReferralDocumentRuleException>(()=>docs.UnlinkAsync(patient,draft.ReferralUid,document,sent.RowVersion,7));
        await Assert.ThrowsAsync<ReferralDocumentRuleException>(()=>docs.LinkAsync(patient,draft.ReferralUid,document,sent.RowVersion,7));
        await local.Execute("UPDATE dbo.Provider SET DisplayName=N'Changed' WHERE ProviderUid=@Uid",provider);
        await local.Execute("UPDATE dbo.PatientDocument SET DocumentTitle=N'Changed' WHERE PatientDocumentUid=@Uid",document);
        var followUp=(await repo.SetFollowUpDueAsync(patient,draft.ReferralUid,DateTime.UtcNow.AddDays(1),sent.RowVersion,7))!;
        var response=(await repo.MarkResponseReceivedAsync(patient,draft.ReferralUid,followUp.RowVersion,7))!;
        var responseLink=(await repo.SetResponseDocumentAsync(patient,draft.ReferralUid,document,response.RowVersion,7))!;
        var closed=(await repo.CloseAsync(patient,draft.ReferralUid,responseLink.RowVersion,7))!;
        Assert.Equal(ReferralStatus.Closed,closed.Status); Assert.Equal(artifact.SentAtUtc,closed.SentAt);
        var final=(await repo.GetArtifactAsync(patient,draft.ReferralUid))!;
        Assert.Equal(preserved.PdfContent,final.PdfContent); Assert.Equal(preserved.Sha256,final.Sha256);
    }

    public sealed class FinalizationSqlFactAttribute:FactAttribute
    {
        public FinalizationSqlFactAttribute()
        {
            if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MICROEMR_REFERRAL_FINALIZATION_TEST_CONNECTION")))
                Skip="Set MICROEMR_REFERRAL_FINALIZATION_TEST_CONNECTION for disposable SQL Server test databases.";
        }
    }
    private sealed class Database:ITenantSqlConnectionFactory,IAsyncDisposable
    {
        private readonly string name="MicroEMR_ReferralFinalizationTest_"+Guid.NewGuid().ToString("N");
        private readonly SqlConnectionStringBuilder connection=new(Environment.GetEnvironmentVariable("MICROEMR_REFERRAL_FINALIZATION_TEST_CONNECTION")!);
        private bool created;
        public PatientReferralRepository Repository=>new(this,NullLogger<PatientReferralRepository>.Instance);
        public async Task Initialize()
        {
            connection.InitialCatalog="master";
            await using(var sql=new SqlConnection(connection.ConnectionString))
            {
                await sql.OpenAsync(); await using var command=sql.CreateCommand();
                command.CommandText=$"CREATE DATABASE [{name}]"; await command.ExecuteNonQueryAsync(); created=true;
            }
            connection.InitialCatalog=name;
            await Execute("""
                CREATE TABLE dbo.Provider(ProviderId bigint IDENTITY PRIMARY KEY,ProviderUid uniqueidentifier UNIQUE NOT NULL,
                    DisplayName nvarchar(200),ProviderType nvarchar(100),BillingNumber nvarchar(100),Specialty nvarchar(100),IsActive bit NOT NULL DEFAULT 1);
                CREATE TABLE dbo.ApplicationUser(UserId bigint PRIMARY KEY,IsActive bit NOT NULL,ProviderId bigint NULL,DisplayName nvarchar(200));
                INSERT dbo.ApplicationUser(UserId,IsActive,DisplayName) VALUES(7,1,N'Clinical actor');
                CREATE TABLE dbo.Patient(PatientId bigint IDENTITY PRIMARY KEY,PatientUid uniqueidentifier UNIQUE NOT NULL,IsDeleted bit NOT NULL DEFAULT 0);
                CREATE TABLE dbo.PatientDocument(PatientDocumentUid uniqueidentifier PRIMARY KEY,PatientUid uniqueidentifier,
                    DocumentTitle nvarchar(200),DocumentType nvarchar(100),DocumentStatus nvarchar(30),IsDeleted bit NOT NULL DEFAULT 0,
                    CreatedAt datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),CreatedBy bigint NULL);
                CREATE TABLE dbo.PatientEncounter(EncounterUid uniqueidentifier PRIMARY KEY,PatientUid uniqueidentifier,Status nvarchar(30));
                CREATE TABLE dbo.PatientResult(PatientResultUid uniqueidentifier PRIMARY KEY,PatientUid uniqueidentifier,LifecycleStatus nvarchar(20));
                CREATE TABLE dbo.AuditLog(AuditLogId bigint IDENTITY PRIMARY KEY,UserId bigint,PatientId bigint,ActionName nvarchar(100),
                    EntityName nvarchar(100),EntityId nvarchar(100),OldValue nvarchar(max),NewValue nvarchar(max),CreatedAt datetime2(0));
                """);
            // Apply existing referral-only scripts to disposable databases, never the active tenant.
            foreach(var file in new[] {"0021-patient-referrals-foundation.sql","0023-patient-referral-document-linkage.sql",
                "0056-referral-letter-artifact.sql","0058-referral-followup-response-tracking.sql","0065-referral-clinical-selections.sql"})
            {
                var source=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"database","tenant-clinical","migrations",file));
                foreach(var batch in Regex.Split(source,@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase))
                    if(!string.IsNullOrWhiteSpace(batch)) await Execute(batch);
            }
        }
        public async Task Seed(Guid patient,Guid provider,Guid document)
        {
            await Execute("INSERT dbo.Patient(PatientUid) VALUES(@Uid)",patient);
            await Execute("INSERT dbo.Provider(ProviderUid,DisplayName,ProviderType) VALUES(@Uid,N'Referrer',N'Physician')",provider);
            await using var sql=await OpenConnectionAsync(); await using var command=sql.CreateCommand();
            command.CommandText="INSERT dbo.PatientDocument(PatientDocumentUid,PatientUid,DocumentTitle,DocumentType,DocumentStatus,CreatedBy) VALUES(@Document,@Patient,N'Supporting report',N'Report',N'Final',7)";
            command.Parameters.Add("@Document",SqlDbType.UniqueIdentifier).Value=document;
            command.Parameters.Add("@Patient",SqlDbType.UniqueIdentifier).Value=patient; await command.ExecuteNonQueryAsync();
        }
        public async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken=default)
        {
            var sql=new SqlConnection(connection.ConnectionString);
            try {await sql.OpenAsync(cancellationToken);return sql;} catch {await sql.DisposeAsync();throw;}
        }
        public async Task Execute(string query,Guid? uid=null)
        {
            await using var sql=await OpenConnectionAsync();await using var command=sql.CreateCommand();command.CommandText=query;
            if(uid.HasValue)command.Parameters.Add("@Uid",SqlDbType.UniqueIdentifier).Value=uid.Value;
            await command.ExecuteNonQueryAsync();
        }
        public async Task<int> Scalar(string query)
        {
            await using var sql=await OpenConnectionAsync();await using var command=sql.CreateCommand();command.CommandText=query;
            return (int)(await command.ExecuteScalarAsync())!;
        }
        public async ValueTask DisposeAsync()
        {
            if(!created)return;
            if(!Regex.IsMatch(name,@"^MicroEMR_ReferralFinalizationTest_[a-f0-9]{32}$"))throw new InvalidOperationException("Invalid test database name.");
            SqlConnection.ClearAllPools();connection.InitialCatalog="master";
            await using var sql=new SqlConnection(connection.ConnectionString);await sql.OpenAsync();await using var command=sql.CreateCommand();
            command.CommandText=$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await command.ExecuteNonQueryAsync();
        }
    }
}
