using Microsoft.Extensions.Logging;

namespace MicroEMR.Application.PatientPrescriptions;

public sealed record DispensingLocation(Guid LocationUid,string System,string Identifier,string FacilityName,string FacilityAddress,string? Phone,bool IsActive,DateTime CreatedAt,long CreatedBy,DateTime? UpdatedAt,long? UpdatedBy,string RowVersion);
public sealed record DispensingLocationPage(IReadOnlyList<DispensingLocation> Items,int Page,int PageSize,bool HasMore);
public sealed record DispensingLocationSelection(Guid LocationUid,string RowVersion);
public sealed class SaveDispensingLocation
{
    public string System {get;set;}="";
    public string Identifier {get;set;}="";
    public string FacilityName {get;set;}="";
    public string FacilityAddress {get;set;}="";
    public string? Phone {get;set;}
    public string? RowVersion {get;set;}
}
public interface IDispensingLocationRepository
{
    Task<IReadOnlyList<DispensingLocation>> SearchAsync(string query,bool includeInactive,int offset,int take,CancellationToken token);
    Task<DispensingLocation?> GetAsync(Guid? uid,PrescriptionDispensingLocationReference? identity,CancellationToken token);
    Task<DispensingLocation> SaveAsync(Guid? uid,string mode,SaveDispensingLocation request,long actor,CancellationToken token);
}
public sealed class DispensingLocationConflictException(string message):Exception(message);
public sealed class DispensingLocationDirectory(IDispensingLocationRepository repository,ILogger<DispensingLocationDirectory> logger)
{
    public async Task<DispensingLocationPage> SearchAsync(string? query,bool includeInactive,int page=1,int pageSize=20,CancellationToken token=default)
    {
        if(page<1||page>10000||pageSize<1||pageSize>50)throw new ArgumentException("Page must be 1–10000 and page size 1–50.");
        var text=query?.Trim()??"";if(text.Length>200||text.Any(char.IsControl))throw new ArgumentException("Search must be at most 200 characters without controls.");
        var rows=await repository.SearchAsync(text,includeInactive,(page-1)*pageSize,pageSize+1,token);
        return new(rows.Take(pageSize).ToArray(),page,pageSize,rows.Count>pageSize);
    }
    public Task<DispensingLocation?> GetAsync(Guid uid,CancellationToken token=default)=>repository.GetAsync(uid,null,token);
    public Task<DispensingLocation?> ResolveAsync(string system,string identifier,CancellationToken token=default)=>repository.GetAsync(null,PrescriptionDispensingLocationReference.Normalize(new(system,identifier)),token);
    public async Task<DispensingLocation> SaveAsync(Guid? uid,string mode,SaveDispensingLocation request,long actor,CancellationToken token=default)
    {
        if(actor<=0)throw new ArgumentException("An active actor is required.");
        if(mode is not ("Create" or "Edit" or "Deactivate" or "Reactivate"))throw new ArgumentException("Invalid maintenance operation.");
        if(mode!="Create"){Version(request.RowVersion);if(uid is null||uid==Guid.Empty)throw new ArgumentException("Location is required.");}
        if(mode is "Create" or "Edit")
        {
            var pair=PrescriptionDispensingLocationReference.Normalize(new(request.System,request.Identifier))!;
            request.System=pair.System!;request.Identifier=pair.Identifier!;
            request.FacilityName=TargetDispensingFacilityName.Normalize(request.FacilityName)??throw new ArgumentException("Facility name is required.");
            request.FacilityAddress=TargetDispensingFacilityAddress.Normalize(request.FacilityAddress)??throw new ArgumentException("Facility address is required.");
            request.Phone=string.IsNullOrWhiteSpace(request.Phone)?null:TargetDispensingFacilityName.Normalize(request.Phone);
            if(request.Phone?.Length>30)throw new ArgumentException("Phone cannot exceed 30 characters.");
        }
        try{return await repository.SaveAsync(uid,mode,request,actor,token);}
        catch(DispensingLocationConflictException){logger.LogWarning("Dispensing location {Operation} rejected for {LocationUid} due to identity or concurrency conflict.",mode,uid);throw;}
    }
    public static byte[] Version(string? value)
    {
        if(value is null||!Convert.TryFromBase64String(value,new byte[8],out var count)||count!=8)throw new ArgumentException("A current eight-byte RowVersion is required.");
        return Convert.FromBase64String(value);
    }
}
