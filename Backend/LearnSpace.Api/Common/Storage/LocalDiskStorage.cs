using System.Text.RegularExpressions;
using LearnSpace.Api.Common.Errors;

namespace LearnSpace.Api.Common.Storage;

public partial class LocalDiskStorage : IStorageService
{
    private readonly string _root;

    public LocalDiskStorage(IConfiguration config, IHostEnvironment env)
    {
        var configured = config["Storage:RootPath"] ?? "storage";
        _root = Path.IsPathRooted(configured) ? configured : Path.Combine(env.ContentRootPath, configured);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken ct = default)
    {
        // Key = new Guid + the original extension. The user's file name is never used as a path.
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (!SafeExtension().IsMatch(ext)) ext = "";

        var key = Guid.NewGuid().ToString("N") + ext;
        var path = Resolve(key);

        try
        {
            await using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await content.CopyToAsync(target, ct);
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path);
            throw;
        }

        return key;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        var path = Resolve(storageKey);
        if (!File.Exists(path)) throw AppException.NotFound("The file is missing from storage.");

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var path = Resolve(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string Resolve(string key)
    {
        // Keys are plain file names. Reject anything that looks like a path.
        if (string.IsNullOrWhiteSpace(key) || key != Path.GetFileName(key))
            throw new ArgumentException("Invalid storage key.", nameof(key));
        return Path.Combine(_root, key);
    }

    [GeneratedRegex(@"^\.[a-z0-9]{1,10}$")]
    private static partial Regex SafeExtension();
}
