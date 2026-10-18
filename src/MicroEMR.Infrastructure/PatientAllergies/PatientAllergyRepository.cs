using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MicroEMR.Infrastructure.Tenancy;
using MicroEMR.Application.PatientAllergies;
using MicroEMR.Application.PatientAllergies.Contracts;
using MicroEMR.Application.PatientAllergies.Repositories;
using MicroEMR.Application.PatientClinicalHistory;

namespace MicroEMR.Infrastructure.PatientAllergies;

public sealed class PatientAllergyRepository(ITenantSqlConnectionFactory connections,ILogger<PatientAllergyRepository> logger) : IPatientAllergyRepository
{
    public async Task<AllergySnapshot> GetSnapshotAsync(Guid patientUid,CancellationToken token=default)
    {
        await using var c=await connections.OpenConnectionAsync(token);
        await using var q=Command(c,"dbo.PatientAllergy_GetSnapshot");Add(q,"@PatientUid",SqlDbType.UniqueIdentifier,patientUid);
        try
        {
            await using var r=await q.ExecuteReaderAsync(token);var rows=new List<PatientAllergyListItemResponse>();
            while(await r.ReadAsync(token))rows.Add(ListItem(r));
            if(!await r.NextResultAsync(token)||!await r.ReadAsync(token))throw new PatientAllergyNotFoundException();
            var state=new AllergyDocumentationStateResponse(r.GetString(r.GetOrdinal("DocumentationState")),r["AssertionUid"] is DBNull?null:Assertion(r));
            return new(rows,state);
        }
        catch(SqlException e){throw Translate(e);}
    }
    public async Task<IReadOnlyList<PatientAllergyListItemResponse>> GetByPatientUidAsync(Guid patientUid,CancellationToken cancellationToken=default)=>(await GetSnapshotAsync(patientUid,cancellationToken)).Items;
    public async Task<AllergyDocumentationStateResponse> GetDocumentationStateAsync(Guid patientUid,CancellationToken cancellationToken=default)=>(await GetSnapshotAsync(patientUid,cancellationToken)).Documentation;
    public Task<PatientAllergyDetailsResponse?> GetByUidAsync(Guid allergyUid,CancellationToken cancellationToken=default)=>ReadDetail(null,allergyUid,cancellationToken);
    public Task<PatientAllergyDetailsResponse?> GetForPatientAsync(Guid patientUid,Guid allergyUid,CancellationToken token=default)=>ReadDetail(patientUid,allergyUid,token);
    private async Task<PatientAllergyDetailsResponse?> ReadDetail(Guid? patient,Guid uid,CancellationToken token)
    {
        await using var c=await connections.OpenConnectionAsync(token);await using var q=Command(c,"dbo.PatientAllergy_GetByUid");
        Add(q,"@AllergyUid",SqlDbType.UniqueIdentifier,uid);Add(q,"@PatientUid",SqlDbType.UniqueIdentifier,patient);
        await using var r=await q.ExecuteReaderAsync(token);return await r.ReadAsync(token)?Details(r):null;
    }
    public async Task<PatientAllergyDetailsResponse> CreateAsync(Guid patientUid,CreatePatientAllergyRequest request,long? createdBy,string? createdByDisplayName,CancellationToken cancellationToken=default)
        =>await Save("Create",patientUid,null,request,createdBy,cancellationToken)??throw new PatientAllergyNotFoundException();
    public Task<PatientAllergyDetailsResponse?> UpdateAsync(Guid patientUid,Guid allergyUid,UpdatePatientAllergyRequest request,long? updatedBy,CancellationToken cancellationToken=default)
        =>Save("Update",patientUid,allergyUid,request,updatedBy,cancellationToken);
    public Task<PatientAllergyDetailsResponse?> ResolveAsync(Guid patientUid,Guid allergyUid,ResolvePatientAllergyRequest request,long? resolvedBy,CancellationToken cancellationToken=default)
        =>Write("dbo.PatientAllergy_Resolve",patientUid,q=>{Add(q,"@AllergyUid",SqlDbType.UniqueIdentifier,allergyUid);Add(q,"@ResolveReason",SqlDbType.NVarChar,request.ResolveReason,500);Add(q,"@ResolvedBy",SqlDbType.BigInt,resolvedBy);Add(q,"@ExpectedRowVersion",SqlDbType.Binary,Version(request.RowVersion),8);Add(q,"@RequestCorrelationId",SqlDbType.NVarChar,request.RequestCorrelationId,100);},Details,cancellationToken);
    private Task<PatientAllergyDetailsResponse?> Save(string action,Guid patient,Guid? uid,AllergySaveRequest request,long? actor,CancellationToken token)
        =>Write("dbo.PatientAllergy_"+action,patient,q=>
        {
            Add(q,"@AllergenName",SqlDbType.NVarChar,request.AllergenName,200);Add(q,"@AllergenType",SqlDbType.NVarChar,request.AllergenType,100);
            Add(q,"@Reaction",SqlDbType.NVarChar,request.Reaction,500);Add(q,"@Severity",SqlDbType.NVarChar,request.Severity,30);Add(q,"@Notes",SqlDbType.NVarChar,request.Notes,1000);
            Add(q,"@OnsetDate",SqlDbType.Date,request.OnsetDate?.Date);Add(q,"@OnsetDateSpecified",SqlDbType.Bit,request.OnsetDateSpecified);
            Add(q,"@ClinicalFields",SqlDbType.NVarChar,request.ClinicalFields is null?null:JsonSerializer.Serialize(request.ClinicalFields),-1);Add(q,"@RequestCorrelationId",SqlDbType.NVarChar,request.RequestCorrelationId,100);
            if(request is CreatePatientAllergyRequest create)
            {
                Add(q,"@CreatedBy",SqlDbType.BigInt,actor);Add(q,"@ConfirmReplaceNoKnownAllergies",SqlDbType.Bit,create.ConfirmReplaceNoKnownAllergies);
                Add(q,"@ExpectedAssertionUid",SqlDbType.UniqueIdentifier,create.ExpectedAssertionUid);
                Add(q,"@ExpectedAssertionRowVersion",SqlDbType.Binary,create.ExpectedAssertionRowVersion is null?null:Version(create.ExpectedAssertionRowVersion),8);
            }
            else if(request is UpdatePatientAllergyRequest edit)
            {
                Add(q,"@AllergyUid",SqlDbType.UniqueIdentifier,uid);Add(q,"@AllergyStatus",SqlDbType.NVarChar,edit.Status,30);Add(q,"@UpdatedBy",SqlDbType.BigInt,actor);Add(q,"@RowVersion",SqlDbType.Binary,Version(edit.RowVersion),8);
            }
        },Details,token);
    public async Task<NoKnownAllergiesAssertionResponse> AssertNoKnownAllergiesAsync(Guid patientUid,long verifiedBy,CancellationToken cancellationToken=default,string? correlation=null)
        =>await Write("dbo.PatientAllergy_AssertNoKnownAllergies",patientUid,q=>{Add(q,"@VerifiedBy",SqlDbType.BigInt,verifiedBy);Add(q,"@RequestCorrelationId",SqlDbType.NVarChar,correlation,100);},Assertion,cancellationToken)??throw new PatientAllergyNotFoundException();
    public Task<NoKnownAllergiesAssertionResponse?> RevokeNoKnownAllergiesAsync(Guid patientUid,RevokeNoKnownAllergiesRequest request,long revokedBy,CancellationToken cancellationToken=default)
        =>Write("dbo.PatientAllergy_RevokeNoKnownAllergies",patientUid,q=>{Add(q,"@RevokedBy",SqlDbType.BigInt,revokedBy);Add(q,"@ExpectedAssertionUid",SqlDbType.UniqueIdentifier,request.AssertionUid);Add(q,"@ExpectedRowVersion",SqlDbType.Binary,Version(request.RowVersion),8);Add(q,"@Reason",SqlDbType.NVarChar,request.Reason,500);Add(q,"@RequestCorrelationId",SqlDbType.NVarChar,request.RequestCorrelationId,100);},Assertion,cancellationToken);
    private async Task<T?> Write<T>(string procedure,Guid patient,Action<SqlCommand> configure,Func<SqlDataReader,T> map,CancellationToken token) where T:class
    {
        await using var c=await connections.OpenConnectionAsync(token);await using var q=Command(c,procedure);Add(q,"@PatientUid",SqlDbType.UniqueIdentifier,patient);configure(q);
        try{await using var r=await q.ExecuteReaderAsync(token);return await r.ReadAsync(token)?map(r):null;}
        catch(SqlException e){logger.LogError(e,"Allergy operation {Procedure} failed for {PatientUid}.",procedure,patient);throw Translate(e);}
    }
    private static Exception Translate(SqlException e)=>e.Number switch
    {
        52400=>new PatientAllergyValidationException(e.Message),52401=>new PatientAllergyNotFoundException(),
        52403=>new UnauthorizedAccessException("An active clinical actor is required."),52408 or 52409=>new PatientAllergyConcurrencyException(e.Message,e),_=>e
    };
    private static byte[] Version(string? value)
    {
        try{var bytes=Convert.FromBase64String(value??"");return bytes.Length==8?bytes:throw new FormatException();}
        catch(FormatException){throw new PatientAllergyValidationException("Reload before saving: an exact eight-byte RowVersion is required.");}
    }
    private static SqlCommand Command(SqlConnection c,string name)=>new(name,c){CommandType=CommandType.StoredProcedure};
    private static void Add(SqlCommand q,string name,SqlDbType type,object? value,int size=0)=>(size==0?q.Parameters.Add(name,type):q.Parameters.Add(name,type,size)).Value=value is string text&&string.IsNullOrWhiteSpace(text)?DBNull.Value:value??DBNull.Value;
    private static string? Text(SqlDataReader r,string n)=>r[n] is DBNull?null:(string)r[n];
    private static T? Value<T>(SqlDataReader r,string n) where T:struct=>r[n] is DBNull?null:(T)r[n];
    private static string Token(SqlDataReader r)=>Convert.ToBase64String((byte[])r["RowVersion"]);
    private static AllergyClinicalFields Fields(SqlDataReader r)
    {
        int? Part(string suffix)=>r["Onset"+suffix] is DBNull?null:Convert.ToInt32(r["Onset"+suffix]);var exact=Value<DateTime>(r,"OnsetDate");
        return new(){DrugDIN=Text(r,"DrugDIN"),LifeStageCode=Text(r,"LifeStageCode"),OnsetDate=Part("Year") is {} y?new(){Year=y,Month=Part("Month"),Day=Part("Day")}:exact is {} d?new(){Year=d.Year,Month=d.Month,Day=d.Day}:null};
    }
    private static PatientAllergyListItemResponse ListItem(SqlDataReader r)=>new(){AllergyUid=(Guid)r["AllergyUid"],PatientUid=(Guid)r["PatientUid"],AllergenName=(string)r["AllergenName"],AllergenType=Text(r,"AllergenType"),Reaction=Text(r,"Reaction"),Severity=Text(r,"Severity"),OnsetDate=Value<DateTime>(r,"OnsetDate"),ClinicalFields=Fields(r),Notes=Text(r,"Notes"),Status=(string)r["AllergyStatus"],CreatedBy=Value<long>(r,"CreatedBy"),CreatedByDisplayName=Text(r,"ActorDisplayName"),CreatedAt=(DateTime)r["CreatedAt"],UpdatedAt=Value<DateTime>(r,"UpdatedAt"),RowVersion=Token(r)};
    private static PatientAllergyDetailsResponse Details(SqlDataReader r)=>new(){AllergyUid=(Guid)r["AllergyUid"],PatientUid=(Guid)r["PatientUid"],AllergenName=(string)r["AllergenName"],AllergenType=Text(r,"AllergenType"),Reaction=Text(r,"Reaction"),Severity=Text(r,"Severity"),OnsetDate=Value<DateTime>(r,"OnsetDate"),ClinicalFields=Fields(r),Notes=Text(r,"Notes"),Status=(string)r["AllergyStatus"],CreatedBy=Value<long>(r,"CreatedBy"),CreatedByDisplayName=Text(r,"ActorDisplayName"),CreatedAt=(DateTime)r["CreatedAt"],UpdatedAt=Value<DateTime>(r,"UpdatedAt"),RowVersion=Token(r)};
    private static NoKnownAllergiesAssertionResponse Assertion(SqlDataReader r)=>new((Guid)r["AssertionUid"],(Guid)r["PatientUid"],(string)r["Status"],(long)r["VerifiedBy"],(string)r["VerifiedByDisplayName"],(DateTime)r["VerifiedAtUtc"],Value<long>(r,"RevokedBy"),Value<DateTime>(r,"RevokedAtUtc"),Text(r,"RevocationReason"),Token(r));
}
