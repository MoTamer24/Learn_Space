namespace LearnSpace.Api.Common.Storage;

public interface IStorageService
{
    /// <summary>Saves the stream and returns the storage key (never a path or URL).</summary>
    Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken ct = default);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default);

    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}

public static class StorageServiceExtensions
{
    /// <summary>Best-effort cleanup: never throws.</summary>
    public static async Task TryDeleteAsync(this IStorageService storage, string storageKey)
    {
        try { await storage.DeleteAsync(storageKey); }
        catch { /* ignore: an orphan file is better than hiding the real error */ }
    }
}
