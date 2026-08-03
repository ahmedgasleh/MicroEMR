using Microsoft.Extensions.Configuration;
using MicroEMR.Application.PatientFiles;

namespace MicroEMR.Infrastructure.PatientFiles;

public sealed class LocalPatientFileStorage : IPatientFileStorage
{
    private readonly string root;
    public LocalPatientFileStorage(IConfiguration configuration)
    {
        root = Path.GetFullPath(configuration["PatientFileStorage:LocalRootPath"]
            ?? Path.Combine(Path.GetTempPath(), "MicroEMR", "patient-files"));
        Directory.CreateDirectory(root);
    }
    public async Task SaveAsync(Stream content, string storageKey, CancellationToken cancellationToken = default)
    {
        var path = Resolve(storageKey); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        await content.CopyToAsync(target, cancellationToken);
    }
    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new FileStream(Resolve(storageKey), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true));
    public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(File.Exists(Resolve(storageKey)));
    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    { var path = Resolve(storageKey); if (File.Exists(path)) File.Delete(path); return Task.CompletedTask; }
    private string Resolve(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || Path.IsPathRooted(key)) throw new ArgumentException("Storage key is invalid.");
        var normalized = key.Replace('/', Path.DirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(root, normalized));
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Storage key is invalid.");
        return path;
    }
}
