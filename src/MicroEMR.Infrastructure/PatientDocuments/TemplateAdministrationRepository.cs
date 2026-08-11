using System.Data;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.Templates.Contracts;
using MicroEMR.Application.Templates.Repositories;
using MicroEMR.Infrastructure.Tenancy;
using MicroEMR.Application.PatientDocuments;

namespace MicroEMR.Infrastructure.PatientDocuments;

public sealed class TemplateAdministrationRepository(ITenantSqlConnectionFactory connections) : ITemplateAdministrationRepository
{
    public Task<TemplateAdministrationResult?> CreateAsync(CreateAdministrativeTemplateRequest request,string definitionJson,long actorUserId,CancellationToken token=default) =>
        ExecuteCreateOrCloneAsync("dbo.DocumentTemplateAdmin_Create",null,request.Name,request.TemplateKind,request.Category,request.TemplateScope,request.OwnerUserId,null,definitionJson,actorUserId,token);

    public async Task<DocumentTemplateDetailsResponse?> UpdateMetadataAsync(Guid uid,UpdateAdministrativeTemplateMetadataRequest request,long actorUserId,CancellationToken token=default)
    {
        await using var connection=await connections.OpenConnectionAsync(token);
        await using var command=Command("dbo.DocumentTemplateAdmin_UpdateMetadata",connection);
        Add(command,"@TemplateUid",SqlDbType.UniqueIdentifier,uid); Add(command,"@TemplateName",SqlDbType.NVarChar,request.Name,200);
        Add(command,"@TemplateKind",SqlDbType.NVarChar,request.TemplateKind,20); Add(command,"@Category",SqlDbType.NVarChar,request.Category,100);
        Add(command,"@TemplateScope",SqlDbType.NVarChar,request.TemplateScope,20); Add(command,"@OwnerUserId",SqlDbType.BigInt,request.OwnerUserId);
        Add(command,"@ExpectedRowVersion",SqlDbType.Binary,Convert.FromBase64String(request.RowVersion),8); Add(command,"@UpdatedBy",SqlDbType.BigInt,actorUserId);
        try { await using var reader=await command.ExecuteReaderAsync(token); return await reader.ReadAsync(token)?MapTemplate(reader):null; }
        catch(SqlException exception) when(exception.Number==51043){throw new DocumentTemplateVersionConflictException("The template was updated by another user.",exception);}
    }

    public Task<TemplateAdministrationResult?> CloneAsync(Guid sourceUid,CloneDocumentTemplateRequest request,long actorUserId,CancellationToken token=default) =>
        ExecuteCreateOrCloneAsync("dbo.DocumentTemplateAdmin_Clone",sourceUid,request.Name,null,null,request.TemplateScope,request.OwnerUserId,request.SourceTemplateVersionUid,null,actorUserId,token);

    public async Task<DocumentTemplateDetailsResponse?> SetActiveAsync(Guid uid,SetAdministrativeTemplateActiveRequest request,long actorUserId,CancellationToken token=default)
    {
        await using var connection=await connections.OpenConnectionAsync(token);await using var command=Command("dbo.DocumentTemplateAdmin_SetActive",connection);
        Add(command,"@TemplateUid",SqlDbType.UniqueIdentifier,uid);Add(command,"@IsActive",SqlDbType.Bit,request.IsActive);
        Add(command,"@ExpectedRowVersion",SqlDbType.Binary,Convert.FromBase64String(request.RowVersion),8);Add(command,"@UpdatedBy",SqlDbType.BigInt,actorUserId);
        try{await using var reader=await command.ExecuteReaderAsync(token);return await reader.ReadAsync(token)?MapTemplate(reader):null;}
        catch(SqlException exception) when(exception.Number==51043){throw new DocumentTemplateVersionConflictException("The template was updated by another user.",exception);}
    }

    private async Task<TemplateAdministrationResult?> ExecuteCreateOrCloneAsync(string procedure,Guid? sourceUid,string name,string? kind,string? category,string scope,long? owner,Guid? sourceVersion,string? json,long actor,CancellationToken token)
    {
        await using var connection=await connections.OpenConnectionAsync(token); await using var command=Command(procedure,connection);
        if(sourceUid.HasValue)Add(command,"@SourceTemplateUid",SqlDbType.UniqueIdentifier,sourceUid.Value);
        Add(command,"@TemplateName",SqlDbType.NVarChar,name,200); if(kind is not null)Add(command,"@TemplateKind",SqlDbType.NVarChar,kind,20);
        if(category is not null)Add(command,"@Category",SqlDbType.NVarChar,category,100); Add(command,"@TemplateScope",SqlDbType.NVarChar,scope,20);
        Add(command,"@OwnerUserId",SqlDbType.BigInt,owner); if(sourceVersion.HasValue)Add(command,"@SourceTemplateVersionUid",SqlDbType.UniqueIdentifier,sourceVersion.Value);
        if(json is not null){Add(command,"@SchemaVersion",SqlDbType.Int,1);Add(command,"@DefinitionJson",SqlDbType.NVarChar,json,-1);}
        Add(command,"@CreatedBy",SqlDbType.BigInt,actor);
        await using var reader=await command.ExecuteReaderAsync(token); if(!await reader.ReadAsync(token))return null;
        var template=MapTemplate(reader); DocumentTemplateVersionResponse? version=null;
        if(await reader.NextResultAsync(token)&&await reader.ReadAsync(token))version=MapVersion(reader);
        return new(){Template=template,DraftVersion=version};
    }

    private static SqlCommand Command(string name,SqlConnection connection)=>new(name,connection){CommandType=CommandType.StoredProcedure};
    private static void Add(SqlCommand command,string name,SqlDbType type,object? value,int size=0)=>command.Parameters.Add(new SqlParameter(name,type,size){Value=value??DBNull.Value});
    private static DocumentTemplateDetailsResponse MapTemplate(SqlDataReader r)=>new(){TemplateUid=r.GetGuid(r.GetOrdinal("TemplateUid")),TemplateName=r.GetString(r.GetOrdinal("TemplateName")),DocumentType=r.GetString(r.GetOrdinal("DocumentType")),TemplateKind=r.GetString(r.GetOrdinal("TemplateKind")),Category=OptionalString(r,"Category"),TemplateScope=r.GetString(r.GetOrdinal("TemplateScope")),OwnerUserId=OptionalLong(r,"OwnerUserId"),Description=OptionalString(r,"Description"),TemplateContent=r.GetString(r.GetOrdinal("TemplateContent")),IsActive=r.GetBoolean(r.GetOrdinal("IsActive")),CreatedAt=r.GetDateTime(r.GetOrdinal("CreatedAt")),CreatedBy=OptionalLong(r,"CreatedBy"),UpdatedAt=OptionalDate(r,"UpdatedAt"),UpdatedBy=OptionalLong(r,"UpdatedBy"),RowVersion=Convert.ToBase64String((byte[])r["RowVersion"]),TemplateVersionUid=OptionalGuid(r,"TemplateVersionUid"),CurrentVersion=OptionalInt(r,"CurrentVersion")};
    private static DocumentTemplateVersionResponse MapVersion(SqlDataReader r)=>new(){TemplateVersionUid=r.GetGuid(r.GetOrdinal("TemplateVersionUid")),TemplateUid=r.GetGuid(r.GetOrdinal("TemplateUid")),VersionNumber=r.GetInt32(r.GetOrdinal("VersionNumber")),TemplateContent=r.GetString(r.GetOrdinal("TemplateContent")),SchemaVersion=r.GetInt32(r.GetOrdinal("SchemaVersion")),DefinitionJson=r.GetString(r.GetOrdinal("DefinitionJson")),Status=r.GetString(r.GetOrdinal("VersionStatus")),IsCurrent=r.GetBoolean(r.GetOrdinal("IsCurrent")),CreatedAt=r.GetDateTime(r.GetOrdinal("CreatedAt")),CreatedBy=OptionalLong(r,"CreatedBy"),RowVersion=Convert.ToBase64String((byte[])r["RowVersion"])};
    private static string? OptionalString(SqlDataReader r,string n){var i=r.GetOrdinal(n);return r.IsDBNull(i)?null:r.GetString(i);} private static long? OptionalLong(SqlDataReader r,string n){var i=r.GetOrdinal(n);return r.IsDBNull(i)?null:r.GetInt64(i);} private static Guid? OptionalGuid(SqlDataReader r,string n){var i=r.GetOrdinal(n);return r.IsDBNull(i)?null:r.GetGuid(i);} private static int? OptionalInt(SqlDataReader r,string n){var i=r.GetOrdinal(n);return r.IsDBNull(i)?null:r.GetInt32(i);} private static DateTime? OptionalDate(SqlDataReader r,string n){var i=r.GetOrdinal(n);return r.IsDBNull(i)?null:r.GetDateTime(i);}
}
