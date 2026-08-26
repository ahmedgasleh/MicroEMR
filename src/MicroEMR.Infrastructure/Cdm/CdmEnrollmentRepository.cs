using System.Data;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.Cdm;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.Cdm;

public sealed class CdmEnrollmentRepository(ITenantSqlConnectionFactory connections) : ICdmEnrollmentRepository
{
    public async Task<IReadOnlyList<CdmEnrollmentResponse>> ListAsync(Guid patientUid, CancellationToken token)
    {
        await using var connection=await connections.OpenConnectionAsync(token);
        await using var command=Command(connection,"dbo.ChronicDiseaseEnrollment_ListByPatient"); Add(command,"@PatientUid",patientUid);
        var result=new List<CdmEnrollmentResponse>(); await using var reader=await command.ExecuteReaderAsync(token);
        while(await reader.ReadAsync(token)) result.Add(Map(reader)); return result;
    }
    public async Task<CdmEnrollmentResponse?> GetAsync(Guid patientUid, Guid enrollmentUid, CancellationToken token)
    {
        await using var connection=await connections.OpenConnectionAsync(token);
        await using var command=Command(connection,"dbo.ChronicDiseaseEnrollment_GetByUid"); Add(command,"@PatientUid",patientUid); Add(command,"@ChronicDiseaseEnrollmentUid",enrollmentUid);
        await using var reader=await command.ExecuteReaderAsync(token); return await reader.ReadAsync(token)?Map(reader):null;
    }
    public async Task<CdmEnrollmentResponse> CreateAsync(Guid patientUid, Guid problemUid, CdmProgramMetadata program, long actor, CancellationToken token)
    {
        try
        {
            await using var connection=await connections.OpenConnectionAsync(token); await using var command=Command(connection,"dbo.ChronicDiseaseEnrollment_Create");
            Add(command,"@PatientUid",patientUid); Add(command,"@PatientProblemUid",problemUid); Str(command,"@ProgramKey",100,program.ProgramKey); Num(command,"@ProgramVersion",program.ProgramVersion); Str(command,"@ProgramName",200,program.Name); Big(command,"@EnrolledBy",actor);
            await using var reader=await command.ExecuteReaderAsync(token); if(!await reader.ReadAsync(token)) throw new InvalidOperationException("Enrollment was not returned."); return Map(reader);
        }
        catch(SqlException e) when(e.Number is 51530 or 51531) { throw new CdmEnrollmentValidationException(e.Message); }
        catch(SqlException e) when(e.Number is 51532 or 2601 or 2627) { throw new CdmEnrollmentConflictException("The patient already has an active enrollment for this program."); }
    }
    public async Task<CdmEnrollmentResponse?> InactivateAsync(Guid patientUid, Guid enrollmentUid, byte[] rowVersion, string? reason, long actor, CancellationToken token)
    {
        try
        {
            await using var connection=await connections.OpenConnectionAsync(token); await using var command=Command(connection,"dbo.ChronicDiseaseEnrollment_Inactivate");
            Add(command,"@PatientUid",patientUid); Add(command,"@ChronicDiseaseEnrollmentUid",enrollmentUid); command.Parameters.Add("@RowVersion",SqlDbType.Binary,8).Value=rowVersion; Str(command,"@InactivationReason",500,reason); Big(command,"@InactivatedBy",actor);
            await using var reader=await command.ExecuteReaderAsync(token); return await reader.ReadAsync(token)?Map(reader):null;
        }
        catch(SqlException e) when(e.Number==51533) { throw new CdmEnrollmentConflictException("Enrollment is already inactive."); }
        catch(SqlException e) when(e.Number==51534) { throw new CdmEnrollmentConcurrencyException("Enrollment changed; reload and try again."); }
    }
    private static SqlCommand Command(SqlConnection c,string name)=>new(name,c){CommandType=CommandType.StoredProcedure};
    private static void Add(SqlCommand c,string n,Guid v)=>c.Parameters.Add(n,SqlDbType.UniqueIdentifier).Value=v;
    private static void Num(SqlCommand c,string n,int v)=>c.Parameters.Add(n,SqlDbType.Int).Value=v;
    private static void Big(SqlCommand c,string n,long v)=>c.Parameters.Add(n,SqlDbType.BigInt).Value=v;
    private static void Str(SqlCommand c,string n,int size,string? v)=>c.Parameters.Add(n,SqlDbType.NVarChar,size).Value=(object?)v??DBNull.Value;
    private static CdmEnrollmentResponse Map(SqlDataReader r)=>new(){ChronicDiseaseEnrollmentUid=r.GetGuid(r.GetOrdinal("ChronicDiseaseEnrollmentUid")),PatientUid=r.GetGuid(r.GetOrdinal("PatientUid")),PatientProblemUid=r.GetGuid(r.GetOrdinal("PatientProblemUid")),ProblemName=r.GetString(r.GetOrdinal("ProblemName")),ProgramKey=r.GetString(r.GetOrdinal("ProgramKey")),ProgramVersion=r.GetInt32(r.GetOrdinal("ProgramVersion")),ProgramName=r.GetString(r.GetOrdinal("ProgramName")),Status=r.GetString(r.GetOrdinal("Status")),EnrolledBy=r.GetInt64(r.GetOrdinal("EnrolledBy")),EnrolledByDisplayName=S(r,"EnrolledByDisplayName"),EnrolledAtUtc=r.GetDateTime(r.GetOrdinal("EnrolledAtUtc")),InactivatedBy=L(r,"InactivatedBy"),InactivatedAtUtc=D(r,"InactivatedAtUtc"),InactivationReason=S(r,"InactivationReason"),RowVersion=Convert.ToBase64String((byte[])r["RowVersion"])};
    private static string? S(SqlDataReader r,string n)=>r.IsDBNull(r.GetOrdinal(n))?null:r.GetString(r.GetOrdinal(n));
    private static long? L(SqlDataReader r,string n)=>r.IsDBNull(r.GetOrdinal(n))?null:r.GetInt64(r.GetOrdinal(n));
    private static DateTime? D(SqlDataReader r,string n)=>r.IsDBNull(r.GetOrdinal(n))?null:r.GetDateTime(r.GetOrdinal(n));
}
