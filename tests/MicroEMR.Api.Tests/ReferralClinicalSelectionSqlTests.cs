using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PatientReferrals;
using MicroEMR.Infrastructure.PatientReferrals;
using MicroEMR.Infrastructure.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ReferralClinicalSelectionSqlTests
{
    [SelectionSqlFact]
    public async Task MigrationAndRepositoryEnforceReferencePersistenceOwnershipDraftConcurrencyAndAudit()
    {
        await using var local = new SelectionDatabase();
        await local.Initialize();
        await using var otherTenant = new SelectionDatabase();
        await otherTenant.Initialize();
        var patient = Guid.NewGuid(); var otherPatient = Guid.NewGuid(); var referral = Guid.NewGuid();
        var encounter = Guid.NewGuid(); var result = Guid.NewGuid(); var foreignEncounter = Guid.NewGuid(); var foreignResult = Guid.NewGuid();
        await local.Seed(patient, referral, encounter, result);
        await local.Seed(otherPatient, Guid.NewGuid(), foreignEncounter, foreignResult);
        var remoteEncounter = Guid.NewGuid(); var remoteResult = Guid.NewGuid();
        await otherTenant.Seed(patient, Guid.NewGuid(), remoteEncounter, remoteResult);
        var repository = local.Repository;
        var original = (await repository.GetClinicalSelectionsAsync(patient,referral))!;
        Assert.Empty(original.Selections);
        async Task<PatientReferralClinicalSelectionsResponse> Replace(string version, params ReferralClinicalSelectionInput[] items) =>
            (await repository.ReplaceDraftClinicalSelectionsAsync(patient,referral,new() { RowVersion=version,Selections=items },7))!;
        var first = await Replace(original.RowVersion,new ReferralClinicalSelectionInput("CPP","PROBLEMS"));
        Assert.Single(first.Selections); Assert.NotEqual(original.RowVersion,first.RowVersion);
        var reopened = (await repository.GetClinicalSelectionsAsync(patient,referral))!;
        Assert.Equal(first, reopened with { Selections=first.Selections });
        Assert.Equal(first.Selections[0].SelectionUid,reopened.Selections[0].SelectionUid);
        var all = await Replace(first.RowVersion,new ReferralClinicalSelectionInput("CPP","PROBLEMS"),new("CPP","ALLERGIES"),new("CPP","MEDICATIONS"),
            new("ENCOUNTER",EncounterUid:encounter),new("RESULT",ResultUid:result));
        Assert.Equal(5,all.Selections.Count);
        Assert.Equal(first.Selections[0].SelectionUid,all.Selections.Single(x=>x.CppCategoryCode=="PROBLEMS").SelectionUid);
        var unchanged = await Replace(all.RowVersion,new ReferralClinicalSelectionInput("CPP","PROBLEMS"),new("CPP","ALLERGIES"),new("CPP","MEDICATIONS"),
            new("ENCOUNTER",EncounterUid:encounter),new("RESULT",ResultUid:result));
        Assert.NotEqual(all.RowVersion,unchanged.RowVersion);
        Assert.Equal(all.Selections.Select(x=>x.SelectionUid),unchanged.Selections.Select(x=>x.SelectionUid));
        var reduced = await Replace(unchanged.RowVersion,new ReferralClinicalSelectionInput("CPP","PROBLEMS"));
        Assert.Single(reduced.Selections);
        Assert.Equal(4,await local.Scalar<int>("SELECT COUNT(*) FROM dbo.PatientReferralClinicalSelection WHERE ReferralUid=@Uid AND IsDeleted=1",referral));
        var cleared = await Replace(reduced.RowVersion);
        Assert.Empty(cleared.Selections);
        Assert.Equal(5,await local.Scalar<int>("SELECT COUNT(*) FROM dbo.PatientReferralClinicalSelection WHERE ReferralUid=@Uid AND IsDeleted=1",referral));
        Assert.Equal(1,await local.Scalar<int>("SELECT COUNT(*) FROM dbo.PatientReferralDocument WHERE ReferralUid=@Uid",referral));
        Assert.Equal(5,await local.Scalar<int>("SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@Uid) AND ActionName=N'ReferralClinicalSelectionsReplaced' AND UserId=7",referral));
        var lastAudit = await local.Scalar<string>("SELECT TOP(1) NewValue FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@Uid) ORDER BY AuditLogId DESC",referral);
        Assert.Equal("[]",lastAudit);

        await Assert.ThrowsAsync<PatientReferralConcurrencyException>(()=>Replace(original.RowVersion));
        foreach (var duplicate in new[] { new ReferralClinicalSelectionInput("CPP","PROBLEMS"),
            new ReferralClinicalSelectionInput("ENCOUNTER",EncounterUid:encounter),new ReferralClinicalSelectionInput("RESULT",ResultUid:result) })
            await Assert.ThrowsAsync<ReferralClinicalSelectionRuleException>(()=>Replace(cleared.RowVersion,duplicate,duplicate));
        foreach (var invalid in new[] { new ReferralClinicalSelectionInput("CPP","UNSUPPORTED"),
            new ReferralClinicalSelectionInput("CPP","PROBLEMS "),new ReferralClinicalSelectionInput("DOCUMENT"),
            new ReferralClinicalSelectionInput("CPP","PROBLEMS",encounter),new ReferralClinicalSelectionInput("RESULT",ResultUid:Guid.Empty),
            new ReferralClinicalSelectionInput("ENCOUNTER",EncounterUid:foreignEncounter),new ReferralClinicalSelectionInput("RESULT",ResultUid:foreignResult),
            new ReferralClinicalSelectionInput("ENCOUNTER",EncounterUid:remoteEncounter),new ReferralClinicalSelectionInput("RESULT",ResultUid:remoteResult) })
            await Assert.ThrowsAsync<ReferralClinicalSelectionRuleException>(()=>Replace(cleared.RowVersion,invalid));
        Assert.Null(await repository.GetClinicalSelectionsAsync(otherPatient,referral));
        Assert.Null(await repository.ReplaceDraftClinicalSelectionsAsync(otherPatient,referral,new() { RowVersion=cleared.RowVersion },7));
        await Assert.ThrowsAsync<ClinicalUserResolutionException>(()=>repository.ReplaceDraftClinicalSelectionsAsync(patient,referral,new() { RowVersion=cleared.RowVersion },8));
        Assert.Equal(cleared.RowVersion,(await repository.GetClinicalSelectionsAsync(patient,referral))!.RowVersion);
        Assert.Equal(5,await local.Scalar<int>("SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@Uid)",referral));

        await local.Execute("UPDATE dbo.PatientResult SET LifecycleStatus=N'EnteredInError' WHERE PatientResultUid=@Uid",result);
        await Assert.ThrowsAsync<ReferralClinicalSelectionRuleException>(()=>Replace(cleared.RowVersion,new ReferralClinicalSelectionInput("RESULT",ResultUid:result)));
        var constraint = await Assert.ThrowsAsync<SqlException>(()=>local.Execute(
            "INSERT dbo.PatientReferralClinicalSelection(ReferralUid,SelectionKind,CppCategoryCode,EncounterUid,CreatedBy) SELECT @Uid,N'CPP',N'PROBLEMS',e.EncounterUid,7 FROM dbo.PatientEncounter e JOIN dbo.PatientReferral r ON r.PatientUid=e.PatientUid WHERE r.ReferralUid=@Uid",referral));
        Assert.Equal(547,constraint.Number);
        var selected = await Replace(cleared.RowVersion,new ReferralClinicalSelectionInput("CPP","PROBLEMS"));
        var unique = await Assert.ThrowsAsync<SqlException>(()=>local.Execute(
            "INSERT dbo.PatientReferralClinicalSelection(ReferralUid,SelectionKind,CppCategoryCode,CreatedBy) VALUES(@Uid,N'CPP',N'PROBLEMS',7)",referral));
        Assert.Contains(unique.Number,new[] {2601,2627});
        foreach (var status in new[] {"Sent","ResponseReceived","Closed"})
        {
            await local.Execute($"UPDATE dbo.PatientReferral SET Status=N'{status}' WHERE ReferralUid=@Uid",referral);
            var version = (await repository.GetClinicalSelectionsAsync(patient,referral))!.RowVersion;
            await Assert.ThrowsAsync<PatientReferralTransitionException>(()=>Replace(version));
        }
        Assert.Single((await repository.GetClinicalSelectionsAsync(patient,referral))!.Selections);
    }

    public sealed class SelectionSqlFactAttribute : FactAttribute
    {
        public SelectionSqlFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MICROEMR_REFERRAL_SELECTION_TEST_CONNECTION")))
                Skip="Set MICROEMR_REFERRAL_SELECTION_TEST_CONNECTION to a SQL Server connection permitted to create disposable test databases.";
        }
    }

    private sealed class SelectionDatabase : ITenantSqlConnectionFactory, IAsyncDisposable
    {
        private readonly string name = "MicroEMR_ReferralSelectionTest_"+Guid.NewGuid().ToString("N");
        private readonly SqlConnectionStringBuilder connection = new(Environment.GetEnvironmentVariable("MICROEMR_REFERRAL_SELECTION_TEST_CONNECTION")!);
        private bool created;
        public PatientReferralRepository Repository => new(this,NullLogger<PatientReferralRepository>.Instance);

        public async Task Initialize()
        {
            connection.InitialCatalog="master";
            await using(var master=new SqlConnection(connection.ConnectionString))
            {
                await master.OpenAsync(); await using var command=master.CreateCommand();
                command.CommandText=$"CREATE DATABASE [{name}]"; await command.ExecuteNonQueryAsync(); created=true;
            }
            connection.InitialCatalog=name;
            await Execute("""
                CREATE TABLE dbo.ApplicationUser(UserId bigint PRIMARY KEY,IsActive bit NOT NULL);
                INSERT dbo.ApplicationUser VALUES(7,1),(8,0);
                CREATE TABLE dbo.Patient(PatientId bigint IDENTITY PRIMARY KEY,PatientUid uniqueidentifier UNIQUE NOT NULL,IsDeleted bit NOT NULL DEFAULT 0);
                CREATE TABLE dbo.PatientReferral(PatientReferralId bigint IDENTITY PRIMARY KEY,ReferralUid uniqueidentifier UNIQUE NOT NULL,
                    PatientUid uniqueidentifier NOT NULL REFERENCES dbo.Patient(PatientUid),Status nvarchar(30) NOT NULL,
                    UpdatedBy bigint NULL,UpdatedAt datetime2(0) NULL,RowVersion rowversion);
                CREATE TABLE dbo.PatientEncounter(EncounterUid uniqueidentifier PRIMARY KEY,PatientUid uniqueidentifier NOT NULL REFERENCES dbo.Patient(PatientUid),Status nvarchar(30) NOT NULL);
                CREATE TABLE dbo.PatientResult(PatientResultUid uniqueidentifier PRIMARY KEY,PatientUid uniqueidentifier NOT NULL REFERENCES dbo.Patient(PatientUid),LifecycleStatus nvarchar(20) NOT NULL);
                CREATE TABLE dbo.PatientReferralDocument(ReferralUid uniqueidentifier NOT NULL,DocumentUid uniqueidentifier NOT NULL,PRIMARY KEY(ReferralUid,DocumentUid));
                CREATE TABLE dbo.AuditLog(AuditLogId bigint IDENTITY PRIMARY KEY,UserId bigint,PatientId bigint,ActionName nvarchar(100),EntityName nvarchar(100),EntityId nvarchar(100),OldValue nvarchar(max),NewValue nvarchar(max),CreatedAt datetime2(0));
                """);
            var migration=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"database","tenant-clinical","migrations","0065-referral-clinical-selections.sql"));
            foreach(var batch in Regex.Split(migration,@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase))
                if(!string.IsNullOrWhiteSpace(batch)) await Execute(batch);
        }

        public async Task Seed(Guid patient,Guid referral,Guid encounter,Guid result)
        {
            await using var sql=await OpenConnectionAsync(); await using var command=sql.CreateCommand();
            command.CommandText="""
                INSERT dbo.Patient(PatientUid) VALUES(@Patient);
                INSERT dbo.PatientReferral(ReferralUid,PatientUid,Status) VALUES(@Referral,@Patient,N'Draft');
                INSERT dbo.PatientEncounter VALUES(@Encounter,@Patient,N'Signed');
                INSERT dbo.PatientResult VALUES(@Result,@Patient,N'Current');
                INSERT dbo.PatientReferralDocument VALUES(@Referral,NEWID());
                """;
            foreach(var parameter in new[] {("@Patient",patient),("@Referral",referral),("@Encounter",encounter),("@Result",result)})
                command.Parameters.Add(parameter.Item1,SqlDbType.UniqueIdentifier).Value=parameter.Item2;
            await command.ExecuteNonQueryAsync();
        }

        public async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken=default)
        {
            var sql=new SqlConnection(connection.ConnectionString);
            try { await sql.OpenAsync(cancellationToken); return sql; }
            catch { await sql.DisposeAsync(); throw; }
        }

        public async Task Execute(string query,Guid? uid=null)
        {
            await using var sql=await OpenConnectionAsync(); await using var command=sql.CreateCommand(); command.CommandText=query;
            if(uid.HasValue) command.Parameters.Add("@Uid",SqlDbType.UniqueIdentifier).Value=uid.Value;
            await command.ExecuteNonQueryAsync();
        }

        public async Task<T> Scalar<T>(string query,Guid uid)
        {
            await using var sql=await OpenConnectionAsync(); await using var command=sql.CreateCommand(); command.CommandText=query;
            command.Parameters.Add("@Uid",SqlDbType.UniqueIdentifier).Value=uid;
            return (T)(await command.ExecuteScalarAsync())!;
        }

        public async ValueTask DisposeAsync()
        {
            if(!created) return;
            // Only names generated by this fixture are eligible for cleanup.
            if(!Regex.IsMatch(name,@"^MicroEMR_ReferralSelectionTest_[a-f0-9]{32}$")) throw new InvalidOperationException("Invalid test database name.");
            SqlConnection.ClearAllPools(); connection.InitialCatalog="master";
            await using var sql=new SqlConnection(connection.ConnectionString); await sql.OpenAsync();
            await using var command=sql.CreateCommand();
            command.CommandText=$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await command.ExecuteNonQueryAsync();
        }
    }
}
