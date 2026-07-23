using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.PatientVitals.Contracts;
using MicroEMR.Application.PatientVitals.Repositories;
namespace MicroEMR.Infrastructure.PatientVitals;
public sealed class PatientVitalRepository(IConfiguration configuration, ILogger<PatientVitalRepository> logger) : IPatientVitalRepository
{
    private readonly string _connectionString = configuration.GetConnectionString("MicroEmrDatabase") ?? throw new InvalidOperationException("Connection string 'MicroEmrDatabase' was not found.");
    public async Task<IReadOnlyList<PatientVitalResponse>> GetByPatientUidAsync(Guid patientUid, CancellationToken cancellationToken = default)
    {
        var results = new List<PatientVitalResponse>();
        await using var connection = new SqlConnection(_connectionString);
        await using var command = Command(connection, "dbo.PatientVital_GetByPatientUid");
        command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) results.Add(Map(reader));
        return results;
    }
    public async Task<PatientVitalResponse?> GetByUidAsync(Guid patientUid, Guid patientVitalUid, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = Command(connection, "dbo.PatientVital_GetByUid");
        AddIds(command, patientUid, patientVitalUid);
        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }
    public Task<PatientVitalResponse?> CreateAsync(Guid patientUid, CreatePatientVitalRequest request, long? createdBy, CancellationToken cancellationToken = default) => SaveAsync("dbo.PatientVital_Create", patientUid, null, request, createdBy, cancellationToken);
    public Task<PatientVitalResponse?> UpdateAsync(Guid patientUid, Guid patientVitalUid, UpdatePatientVitalRequest request, long? updatedBy, CancellationToken cancellationToken = default) => SaveAsync("dbo.PatientVital_Update", patientUid, patientVitalUid, request, updatedBy, cancellationToken);
    private async Task<PatientVitalResponse?> SaveAsync(string procedure, Guid patientUid, Guid? vitalUid, CreatePatientVitalRequest request, long? userId, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = Command(connection, procedure);
        command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
        if (vitalUid.HasValue) command.Parameters.Add("@PatientVitalUid", SqlDbType.UniqueIdentifier).Value = vitalUid.Value;
        command.Parameters.Add("@RecordedAt", SqlDbType.DateTime2).Value = request.RecordedAt;
        AddNullable(command,"@BloodPressureSystolic",SqlDbType.Int,request.BloodPressureSystolic);
        AddNullable(command,"@BloodPressureDiastolic",SqlDbType.Int,request.BloodPressureDiastolic);
        AddNullable(command,"@HeartRate",SqlDbType.Int,request.HeartRate);
        AddNullable(command,"@RespiratoryRate",SqlDbType.Int,request.RespiratoryRate);
        AddDecimal(command,"@TemperatureCelsius",5,2,request.TemperatureCelsius);
        AddNullable(command,"@OxygenSaturation",SqlDbType.Int,request.OxygenSaturation);
        AddDecimal(command,"@HeightCm",6,2,request.HeightCm);
        AddDecimal(command,"@WeightKg",6,2,request.WeightKg);
        command.Parameters.Add("@Notes",SqlDbType.NVarChar,1000).Value = string.IsNullOrWhiteSpace(request.Notes) ? DBNull.Value : request.Notes.Trim();
        command.Parameters.Add(vitalUid.HasValue ? "@UpdatedBy" : "@CreatedBy",SqlDbType.BigInt).Value = (object?)userId ?? DBNull.Value;
        await connection.OpenAsync(cancellationToken);
        try { await using var reader=await command.ExecuteReaderAsync(cancellationToken); return await reader.ReadAsync(cancellationToken)?Map(reader):null; }
        catch(SqlException ex) { logger.LogError(ex,"Failed to save patient vitals."); throw; }
    }
    private static SqlCommand Command(SqlConnection connection,string name)=>new(name,connection){CommandType=CommandType.StoredProcedure};
    private static void AddIds(SqlCommand c,Guid p,Guid v){c.Parameters.Add("@PatientUid",SqlDbType.UniqueIdentifier).Value=p;c.Parameters.Add("@PatientVitalUid",SqlDbType.UniqueIdentifier).Value=v;}
    private static void AddNullable(SqlCommand c,string n,SqlDbType t,object? v)=>c.Parameters.Add(n,t).Value=v??DBNull.Value;
    private static void AddDecimal(SqlCommand c,string n,byte precision,byte scale,decimal? v){var p=c.Parameters.Add(n,SqlDbType.Decimal);p.Precision=precision;p.Scale=scale;p.Value=v??(object)DBNull.Value;}
    private static PatientVitalResponse Map(SqlDataReader r)=>new(){PatientVitalUid=r.GetGuid(r.GetOrdinal("PatientVitalUid")),PatientUid=r.GetGuid(r.GetOrdinal("PatientUid")),RecordedAt=r.GetDateTime(r.GetOrdinal("RecordedAt")),BloodPressureSystolic=I(r,"BloodPressureSystolic"),BloodPressureDiastolic=I(r,"BloodPressureDiastolic"),HeartRate=I(r,"HeartRate"),RespiratoryRate=I(r,"RespiratoryRate"),TemperatureCelsius=D(r,"TemperatureCelsius"),OxygenSaturation=I(r,"OxygenSaturation"),HeightCm=D(r,"HeightCm"),WeightKg=D(r,"WeightKg"),Bmi=D(r,"Bmi"),Notes=S(r,"Notes"),CreatedAt=r.GetDateTime(r.GetOrdinal("CreatedAt")),CreatedBy=L(r,"CreatedBy"),CreatedByDisplayName=S(r,"CreatedByDisplayName"),UpdatedAt=DT(r,"UpdatedAt"),UpdatedBy=L(r,"UpdatedBy"),UpdatedByDisplayName=S(r,"UpdatedByDisplayName"),RowVersion=Convert.ToBase64String((byte[])r["RowVersion"])};
    private static int? I(SqlDataReader r,string n)=>r.IsDBNull(r.GetOrdinal(n))?null:r.GetInt32(r.GetOrdinal(n)); private static decimal? D(SqlDataReader r,string n)=>r.IsDBNull(r.GetOrdinal(n))?null:r.GetDecimal(r.GetOrdinal(n)); private static long? L(SqlDataReader r,string n)=>r.IsDBNull(r.GetOrdinal(n))?null:r.GetInt64(r.GetOrdinal(n)); private static string? S(SqlDataReader r,string n)=>r.IsDBNull(r.GetOrdinal(n))?null:r.GetString(r.GetOrdinal(n)); private static DateTime? DT(SqlDataReader r,string n)=>r.IsDBNull(r.GetOrdinal(n))?null:r.GetDateTime(r.GetOrdinal(n));
}
