using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.Patients.Repositories;
using MicroEMR.Application.Tenancy;

namespace MicroEMR.Application.PatientFiles;

public sealed class PatientFileUploadOptions
{
    public long MaxFileSizeBytes { get; set; } = 26_214_400;
}
public sealed record UploadPatientFileInput(Stream Content,string FileName,string ContentType,long DeclaredLength,string? Description,string? Category);
public sealed record PatientFileResponse(Guid FileUid,Guid PatientUid,string OriginalFileName,string ContentType,long FileSizeBytes,string? FileExtension,string? Sha256Hash,string? Description,string? Category,string Status,DateTime UploadedAtUtc,long UploadedBy,string RowVersion);
public sealed record PatientFileContent(Stream Content,string FileName,string ContentType,long Length);

public interface IPatientFileService
{
    Task<PatientFileResponse> UploadAsync(Guid patientUid,UploadPatientFileInput input,CancellationToken cancellationToken=default);
    Task<IReadOnlyList<PatientFileResponse>> GetByPatientUidAsync(Guid patientUid,CancellationToken cancellationToken=default);
    Task<PatientFileResponse?> GetByUidAsync(Guid patientUid,Guid fileUid,CancellationToken cancellationToken=default);
    Task<PatientFileContent?> OpenContentAsync(Guid patientUid,Guid fileUid,CancellationToken cancellationToken=default);
}

public sealed class PatientFileService(IPatientFileRepository repository,IPatientFileStorage storage,
    IPatientRepository patients,IAuthenticatedClinicalUserAccessor actor,ITenantContext tenant,
    IOptions<PatientFileUploadOptions> options,ILogger<PatientFileService> logger):IPatientFileService
{
    private static readonly Dictionary<string,string> Types=new(StringComparer.OrdinalIgnoreCase){{".pdf","application/pdf"},{".jpg","image/jpeg"},{".jpeg","image/jpeg"},{".png","image/png"},{".txt","text/plain"}};
    public async Task<PatientFileResponse> UploadAsync(Guid patientUid,UploadPatientFileInput input,CancellationToken ct=default)
    {
        if(await patients.GetByUidAsync(patientUid,ct)is null)throw new KeyNotFoundException();
        if(input.DeclaredLength<=0)throw new ArgumentException("File is empty.");if(input.DeclaredLength>options.Value.MaxFileSizeBytes)throw new ArgumentException("File is too large.");
        if(input.Description?.Length>1000||input.Category?.Length>100)throw new ArgumentException("File metadata is too long.");
        var name=PatientFileNaming.OriginalFileName(input.FileName);if(name.Any(char.IsControl))throw new ArgumentException("File name is invalid.");
        var ext=Path.GetExtension(name).ToLowerInvariant();if(!Types.TryGetValue(ext,out var expected)||!string.Equals(expected,input.ContentType,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("File type is not supported.");
        var temp=Path.GetTempFileName();var length=0L;
        try
        {
            await using(var target=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None,81920,true)){var buffer=new byte[81920];int read;while((read=await input.Content.ReadAsync(buffer,ct))>0){length+=read;if(length>options.Value.MaxFileSizeBytes)throw new ArgumentException("File is too large.");await target.WriteAsync(buffer.AsMemory(0,read),ct);}}
            if(length==0)throw new ArgumentException("File is empty.");var sample=new byte[Math.Min(length,512)];await using(var s=File.OpenRead(temp)){await s.ReadExactlyAsync(sample,ct);}ValidateSignature(ext,sample);
            string hash;await using(var s=File.OpenRead(temp)){hash=Convert.ToHexString(await SHA256.HashDataAsync(s,ct)).ToLowerInvariant();}
            var fileUid=Guid.NewGuid();var key=$"tenants/{tenant.TenantUid:N}/"+PatientFileNaming.StorageKey(patientUid,fileUid);var saved=false;
            try{await using(var s=File.OpenRead(temp)){await storage.SaveAsync(s,key,ct);}saved=true;var created=await repository.CreateAsync(patientUid,new(){OriginalFileName=name,StorageKey=key,ContentType=expected,FileSizeBytes=length,FileExtension=ext,Sha256Hash=hash,Description=input.Description,Category=input.Category},await actor.GetRequiredUserIdAsync(ct),ct);return Map(created,true);}
            catch{if(saved)try{await storage.DeleteAsync(key,ct);}catch(Exception cleanup){logger.LogError(cleanup,"Failed to clean up patient file {FileUid} for patient {PatientUid}.",fileUid,patientUid);}throw;}
        }
        finally{File.Delete(temp);}
    }
    public async Task<IReadOnlyList<PatientFileResponse>> GetByPatientUidAsync(Guid p,CancellationToken ct=default)=>(await repository.GetByPatientUidAsync(p,ct)).Select(x=>Map(x,false)).ToArray();
    public async Task<PatientFileResponse?> GetByUidAsync(Guid p,Guid f,CancellationToken ct=default)=>await repository.GetByUidAsync(p,f,ct)is{}x?Map(x,true):null;
    public async Task<PatientFileContent?> OpenContentAsync(Guid p,Guid f,CancellationToken ct=default){var x=await repository.GetByUidAsync(p,f,ct);if(x is null)return null;if(!await storage.ExistsAsync(x.StorageKey,ct)){logger.LogError("Stored content is missing for patient file {FileUid}.",f);return null;}return new(await storage.OpenReadAsync(x.StorageKey,ct),x.OriginalFileName,x.ContentType,x.FileSizeBytes);}
    private static PatientFileResponse Map(PatientFile x,bool details)=>new(x.FileUid,x.PatientUid,x.OriginalFileName,x.ContentType,x.FileSizeBytes,x.FileExtension,details?x.Sha256Hash:null,x.Description,x.Category,x.Status.ToString(),x.UploadedAtUtc,x.UploadedBy,x.RowVersion);
    private static void ValidateSignature(string e,byte[] b){bool ok=e switch{".pdf"=>b.AsSpan().StartsWith("%PDF"u8),".jpg" or ".jpeg"=>b.Length>=3&&b[0]==255&&b[1]==216&&b[2]==255,".png"=>b.AsSpan().StartsWith(new byte[]{137,80,78,71,13,10,26,10}),".txt"=>!b.Contains((byte)0),_=>false};if(!ok)throw new ArgumentException("File content does not match its type.");}
}
