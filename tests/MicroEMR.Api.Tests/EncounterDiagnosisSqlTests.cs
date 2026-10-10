using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.PatientEncounters;
using MicroEMR.Infrastructure.PatientEncounters;
using MicroEMR.Infrastructure.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class EncounterDiagnosisSqlTests(EncounterDiagnosisSqlDatabase db) : IClassFixture<EncounterDiagnosisSqlDatabase>
{
    [DiagnosisSqlFact]
    public async Task MultipleEncounterOnlyDiagnosesReopenWithoutChangingCppOrSoap()
    {
        var (p,e)=await db.Seed();var original=(await db.Repository.GetAsync(p,e))!;
        var saved=(await Save(p,e,original.RowVersion,false,new EncounterDiagnosisInput {Name="Diagnosis A"},new EncounterDiagnosisInput {Name="Diagnosis B"}))!;
        Assert.Equal(2,saved.Diagnoses.Count);Assert.NotEqual(original.RowVersion,saved.RowVersion);
        var reopened=(await db.Repository.GetAsync(p,e))!;Assert.Equal(saved.Diagnoses,reopened.Diagnoses);
        Assert.Equal(0,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.PatientProblem WHERE PatientUid=@p",p));
        Assert.Equal("Preserved SOAP",await db.Scalar<string>("SELECT SubjectiveNote FROM dbo.PatientEncounter WHERE EncounterUid=@p",e));
        Assert.Equal("Open",saved.Status);
        Assert.Equal(1,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@p) AND UserId=7 AND ActionName='EncounterDiagnosesSaved'",e));
        Assert.Equal(1,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.PatientEncounterHistory WHERE EncounterUid=@p AND CreatedBy=7 AND ActionType='DiagnosesSaved'",e));
    }
    [DiagnosisSqlFact]
    public async Task BothDestinationsReuseActiveProblemWithoutOverwritingItsClinicalDataAndAvoidDuplicates()
    {
        var (p,e)=await db.Seed();
        await db.Execute("INSERT dbo.PatientProblem(PatientUid,ProblemName,ProblemDescription,OnsetDate,CreatedBy) VALUES(@p,N'Existing',N'Original details','20000101',7)",p);
        var initial=(await db.Repository.GetAsync(p,e))!;
        var saved=(await Save(p,e,initial.RowVersion,true,new EncounterDiagnosisInput {Name=" existing ",Description="Encounter details"},new EncounterDiagnosisInput {Name="New problem"}))!;
        Assert.All(saved.Diagnoses,x=>Assert.NotNull(x.PatientProblemUid));
        Assert.Equal(2,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.PatientProblem WHERE PatientUid=@p AND ProblemStatus='Active'",p));
        Assert.Equal("Original details",await db.Scalar<string>("SELECT ProblemDescription FROM dbo.PatientProblem WHERE PatientUid=@p AND ProblemName='Existing'",p));
        Assert.Equal(new DateTime(2000,1,1),await db.Scalar<DateTime>("SELECT OnsetDate FROM dbo.PatientProblem WHERE PatientUid=@p AND ProblemName='Existing'",p));
        var second=(await Save(p,e,saved.RowVersion,true,Inputs(saved)))!;
        Assert.Equal(saved.Diagnoses.Select(x=>x.DiagnosisUid),second.Diagnoses.Select(x=>x.DiagnosisUid));
        Assert.Equal(2,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.PatientProblem WHERE PatientUid=@p",p));
        Assert.Equal(1,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.AuditLog WHERE PatientId=(SELECT PatientId FROM dbo.Patient WHERE PatientUid=@p) AND EntityName='PatientProblem' AND ActionName='Create'",p));
        var audit=await db.Scalar<string>("SELECT TOP(1) NewValue FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@p) ORDER BY AuditLogId DESC",e);
        Assert.Contains("PatientProblemUid",audit);Assert.Contains("Encounter details",audit);
    }
    [DiagnosisSqlFact]
    public async Task DraftEditsAndSoftRemovalPreserveAuditBeforeAfterAndCpp()
    {
        var (p,e)=await db.Seed();var original=(await db.Repository.GetAsync(p,e))!;
        var saved=(await Save(p,e,original.RowVersion,true,new EncounterDiagnosisInput {Name="Original"},new EncounterDiagnosisInput {Name="Other"}))!;
        var edited=new EncounterDiagnosisInput {DiagnosisUid=saved.Diagnoses[0].DiagnosisUid,Name="Changed",Description="New description"};
        var next=(await Save(p,e,saved.RowVersion,false,edited))!;
        Assert.Single(next.Diagnoses);Assert.Null(next.Diagnoses[0].PatientProblemUid);
        Assert.Equal(1,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.PatientEncounterDiagnosis WHERE EncounterUid=@p AND IsDeleted=1",e));
        Assert.Equal(2,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.PatientProblem WHERE PatientUid=@p",p));
        var before=await db.Scalar<string>("SELECT TOP(1) OldValue FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@p) ORDER BY AuditLogId DESC",e);
        Assert.Contains("Original",before);Assert.Contains("Other",before);
    }
    [DiagnosisSqlTheory]
    [InlineData("PatientProblem")] [InlineData("PatientEncounterDiagnosis")] [InlineData("PatientEncounter")] [InlineData("AuditLog")]
    public async Task FailureAtEitherDestinationOrAuditRollsBackAllClinicalChanges(string table)
    {
        var (p,e)=await db.Seed();var original=(await db.Repository.GetAsync(p,e))!;
        var operation=table=="PatientEncounter"?"UPDATE":"INSERT";
        var condition=table=="AuditLog"?"IF EXISTS(SELECT 1 FROM inserted WHERE ActionName='EncounterDiagnosesSaved') ":"";
        await db.Execute($"CREATE OR ALTER TRIGGER dbo.Step69Fail ON dbo.{table} AFTER {operation} AS {condition}THROW 51999,'Injected test failure',1;");
        try
        {
            await Assert.ThrowsAsync<SqlException>(()=>Save(p,e,original.RowVersion,true,new EncounterDiagnosisInput {Name="Must roll back"}));
            var reopened=(await db.Repository.GetAsync(p,e))!;Assert.Empty(reopened.Diagnoses);Assert.Equal(original.RowVersion,reopened.RowVersion);
            Assert.Equal(0,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.PatientProblem WHERE PatientUid=@p",p));
            Assert.Equal(0,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@p)",e));
        }
        finally {await db.Execute("DROP TRIGGER dbo.Step69Fail;");}
    }
    [DiagnosisSqlFact]
    public async Task StaleVersionAndSignedEncounterCannotMutateEitherDestination()
    {
        var (p,e)=await db.Seed();var original=(await db.Repository.GetAsync(p,e))!;
        var first=(await Save(p,e,original.RowVersion,false,new EncounterDiagnosisInput {Name="A"}))!;
        await Assert.ThrowsAsync<EncounterDiagnosisConflictException>(()=>Save(p,e,original.RowVersion,true,new EncounterDiagnosisInput {Name="B"}));
        await db.Execute("UPDATE dbo.PatientEncounter SET EncounterStatus='Signed' WHERE EncounterUid=@p",e);
        var signed=(await db.Repository.GetAsync(p,e))!;
        await Assert.ThrowsAsync<EncounterDiagnosisConflictException>(()=>Save(p,e,signed.RowVersion,true,Inputs(first)));
        Assert.Single((await db.Repository.GetAsync(p,e))!.Diagnoses);
        Assert.Equal(0,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.PatientProblem WHERE PatientUid=@p",p));
    }
    [DiagnosisSqlFact]
    public async Task ForeignPatientAndForeignTenantIdentifiersCannotAccessOrReplaceDiagnoses()
    {
        var (p,e)=await db.Seed();var (foreign,foreignEncounter)=await db.Seed();
        var remote=(await db.Repository.GetAsync(foreign,foreignEncounter))!;
        remote=(await Save(foreign,foreignEncounter,remote.RowVersion,false,new EncounterDiagnosisInput {Name="Foreign diagnosis"}))!;
        Assert.Null(await db.Repository.GetAsync(p,foreignEncounter));
        var own=(await db.Repository.GetAsync(p,e))!;
        await Assert.ThrowsAsync<EncounterDiagnosisConflictException>(()=>Save(p,e,own.RowVersion,true,Inputs(remote)));
        var other=new EncounterDiagnosisRepository(new Factory(db.OtherConnection),NullLogger<EncounterDiagnosisRepository>.Instance);
        Assert.Null(await other.GetAsync(p,e));Assert.Null(await other.SaveAsync(p,e,new() {RowVersion=own.RowVersion,SaveToCpp=true,Diagnoses=[new EncounterDiagnosisInput {Name="Forbidden"}]},7));
        Assert.Empty((await db.Repository.GetAsync(p,e))!.Diagnoses);
    }
    [DiagnosisSqlFact]
    public async Task DuplicateNamesRejectedAndResolvedProblemsRemainHistorical()
    {
        var (p,e)=await db.Seed();var original=(await db.Repository.GetAsync(p,e))!;
        await Assert.ThrowsAsync<SqlException>(()=>Save(p,e,original.RowVersion,true,new EncounterDiagnosisInput {Name="Same"},new EncounterDiagnosisInput {Name="same"}));
        await db.Execute("INSERT dbo.PatientProblem(PatientUid,ProblemName,ProblemStatus,CreatedBy) VALUES(@p,N'History',N'Resolved',7)",p);
        var saved=(await Save(p,e,original.RowVersion,true,new EncounterDiagnosisInput {Name="History"}))!;Assert.Single(saved.Diagnoses);
        Assert.Equal(2,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.PatientProblem WHERE PatientUid=@p",p));
        Assert.Equal(1,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.PatientProblem WHERE PatientUid=@p AND ProblemStatus='Resolved'",p));
    }
    private Task<EncounterDiagnosesResponse?> Save(Guid p,Guid e,string version,bool cpp,params EncounterDiagnosisInput[] items)
        =>db.Repository.SaveAsync(p,e,new() {RowVersion=version,SaveToCpp=cpp,Diagnoses=items},7);
    private static EncounterDiagnosisInput[] Inputs(EncounterDiagnosesResponse response)=>response.Diagnoses.Select(x=>new EncounterDiagnosisInput {DiagnosisUid=x.DiagnosisUid,Name=x.Name,Description=x.Description,OnsetDate=x.OnsetDate}).ToArray();
    public sealed class Factory(string connection):ITenantSqlConnectionFactory
    {public async Task<SqlConnection> OpenConnectionAsync(CancellationToken token=default) {var sql=new SqlConnection(connection);await sql.OpenAsync(token);return sql;}}
}

public sealed class DiagnosisSqlFactAttribute : FactAttribute
{
    public DiagnosisSqlFactAttribute()
    {if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MICROEMR_ENCOUNTER_DIAGNOSIS_TEST_CONNECTION")))Skip="Set MICROEMR_ENCOUNTER_DIAGNOSIS_TEST_CONNECTION for isolated Step 69 SQL tests.";}
}
public sealed class DiagnosisSqlTheoryAttribute : TheoryAttribute
{
    public DiagnosisSqlTheoryAttribute()
    {if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MICROEMR_ENCOUNTER_DIAGNOSIS_TEST_CONNECTION")))Skip="Set MICROEMR_ENCOUNTER_DIAGNOSIS_TEST_CONNECTION for isolated Step 69 SQL tests.";}
}

public sealed class EncounterDiagnosisSqlDatabase : IAsyncLifetime
{
    private readonly string name="MicroEMR_Step69_"+Guid.NewGuid().ToString("N");
    private readonly string other="MicroEMR_Step69_"+Guid.NewGuid().ToString("N");
    private readonly string master=Environment.GetEnvironmentVariable("MICROEMR_ENCOUNTER_DIAGNOSIS_TEST_CONNECTION") ?? @"Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true;Connect Timeout=15";
    private string Connection=>new SqlConnectionStringBuilder(master) {InitialCatalog=name}.ConnectionString;
    public string OtherConnection=>new SqlConnectionStringBuilder(master) {InitialCatalog=other}.ConnectionString;
    public EncounterDiagnosisRepository Repository=>new(new EncounterDiagnosisSqlTests.Factory(Connection),NullLogger<EncounterDiagnosisRepository>.Instance);
    public async Task InitializeAsync()
    {
        foreach(var database in new[] {name,other})
        {
            await using(var sql=new SqlConnection(new SqlConnectionStringBuilder(master) {InitialCatalog="master"}.ConnectionString))
            {await sql.OpenAsync();await new SqlCommand($"CREATE DATABASE [{database}]",sql).ExecuteNonQueryAsync();}
            await using var connection=new SqlConnection(new SqlConnectionStringBuilder(master) {InitialCatalog=database}.ConnectionString);await connection.OpenAsync();
            var schema="""
                CREATE TABLE dbo.ApplicationUser(UserId bigint PRIMARY KEY,DisplayName nvarchar(200)); INSERT dbo.ApplicationUser VALUES(7,N'Test clinician');
                CREATE TABLE dbo.Patient(PatientId bigint IDENTITY PRIMARY KEY,PatientUid uniqueidentifier UNIQUE,IsDeleted bit NOT NULL DEFAULT 0);
                CREATE TABLE dbo.PatientEncounter(EncounterUid uniqueidentifier PRIMARY KEY,PatientUid uniqueidentifier,PatientId bigint,EncounterStatus nvarchar(50),
                  SubjectiveNote nvarchar(max),UpdatedAt datetime2,UpdatedBy bigint,RowVersion rowversion);
                CREATE TABLE dbo.AuditLog(AuditLogId bigint IDENTITY PRIMARY KEY,UserId bigint,PatientId bigint,ActionName nvarchar(100),EntityName nvarchar(100),EntityId nvarchar(100),OldValue nvarchar(max),NewValue nvarchar(max),CreatedAt datetime2);
                CREATE TABLE dbo.PatientEncounterHistory(EncounterHistoryUid uniqueidentifier,EncounterUid uniqueidentifier,PatientUid uniqueidentifier,ActionType nvarchar(50),
                  ActionDescription nvarchar(500),OldStatus nvarchar(50),NewStatus nvarchar(50),Reason nvarchar(500),CreatedBy bigint,CreatedAt datetime2 DEFAULT SYSUTCDATETIME());
                """;
            await new SqlCommand(schema,connection).ExecuteNonQueryAsync();
            var root=Root();
            await Batches(connection,File.ReadAllText(Path.Combine(root,"db/patient_problem_stored_procedures.sql")));
            var encounterBatches=Regex.Split(File.ReadAllText(Path.Combine(root,"db/patient_encounter_stored_procedures.sql")),@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase);
            await Batches(connection,encounterBatches.Single(x=>x.TrimStart().StartsWith("CREATE OR ALTER PROCEDURE dbo.PatientEncounterHistory_Create",StringComparison.Ordinal)));
            await Batches(connection,File.ReadAllText(Path.Combine(root,"db/tenant-clinical/migrations/0069-encounter-diagnoses.sql")));
        }
    }
    private static async Task Batches(SqlConnection connection,string text)
    {foreach(var batch in Regex.Split(text,@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase).Where(x=>!string.IsNullOrWhiteSpace(x)))await new SqlCommand(batch,connection).ExecuteNonQueryAsync();}
    private static string Root()
    {var dir=new DirectoryInfo(AppContext.BaseDirectory);while(dir is not null && !File.Exists(Path.Combine(dir.FullName,"AGENTS.md")))dir=dir.Parent;return dir?.FullName ?? throw new InvalidOperationException("Workspace not found.");}
    public async Task<(Guid,Guid)> Seed()
    {var p=Guid.NewGuid();var e=Guid.NewGuid();await using var sql=new SqlConnection(Connection);await sql.OpenAsync();using var command=new SqlCommand("INSERT dbo.Patient(PatientUid) VALUES(@p); INSERT dbo.PatientEncounter(EncounterUid,PatientUid,PatientId,EncounterStatus,SubjectiveNote) VALUES(@e,@p,SCOPE_IDENTITY(),'Open','Preserved SOAP');",sql);command.Parameters.AddWithValue("@p",p);command.Parameters.AddWithValue("@e",e);await command.ExecuteNonQueryAsync();return(p,e);}
    public async Task Execute(string text,Guid? p=null)
    {await using var sql=new SqlConnection(Connection);await sql.OpenAsync();using var cmd=new SqlCommand(text,sql);if(p.HasValue)cmd.Parameters.AddWithValue("@p",p.Value);await cmd.ExecuteNonQueryAsync();}
    public async Task<T> Scalar<T>(string text,Guid p)
    {await using var sql=new SqlConnection(Connection);await sql.OpenAsync();using var cmd=new SqlCommand(text,sql);cmd.Parameters.AddWithValue("@p",p);return (T)(await cmd.ExecuteScalarAsync())!;}
    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        foreach(var database in new[] {name,other})
        {
            if(!Regex.IsMatch(database,@"^MicroEMR_Step69_[a-f0-9]{32}$"))throw new InvalidOperationException("Unsafe test database name.");
            await using var sql=new SqlConnection(new SqlConnectionStringBuilder(master) {InitialCatalog="master"}.ConnectionString);await sql.OpenAsync();
            await new SqlCommand($"IF DB_ID(N'{database}') IS NOT NULL BEGIN ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]; END",sql).ExecuteNonQueryAsync();
        }
    }
}
