using LearnSpace.Api.Common.Errors;
using Microsoft.AspNetCore.StaticFiles;

namespace LearnSpace.Api.Common.Storage;

public static class UploadKinds
{
    public const string Video = "Video";
    public const string Document = "Document";
    public const string TaskFile = "TaskFile";
    public const string Logo = "Logo";
}

/// <summary>Checks file extension and size against config section Storage:Uploads:{kind}.</summary>
public class UploadValidator(IConfiguration config)
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public void Validate(IFormFile? file, string kind)
    {
        if (file is null || file.Length == 0)
            throw AppException.BadRequest("A non-empty file is required.");

        var section = config.GetSection($"Storage:Uploads:{kind}");
        var allowed = section.GetSection("Extensions").Get<string[]>() ?? [];
        var maxMb = section.GetValue<int?>("MaxMb") ?? 20;

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext))
            throw AppException.BadRequest($"File type '{ext}' is not allowed. Allowed: {string.Join(", ", allowed)}.");

        if (file.Length > maxMb * 1024L * 1024L)
            throw AppException.BadRequest($"The file is too large. Maximum size is {maxMb} MB.");
    }

    /// <summary>Content type comes from the extension, not from what the client claims.</summary>
    public static string ContentTypeFor(string fileName)
        => ContentTypes.TryGetContentType(fileName, out var type) ? type : "application/octet-stream";

    public static string CleanFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        return name.Length > 255 ? name[^255..] : name;
    }
}
