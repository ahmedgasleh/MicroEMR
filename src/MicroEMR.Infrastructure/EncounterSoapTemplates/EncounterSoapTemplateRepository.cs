using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MicroEMR.Application.EncounterSoapTemplates;

namespace MicroEMR.Infrastructure.EncounterSoapTemplates;

public sealed class EncounterSoapTemplateRepository : IEncounterSoapTemplateRepository
{
    private readonly string _connectionString;
    public EncounterSoapTemplateRepository(IConfiguration configuration) => _connectionString = configuration.GetConnectionString("MicroEmrDatabase") ?? throw new InvalidOperationException("Connection string 'MicroEmrDatabase' was not found.");

    public async Task<IReadOnlyList<EncounterSoapTemplateResponse>> GetAllAsync(string statusFilter, CancellationToken cancellationToken=default)
    {
        var list=new List<EncounterSoapTemplateResponse>(); await using var connection=new SqlConnection(_connectionString);
        await using var command=new SqlCommand("dbo.EncounterSoapTemplate_GetAll",connection){CommandType=CommandType.StoredProcedure};
        command.Parameters.Add(new SqlParameter("@StatusFilter",SqlDbType.NVarChar,50){Value=Normalize(statusFilter)});
        await connection.OpenAsync(cancellationToken); await using var reader=await command.ExecuteReaderAsync(cancellationToken);
        while(await reader.ReadAsync(cancellationToken))list.Add(Map(reader)); return list;
    }
    public async Task<EncounterSoapTemplateResponse?> GetByUidAsync(Guid uid,CancellationToken cancellationToken=default)=>await ExecuteAsync("dbo.EncounterSoapTemplate_GetByUid",uid,null,null,null,cancellationToken);
    public async Task<EncounterSoapTemplateResponse?> CreateAsync(CreateEncounterSoapTemplateRequest request,long? userId,CancellationToken cancellationToken=default)=>await ExecuteAsync("dbo.EncounterSoapTemplate_Create",null,request,null,userId,cancellationToken);
    public async Task<EncounterSoapTemplateResponse?> UpdateAsync(Guid uid,UpdateEncounterSoapTemplateRequest request,long? userId,CancellationToken cancellationToken=default)=>await ExecuteAsync("dbo.EncounterSoapTemplate_Update",uid,request,null,userId,cancellationToken);
    public async Task<EncounterSoapTemplateResponse?> SetActiveAsync(Guid uid,bool isActive,long? userId,CancellationToken cancellationToken=default)=>await ExecuteAsync("dbo.EncounterSoapTemplate_SetActive",uid,null,isActive,userId,cancellationToken);
    private async Task<EncounterSoapTemplateResponse?> ExecuteAsync(string procedure,Guid? uid,SaveEncounterSoapTemplateRequest? request,bool? active,long? userId,CancellationToken token)
    {
        await using var connection=new SqlConnection(_connectionString); await using var command=new SqlCommand(procedure,connection){CommandType=CommandType.StoredProcedure};
        if(uid.HasValue)command.Parameters.Add(new SqlParameter("@EncounterSoapTemplateUid",SqlDbType.UniqueIdentifier){Value=uid.Value});
        if(request is not null){Add(command,"@TemplateName",200,request.TemplateName);Add(command,"@EncounterType",100,request.EncounterType);Add(command,"@SubjectiveTemplate",-1,request.SubjectiveTemplate);Add(command,"@ObjectiveTemplate",-1,request.ObjectiveTemplate);Add(command,"@AssessmentTemplate",-1,request.AssessmentTemplate);Add(command,"@PlanTemplate",-1,request.PlanTemplate);}
        if(active.HasValue)command.Parameters.Add(new SqlParameter("@IsActive",SqlDbType.Bit){Value=active.Value});
        if(request is not null||active.HasValue)command.Parameters.Add(new SqlParameter(procedure.EndsWith("Create",StringComparison.Ordinal)?"@CreatedBy":"@UpdatedBy",SqlDbType.BigInt){Value=(object?)userId??DBNull.Value});
        await connection.OpenAsync(token);await using var reader=await command.ExecuteReaderAsync(token);return await reader.ReadAsync(token)?Map(reader):null;
    }
    private static void Add(SqlCommand c,string n,int size,string? value)=>c.Parameters.Add(new SqlParameter(n,SqlDbType.NVarChar,size){Value=string.IsNullOrWhiteSpace(value)?DBNull.Value:value.Trim()});
    private static string Normalize(string? x)=>x?.ToLowerInvariant() switch{"inactive"=>"Inactive","all"=>"All",_=>"Active"};
    private static EncounterSoapTemplateResponse Map(SqlDataReader r)=>new(){EncounterSoapTemplateUid=r.GetGuid(r.GetOrdinal("EncounterSoapTemplateUid")),TemplateName=r.GetString(r.GetOrdinal("TemplateName")),EncounterType=S(r,"EncounterType"),SubjectiveTemplate=S(r,"SubjectiveTemplate"),ObjectiveTemplate=S(r,"ObjectiveTemplate"),AssessmentTemplate=S(r,"AssessmentTemplate"),PlanTemplate=S(r,"PlanTemplate"),IsActive=r.GetBoolean(r.GetOrdinal("IsActive")),CreatedAt=r.GetDateTime(r.GetOrdinal("CreatedAt")),CreatedBy=L(r,"CreatedBy"),CreatedByDisplayName=S(r,"CreatedByDisplayName"),UpdatedAt=D(r,"UpdatedAt"),UpdatedBy=L(r,"UpdatedBy"),UpdatedByDisplayName=S(r,"UpdatedByDisplayName"),RowVersion=Convert.ToBase64String((byte[])r["RowVersion"])};
    private static string? S(SqlDataReader r,string n){var o=r.GetOrdinal(n);return r.IsDBNull(o)?null:r.GetString(o);}private static long? L(SqlDataReader r,string n){var o=r.GetOrdinal(n);return r.IsDBNull(o)?null:r.GetInt64(o);}private static DateTime? D(SqlDataReader r,string n){var o=r.GetOrdinal(n);return r.IsDBNull(o)?null:r.GetDateTime(o);}
}
