using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Application.Providers;

public sealed record ProviderAdministrationItem(Guid ProviderUid,string FirstName,string LastName,string DisplayName,string ProviderType,string? BillingNumber,string? Specialty,bool IsActive,DateTime CreatedAt,long? CreatedBy,DateTime? UpdatedAt,long? UpdatedBy,Guid? LinkedApplicationUserUid,string? LinkedApplicationUserDisplayName,string? LinkedApplicationUserEmail,string RowVersion);
public sealed record EligibleApplicationUser(Guid ApplicationUserUid,string DisplayName,string? Email);
public sealed class SaveProviderRequest
{
    [Required,StringLength(100)]public string FirstName{get;set;}="";
    [Required,StringLength(100)]public string LastName{get;set;}="";
    [Required,StringLength(200)]public string DisplayName{get;set;}="";
    [Required,StringLength(50)]public string ProviderType{get;set;}="";
    [StringLength(50)]public string? BillingNumber{get;set;}
    [StringLength(100)]public string? Specialty{get;set;}
    public string? RowVersion{get;set;}
}
public sealed record ProviderVersionRequest([Required]string RowVersion);
public sealed record ProviderLinkRequest(Guid ApplicationUserUid,[Required]string RowVersion);
public sealed class ProviderConcurrencyException:Exception;
public sealed class ProviderConflictException(string message):Exception(message);

public interface IProviderAdministrationRepository
{
    Task<IReadOnlyList<ProviderAdministrationItem>>ListAsync(string status,CancellationToken token=default);
    Task<ProviderAdministrationItem?>GetAsync(Guid uid,CancellationToken token=default);
    Task<ProviderAdministrationItem>CreateAsync(SaveProviderRequest request,long actor,CancellationToken token=default);
    Task<ProviderAdministrationItem?>UpdateAsync(Guid uid,SaveProviderRequest request,long actor,CancellationToken token=default);
    Task<ProviderAdministrationItem?>SetActiveAsync(Guid uid,bool active,string version,long actor,CancellationToken token=default);
    Task<IReadOnlyList<EligibleApplicationUser>>EligibleUsersAsync(Guid? uid,CancellationToken token=default);
    Task<ProviderAdministrationItem?>LinkAsync(Guid uid,ProviderLinkRequest request,long actor,CancellationToken token=default);
    Task<ProviderAdministrationItem?>UnlinkAsync(Guid uid,ProviderLinkRequest request,long actor,CancellationToken token=default);
}
public interface IProviderAdministrationService
{
    Task<IReadOnlyList<ProviderAdministrationItem>>ListAsync(string status,CancellationToken token=default);
    Task<ProviderAdministrationItem?>GetAsync(Guid uid,CancellationToken token=default);
    Task<ProviderAdministrationItem>CreateAsync(SaveProviderRequest request,long actor,CancellationToken token=default);
    Task<ProviderAdministrationItem?>UpdateAsync(Guid uid,SaveProviderRequest request,long actor,CancellationToken token=default);
    Task<ProviderAdministrationItem?>SetActiveAsync(Guid uid,bool active,string version,long actor,CancellationToken token=default);
    Task<IReadOnlyList<EligibleApplicationUser>>EligibleUsersAsync(Guid? uid,CancellationToken token=default);
    Task<ProviderAdministrationItem?>LinkAsync(Guid uid,ProviderLinkRequest request,long actor,CancellationToken token=default);
    Task<ProviderAdministrationItem?>UnlinkAsync(Guid uid,ProviderLinkRequest request,long actor,CancellationToken token=default);
}
public sealed class ProviderAdministrationService(IProviderAdministrationRepository repository):IProviderAdministrationService
{
    private static readonly string[] Statuses=["Active","Inactive","All"];
    public Task<IReadOnlyList<ProviderAdministrationItem>>ListAsync(string status,CancellationToken token=default){if(!Statuses.Contains(status,StringComparer.OrdinalIgnoreCase))throw new ArgumentException("Status must be Active, Inactive, or All.");return repository.ListAsync(Statuses.First(x=>x.Equals(status,StringComparison.OrdinalIgnoreCase)),token);}
    public Task<ProviderAdministrationItem?>GetAsync(Guid uid,CancellationToken token=default)=>repository.GetAsync(Required(uid),token);
    public Task<ProviderAdministrationItem>CreateAsync(SaveProviderRequest request,long actor,CancellationToken token=default){Normalize(request,false);return repository.CreateAsync(request,Actor(actor),token);}
    public Task<ProviderAdministrationItem?>UpdateAsync(Guid uid,SaveProviderRequest request,long actor,CancellationToken token=default){Normalize(request,true);return repository.UpdateAsync(Required(uid),request,Actor(actor),token);}
    public Task<ProviderAdministrationItem?>SetActiveAsync(Guid uid,bool active,string version,long actor,CancellationToken token=default){Version(version);return repository.SetActiveAsync(Required(uid),active,version,Actor(actor),token);}
    public Task<IReadOnlyList<EligibleApplicationUser>>EligibleUsersAsync(Guid? uid,CancellationToken token=default)=>repository.EligibleUsersAsync(uid,token);
    public Task<ProviderAdministrationItem?>LinkAsync(Guid uid,ProviderLinkRequest request,long actor,CancellationToken token=default){Link(request);return repository.LinkAsync(Required(uid),request,Actor(actor),token);}
    public Task<ProviderAdministrationItem?>UnlinkAsync(Guid uid,ProviderLinkRequest request,long actor,CancellationToken token=default){Link(request);return repository.UnlinkAsync(Required(uid),request,Actor(actor),token);}
    private static void Normalize(SaveProviderRequest x,bool version){x.FirstName=Need(x.FirstName,nameof(x.FirstName));x.LastName=Need(x.LastName,nameof(x.LastName));x.DisplayName=Need(x.DisplayName,nameof(x.DisplayName));x.ProviderType=Need(x.ProviderType,nameof(x.ProviderType));x.BillingNumber=Optional(x.BillingNumber);x.Specialty=Optional(x.Specialty);if(version)Version(x.RowVersion);}
    private static string Need(string? x,string name)=>string.IsNullOrWhiteSpace(x)?throw new ArgumentException($"{name} is required."):x.Trim();
    private static string? Optional(string? x)=>string.IsNullOrWhiteSpace(x)?null:x.Trim();
    private static void Version(string? x){if(string.IsNullOrWhiteSpace(x))throw new ArgumentException("RowVersion is required.");try{if(Convert.FromBase64String(x).Length!=8)throw new FormatException();}catch(FormatException){throw new ArgumentException("RowVersion is invalid.");}}
    private static void Link(ProviderLinkRequest x){if(x.ApplicationUserUid==Guid.Empty)throw new ArgumentException("ApplicationUserUid is required.");Version(x.RowVersion);}
    private static Guid Required(Guid x)=>x==Guid.Empty?throw new ArgumentException("ProviderUid is required."):x;
    private static long Actor(long x)=>x<=0?throw new ArgumentException("Actor is required."):x;
}
