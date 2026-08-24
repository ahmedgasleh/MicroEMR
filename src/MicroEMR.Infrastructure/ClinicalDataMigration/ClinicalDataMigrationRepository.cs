using System.Data;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.ClinicalDataMigration;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.ClinicalDataMigration;

public sealed class ClinicalDataMigrationRepository(ITenantSqlConnectionFactory connections) : IClinicalDataMigrationRepository
{
    public async Task<ClinicalMigrationBatchStart> BeginValidationAsync(ClinicalMigrationPackageV1 package,string fingerprint,long actor,CancellationToken token=default)
    {
        await using var connection=await connections.OpenConnectionAsync(token);await using var command=Command(connection,"dbo.ClinicalDataMigration_BeginValidation");
        var uid=Guid.NewGuid();Add(command,"@MigrationBatchUid",SqlDbType.UniqueIdentifier,uid);Str(command,"@SourceSystem",100,package.SourceSystem);Str(command,"@SourceSystemVersion",100,package.SourceSystemVersion);Add(command,"@PackageUid",SqlDbType.UniqueIdentifier,package.PackageUid);Str(command,"@PackageSchemaVersion",50,package.PackageSchemaVersion);Str(command,"@PackageFingerprint",64,fingerprint,SqlDbType.Char);Add(command,"@RequestedBy",SqlDbType.BigInt,actor);
        await using var reader=await command.ExecuteReaderAsync(token);await reader.ReadAsync(token);return new(reader.GetGuid(0),reader.GetBoolean(1),reader.GetString(2));
    }

    public async Task<PatientMatchCandidate> FindPatientMatchAsync(string sourceSystem,string sourcePatientId,string? healthCardNumber,string firstName,string lastName,DateOnly? dateOfBirth,CancellationToken token=default)
    {
        await using var connection=await connections.OpenConnectionAsync(token);await using var command=Command(connection,"dbo.ClinicalDataMigration_FindPatientMatch");
        Str(command,"@SourceSystem",100,sourceSystem);Str(command,"@SourcePatientId",200,sourcePatientId);Str(command,"@HealthCardNumber",50,healthCardNumber);Str(command,"@FirstName",100,firstName);Str(command,"@LastName",100,lastName);Date(command,"@DateOfBirth",dateOfBirth);
        await using var reader=await command.ExecuteReaderAsync(token);await reader.ReadAsync(token);return new(reader.IsDBNull(0)?null:reader.GetGuid(0),reader.GetInt32(1),reader.GetInt32(2));
    }

    public async Task StagePatientAsync(Guid batchUid,string sourceSystem,StagedMigrationPatient p,CancellationToken token=default)
    {
        await using var c=await connections.OpenConnectionAsync(token);await using var x=Command(c,"dbo.ClinicalDataMigration_StagePatient");
        Add(x,"@MigrationBatchUid",SqlDbType.UniqueIdentifier,batchUid);Str(x,"@SourceSystem",100,sourceSystem);Str(x,"@SourceObjectId",200,p.SourceObjectId);Str(x,"@SourcePatientId",200,p.SourcePatientId);Str(x,"@ChartNumber",50,p.ChartNumber);Str(x,"@HealthCardNumber",50,p.HealthCardNumber);Str(x,"@HealthCardVersion",10,p.HealthCardVersion);Str(x,"@FirstName",100,p.FirstName);Str(x,"@MiddleName",100,p.MiddleName);Str(x,"@LastName",100,p.LastName);Date(x,"@DateOfBirth",p.DateOfBirth);Str(x,"@SexAtBirth",20,p.SexAtBirth);Str(x,"@GenderIdentity",50,p.GenderIdentity);Str(x,"@PreferredName",100,p.PreferredName);Str(x,"@PhoneNumber",30,p.PhoneNumber);Str(x,"@AlternatePhoneNumber",30,p.AlternatePhoneNumber);Str(x,"@Email",255,p.Email);Str(x,"@AddressLine1",255,p.AddressLine1);Str(x,"@AddressLine2",255,p.AddressLine2);Str(x,"@City",100,p.City);Str(x,"@Province",50,p.Province);Str(x,"@PostalCode",20,p.PostalCode);Str(x,"@CountryCode",2,p.CountryCode);Offset(x,"@SourceCreatedAt",p.SourceCreatedAt);Offset(x,"@SourceUpdatedAt",p.SourceUpdatedAt);Str(x,"@SourceAuthor",200,p.SourceAuthor);Str(x,"@MappingStatus",30,p.MappingStatus);Add(x,"@TargetPatientUid",SqlDbType.UniqueIdentifier,p.TargetPatientUid);Str(x,"@ValidationState",20,p.ValidationState);Add(x,"@ErrorCount",SqlDbType.Int,p.ErrorCount);Add(x,"@WarningCount",SqlDbType.Int,p.WarningCount);await x.ExecuteNonQueryAsync(token);
    }

    public async Task StageProblemAsync(Guid batchUid,string sourceSystem,StagedMigrationProblem p,CancellationToken token=default)
    {
        await using var c=await connections.OpenConnectionAsync(token);await using var x=Command(c,"dbo.ClinicalDataMigration_StageProblem");
        Add(x,"@MigrationBatchUid",SqlDbType.UniqueIdentifier,batchUid);Str(x,"@SourceSystem",100,sourceSystem);Str(x,"@SourceObjectId",200,p.SourceObjectId);Str(x,"@SourcePatientId",200,p.SourcePatientId);Str(x,"@ProblemName",200,p.ProblemName);Str(x,"@ProblemDescription",1000,p.ProblemDescription);Date(x,"@OnsetDate",p.OnsetDate);Str(x,"@ProblemStatus",20,p.Status);Date(x,"@ResolvedDate",p.ResolvedDate);Offset(x,"@SourceCreatedAt",p.SourceCreatedAt);Offset(x,"@SourceUpdatedAt",p.SourceUpdatedAt);Str(x,"@SourceAuthor",200,p.SourceAuthor);Str(x,"@ValidationState",20,p.ValidationState);Add(x,"@ErrorCount",SqlDbType.Int,p.ErrorCount);Add(x,"@WarningCount",SqlDbType.Int,p.WarningCount);await x.ExecuteNonQueryAsync(token);
    }

    public async Task AddIssueAsync(Guid batchUid,ClinicalMigrationIssue issue,CancellationToken token=default)
    {await using var c=await connections.OpenConnectionAsync(token);await using var x=Command(c,"dbo.ClinicalDataMigration_AddIssue");Add(x,"@MigrationBatchUid",SqlDbType.UniqueIdentifier,batchUid);Str(x,"@Code",100,issue.Code);Str(x,"@Severity",20,issue.Severity);Str(x,"@RecordType",30,issue.RecordType);Str(x,"@SourceObjectId",200,issue.SourceObjectId);Str(x,"@Message",500,issue.Message);await x.ExecuteNonQueryAsync(token);}

    public async Task CompleteValidationAsync(Guid batchUid,long actor,CancellationToken token=default)
    {await using var c=await connections.OpenConnectionAsync(token);await using var x=Command(c,"dbo.ClinicalDataMigration_CompleteValidation");Add(x,"@MigrationBatchUid",SqlDbType.UniqueIdentifier,batchUid);Add(x,"@RequestedBy",SqlDbType.BigInt,actor);await x.ExecuteNonQueryAsync(token);}

    public async Task<ClinicalMigrationValidationReport?> GetReportAsync(Guid batchUid,bool reused=false,CancellationToken token=default)
    {
        await using var c=await connections.OpenConnectionAsync(token);await using var x=Command(c,"dbo.ClinicalDataMigration_GetBatch");Add(x,"@MigrationBatchUid",SqlDbType.UniqueIdentifier,batchUid);await using var r=await x.ExecuteReaderAsync(token);if(!await r.ReadAsync(token))return null;
        var uid=r.GetGuid(0);var source=r.GetString(1);var package=r.GetGuid(2);var fingerprint=r.GetString(3);var status=r.GetString(4);var total=r.GetInt32(5);var valid=r.GetInt32(6);var warnings=r.GetInt32(7);var failed=r.GetInt32(8);var counts=new List<ClinicalMigrationRecordTypeCount>();var summary=new Dictionary<string,int>(StringComparer.Ordinal);
        await r.NextResultAsync(token);while(await r.ReadAsync(token))counts.Add(new(r.GetString(0),r.GetInt32(1),r.IsDBNull(2)?0:r.GetInt32(2),r.IsDBNull(3)?0:r.GetInt32(3),r.IsDBNull(4)?0:r.GetInt32(4)));
        await r.NextResultAsync(token);while(await r.ReadAsync(token))summary[r.GetString(0)]=r.GetInt32(1);
        return new(uid,source,package,fingerprint,status,total,valid,warnings,failed,counts,summary,reused);
    }

    public async Task<IReadOnlyList<ClinicalMigrationIssue>> ListIssuesAsync(Guid batchUid,int skip,int take,CancellationToken token=default)
    {var result=new List<ClinicalMigrationIssue>();await using var c=await connections.OpenConnectionAsync(token);await using var x=Command(c,"dbo.ClinicalDataMigration_ListIssues");Add(x,"@MigrationBatchUid",SqlDbType.UniqueIdentifier,batchUid);Add(x,"@Skip",SqlDbType.Int,skip);Add(x,"@Take",SqlDbType.Int,take);await using var r=await x.ExecuteReaderAsync(token);while(await r.ReadAsync(token))result.Add(new(r.GetString(0),r.GetString(1),r.GetString(2),r.IsDBNull(3)?null:r.GetString(3),r.GetString(4),new DateTimeOffset(DateTime.SpecifyKind(r.GetDateTime(5),DateTimeKind.Utc))));return result;}

    private static SqlCommand Command(SqlConnection c,string name)=>new(name,c){CommandType=CommandType.StoredProcedure};
    private static void Add(SqlCommand c,string name,SqlDbType type,object? value)=>c.Parameters.Add(name,type).Value=value??DBNull.Value;
    private static void Str(SqlCommand c,string name,int size,string? value,SqlDbType type=SqlDbType.NVarChar)=>c.Parameters.Add(name,type,size).Value=string.IsNullOrWhiteSpace(value)?DBNull.Value:value.Trim();
    private static void Date(SqlCommand c,string name,DateOnly? value)=>c.Parameters.Add(name,SqlDbType.Date).Value=value?.ToDateTime(TimeOnly.MinValue)??(object)DBNull.Value;
    private static void Offset(SqlCommand c,string name,DateTimeOffset? value)=>c.Parameters.Add(name,SqlDbType.DateTimeOffset).Value=value?.ToUniversalTime()??(object)DBNull.Value;
}
