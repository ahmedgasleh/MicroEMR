using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.PatientFamilyHistory;
using MicroEMR.Infrastructure.PatientFamilyHistory;
using MicroEMR.Infrastructure.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class FamilyHistorySqlTests(FamilyHistorySqlDatabase db) : IClassFixture<FamilyHistorySqlDatabase>
{
    internal static CreatePatientFamilyHistoryRequest Draft() => new()
    {
        Relationship = "Mother", ConditionDescription = "TEST ONLY - type 2 diabetes", AgeAtOnset = "45 years",
        LifeStageCode = "A", Treatment = "TEST ONLY - diet and oral therapy", Notes = "TEST ONLY - maternal history"
    };
    internal static UpdatePatientFamilyHistoryRequest Edit(PatientFamilyHistoryResponse row) => new()
    {
        Relationship = row.Relationship, ConditionDescription = row.ConditionDescription, AgeAtOnset = row.AgeAtOnset,
        LifeStageCode = row.LifeStageCode, Treatment = row.Treatment, Notes = row.Notes, RowVersion = row.RowVersion
    };

    [Fact]
    public async Task SixFieldsPersistWithTrustedActorAndCorrelatedAudit()
    {
        var patient = await db.Seed(); var row = await db.Repository.CreateAsync(patient, Draft(), 7);
        var saved = await db.Repository.GetAsync(patient, row.FamilyHistoryUid);
        Assert.Equal(row, saved); Assert.Equal("Adult", saved!.LifeStage); Assert.Equal(7, saved.CreatedBy);
        Assert.Single(await db.Repository.ListAsync(patient, "Active"));
        Assert.Equal(1, await db.Audits(row.FamilyHistoryUid, "Create"));
        var json = await db.Read<string>("SELECT NewValue FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@Uid)", row.FamilyHistoryUid);
        Assert.Contains("\"LifeStageCode\":\"A\"", json); Assert.Contains("TEST ONLY - maternal history", json);
    }

    [Fact]
    public async Task OptionalUnknownValuesRemainNullAndEmptyListIsNotNegative()
    {
        var patient = await db.Seed(); Assert.Empty(await db.Repository.ListAsync(patient, "Active"));
        var row = await db.Repository.CreateAsync(patient, new() { Relationship = " Father ", ConditionDescription = "Test hypertension", AgeAtOnset = " ", LifeStageCode = "", Notes = " " }, 7);
        Assert.Equal("Father", row.Relationship); Assert.Null(row.AgeAtOnset); Assert.Null(row.LifeStageCode);
        Assert.Null(row.Treatment); Assert.Null(row.Notes); Assert.Equal("Not recorded", row.LifeStage);
    }

    [Fact]
    public async Task UpdatePreservesCreationAndRejectsStaleSaveAndArchive()
    {
        var patient = await db.Seed(); var row = await db.Repository.CreateAsync(patient, Draft(), 7);
        var edit = Edit(row); edit.Notes = "Changed notes";
        var saved = (await db.Repository.UpdateAsync(patient, row.FamilyHistoryUid, edit, 8))!;
        Assert.Equal(row.CreatedAt, saved.CreatedAt); Assert.Equal(7, saved.CreatedBy); Assert.Equal(8, saved.UpdatedBy);
        Assert.NotEqual(row.RowVersion, saved.RowVersion); Assert.Equal("Changed notes", saved.Notes);
        await Assert.ThrowsAsync<PatientFamilyHistoryConcurrencyException>(() => db.Repository.UpdateAsync(patient, row.FamilyHistoryUid, edit, 7));
        await Assert.ThrowsAsync<PatientFamilyHistoryConcurrencyException>(() => db.Repository.ArchiveAsync(patient, row.FamilyHistoryUid, row.RowVersion, 7));
        Assert.Equal(saved, await db.Repository.GetAsync(patient, row.FamilyHistoryUid));
        Assert.Equal(1, await db.Audits(row.FamilyHistoryUid, "Update")); Assert.Equal(0, await db.Audits(row.FamilyHistoryUid, "Archive"));
    }

    [Fact]
    public async Task ArchiveRetainsClinicalFieldsAndForbidsFurtherTransitions()
    {
        var patient = await db.Seed(); var row = await db.Repository.CreateAsync(patient, Draft(), 7);
        var archived = (await db.Repository.ArchiveAsync(patient, row.FamilyHistoryUid, row.RowVersion, 8))!;
        Assert.Empty(await db.Repository.ListAsync(patient, "Active"));
        Assert.Equal(archived, Assert.Single(await db.Repository.ListAsync(patient, "Archived")));
        Assert.Equal(row.Notes, archived.Notes); Assert.Equal(row.ConditionDescription, archived.ConditionDescription);
        Assert.Equal(row.CreatedAt, archived.CreatedAt); Assert.Equal("Archived", archived.Status);
        await Assert.ThrowsAsync<PatientFamilyHistoryArchivedException>(() => db.Repository.UpdateAsync(patient, row.FamilyHistoryUid, Edit(archived), 7));
        await Assert.ThrowsAsync<PatientFamilyHistoryArchivedException>(() => db.Repository.ArchiveAsync(patient, row.FamilyHistoryUid, archived.RowVersion, 7));
        Assert.Equal(1, await db.Audits(row.FamilyHistoryUid, "Archive"));
    }

    [Fact]
    public async Task ForeignPatientAndSeparateTenantCannotReadUpdateOrArchive()
    {
        var patient = await db.Seed(); var otherPatient = await db.Seed(); await db.SeedOther(patient);
        var row = await db.Repository.CreateAsync(patient, Draft(), 7);
        foreach (var (repository, target) in new[] { (db.Repository, otherPatient), (db.OtherRepository, patient) })
        {
            Assert.Null(await repository.GetAsync(target, row.FamilyHistoryUid));
            Assert.Null(await repository.UpdateAsync(target, row.FamilyHistoryUid, Edit(row), 7));
            Assert.Null(await repository.ArchiveAsync(target, row.FamilyHistoryUid, row.RowVersion, 7));
            Assert.Empty(await repository.ListAsync(target, "All"));
        }
        Assert.Equal(row, await db.Repository.GetAsync(patient, row.FamilyHistoryUid));
        Assert.Equal(1, await db.Audits(row.FamilyHistoryUid, "Create")); Assert.Equal(0, await db.Audits(row.FamilyHistoryUid, "Update"));
    }

    [Fact]
    public async Task MissingDeletedPatientAndInactiveActorAreRejected()
    {
        var patient = await db.Seed();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => db.Repository.CreateAsync(patient, Draft(), 9));
        await Assert.ThrowsAsync<PatientFamilyHistoryNotFoundException>(() => db.Repository.CreateAsync(Guid.NewGuid(), Draft(), 7));
        await db.Execute("UPDATE dbo.Patient SET IsDeleted=1 WHERE PatientUid=@Uid", patient);
        await Assert.ThrowsAsync<PatientFamilyHistoryNotFoundException>(() => db.Repository.ListAsync(patient, "All"));
        await Assert.ThrowsAsync<PatientFamilyHistoryNotFoundException>(() => db.Repository.CreateAsync(patient, Draft(), 7));
    }

    [Theory]
    [InlineData("Create")]
    [InlineData("Update")]
    [InlineData("Archive")]
    public async Task AuditFailureRollsBackEveryClinicalMutation(string action)
    {
        var patient = await db.Seed();
        var row = action == "Create" ? null : await db.Repository.CreateAsync(patient, Draft(), 7);
        await db.Execute("CREATE TRIGGER dbo.FamilyAuditFailure ON dbo.AuditLog AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE EntityName=N'PatientFamilyMedicalHistory') THROW 52997,'Synthetic audit failure.',1; END;");
        try
        {
            var error = await Assert.ThrowsAsync<SqlException>(async () =>
            {
                if (action == "Create") await db.Repository.CreateAsync(patient, Draft(), 7);
                else if (action == "Update") await db.Repository.UpdateAsync(patient, row!.FamilyHistoryUid, Edit(row), 8);
                else await db.Repository.ArchiveAsync(patient, row!.FamilyHistoryUid, row.RowVersion, 8);
            });
            Assert.Equal(52997, error.Number);
        }
        finally { await db.Execute("DROP TRIGGER dbo.FamilyAuditFailure;"); }
        if (row is null) Assert.Empty(await db.Repository.ListAsync(patient, "All"));
        else
        {
            Assert.Equal(row, await db.Repository.GetAsync(patient, row.FamilyHistoryUid));
            Assert.Equal(0, await db.Audits(row.FamilyHistoryUid, action));
        }
    }

    [Fact]
    public async Task SqlRejectsUnsupportedLifeStageWithoutAuditOrMutation()
    {
        var patient = await db.Seed(); var request = Draft(); request.LifeStageCode = "X";
        await Assert.ThrowsAsync<ArgumentException>(() => db.Repository.CreateAsync(patient, request, 7));
        Assert.Empty(await db.Repository.ListAsync(patient, "All"));
    }
}

// Existing disposable LocalDB pattern; database names are validated before cleanup, never application databases.
public sealed class FamilyHistorySqlDatabase : IAsyncLifetime
{
    private readonly string primary = "MicroEMR_Step97_" + Guid.NewGuid().ToString("N");
    private readonly string secondary = "MicroEMR_Step97_" + Guid.NewGuid().ToString("N");
    private readonly List<string> created = [];
    private readonly string master = Environment.GetEnvironmentVariable("MICROEMR_FAMILY_HISTORY_TEST_CONNECTION")
        ?? @"Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true;Connect Timeout=15";
    private string Connection(string name) => new SqlConnectionStringBuilder(master) { InitialCatalog = name }.ConnectionString;
    internal string TestConnection(bool other = false) => Connection(other ? secondary : primary);
    public PatientFamilyHistoryRepository Repository => new(new Factory(Connection(primary)), NullLogger<PatientFamilyHistoryRepository>.Instance);
    public PatientFamilyHistoryRepository OtherRepository => new(new Factory(Connection(secondary)), NullLogger<PatientFamilyHistoryRepository>.Instance);
    public async Task InitializeAsync()
    {
        foreach (var name in new[] { primary, secondary })
        {
            await Run("master", $"CREATE DATABASE [{name}]"); created.Add(name);
            await Run(name, """
                CREATE TABLE dbo.Patient(PatientId bigint IDENTITY PRIMARY KEY,PatientUid uniqueidentifier UNIQUE NOT NULL,IsDeleted bit NOT NULL DEFAULT 0);
                CREATE TABLE dbo.ApplicationUser(UserId bigint PRIMARY KEY,IsActive bit NOT NULL,DisplayName nvarchar(200));
                INSERT dbo.ApplicationUser VALUES(7,1,N'Test Writer'),(8,1,N'Other Writer'),(9,0,N'Inactive');
                CREATE TABLE dbo.AuditLog(AuditLogId bigint IDENTITY PRIMARY KEY,UserId bigint,PatientId bigint,
                 ActionName nvarchar(100),EntityName nvarchar(100),EntityId nvarchar(100),OldValue nvarchar(max),NewValue nvarchar(max),CreatedAt datetime2(0));
                """);
            var sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "database", "tenant-clinical", "migrations", "0075-patient-family-medical-history.sql"));
            foreach (var batch in Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                if (!string.IsNullOrWhiteSpace(batch)) await Run(name, batch);
        }
    }
    public async Task<Guid> Seed() { var uid = Guid.NewGuid(); await Execute("INSERT dbo.Patient(PatientUid) VALUES(@Uid)", uid); return uid; }
    public Task SeedOther(Guid uid) => Run(secondary, "INSERT dbo.Patient(PatientUid) VALUES(@Uid)", uid);
    public Task Execute(string sql, Guid? uid = null) => Run(primary, sql, uid);
    public Task<int> Audits(Guid uid, string action) => Read<int>($"SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityName=N'PatientFamilyMedicalHistory' AND EntityId=CONVERT(nvarchar(100),@Uid) AND ActionName=N'{action}'", uid);
    public async Task<T> Read<T>(string sql, Guid uid)
    {
        await using var connection = new SqlConnection(Connection(primary)); await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection); command.Parameters.Add("@Uid", SqlDbType.UniqueIdentifier).Value = uid;
        return (T)(await command.ExecuteScalarAsync())!;
    }
    private async Task Run(string name, string sql, Guid? uid = null)
    {
        await using var connection = new SqlConnection(Connection(name)); await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        if (uid.HasValue) command.Parameters.Add("@Uid", SqlDbType.UniqueIdentifier).Value = uid.Value;
        await command.ExecuteNonQueryAsync();
    }
    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        foreach (var name in created)
        {
            if (!Regex.IsMatch(name, @"^MicroEMR_Step97_[a-f0-9]{32}$")) throw new InvalidOperationException("Unsafe test database name.");
            await Run("master", $"IF DB_ID(N'{name}') IS NOT NULL BEGIN ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]; END");
        }
    }
    private sealed class Factory(string value) : ITenantSqlConnectionFactory
    {
        public async Task<SqlConnection> OpenConnectionAsync(CancellationToken token = default)
        {
            var connection = new SqlConnection(value);
            try { await connection.OpenAsync(token); return connection; }
            catch { await connection.DisposeAsync(); throw; }
        }
    }
}
