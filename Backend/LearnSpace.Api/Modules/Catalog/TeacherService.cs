using LearnSpace.Api.Common.Errors;
using LearnSpace.Api.Common.Storage;
using LearnSpace.Api.Data;
using LearnSpace.Api.Modules.Identity;
using Microsoft.EntityFrameworkCore;

namespace LearnSpace.Api.Modules.Catalog;

/// <summary>Used by the Identity module so registering a teacher also creates the profile row.</summary>
public interface ITeacherService
{
    Task CreateEmptyProfileAsync(Guid userId);
}

public class TeacherService(
    AppDbContext db,
    IUserLookup users,
    IStorageService storage,
    UploadValidator uploads,
    CourseService courses) : ITeacherService
{
    public async Task CreateEmptyProfileAsync(Guid userId)
    {
        db.TeacherProfiles.Add(new TeacherProfile { UserId = userId });
        await db.SaveChangesAsync();
    }

    public async Task<List<TeacherDto>> ListAsync()
    {
        var profiles = await db.TeacherProfiles.AsNoTracking().ToListAsync();
        var names = await users.GetDisplayNamesAsync(profiles.Select(p => p.UserId));
        return profiles
            .Select(p => ToDto(p, names.GetValueOrDefault(p.UserId, "")))
            .OrderBy(t => t.DisplayName)
            .ToList();
    }

    public async Task<TeacherDetailDto> GetAsync(Guid teacherId)
    {
        var profile = await db.TeacherProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == teacherId)
            ?? throw AppException.NotFound("Teacher not found.");
        var names = await users.GetDisplayNamesAsync([teacherId]);
        var published = await courses.ListPublishedByTeacherAsync(teacherId);
        var dto = ToDto(profile, names.GetValueOrDefault(teacherId, ""));
        return new TeacherDetailDto(dto.Id, dto.DisplayName, dto.Bio, dto.AccentColor, dto.LogoUrl, published);
    }

    public async Task<TeacherDto> UpdateProfileAsync(Guid teacherId, UpdateProfileRequest request)
    {
        var profile = await GetOrCreateTrackedAsync(teacherId);
        profile.Bio = request.Bio?.Trim() ?? "";
        profile.AccentColor = request.AccentColor.ToUpperInvariant();
        await db.SaveChangesAsync();

        var names = await users.GetDisplayNamesAsync([teacherId]);
        return ToDto(profile, names.GetValueOrDefault(teacherId, ""));
    }

    public async Task<TeacherDto> UploadLogoAsync(Guid teacherId, IFormFile file)
    {
        uploads.Validate(file, UploadKinds.Logo);
        var profile = await GetOrCreateTrackedAsync(teacherId);

        // Save the file first, then the DB row. If the save fails nothing is written.
        await using var stream = file.OpenReadStream();
        var newKey = await storage.SaveAsync(stream, file.FileName);

        var oldKey = profile.LogoKey;
        try
        {
            profile.LogoKey = newKey;
            await db.SaveChangesAsync();
        }
        catch
        {
            await storage.TryDeleteAsync(newKey);
            throw;
        }

        if (oldKey is not null) await storage.TryDeleteAsync(oldKey);

        var names = await users.GetDisplayNamesAsync([teacherId]);
        return ToDto(profile, names.GetValueOrDefault(teacherId, ""));
    }

    public async Task<(Stream Content, string ContentType)> OpenLogoAsync(Guid teacherId)
    {
        var key = await db.TeacherProfiles
            .Where(p => p.UserId == teacherId)
            .Select(p => p.LogoKey)
            .FirstOrDefaultAsync();

        if (key is null) throw AppException.NotFound("This teacher has no logo.");

        var stream = await storage.OpenReadAsync(key);
        return (stream, UploadValidator.ContentTypeFor(key));
    }

    private async Task<TeacherProfile> GetOrCreateTrackedAsync(Guid teacherId)
    {
        var profile = await db.TeacherProfiles.FirstOrDefaultAsync(p => p.UserId == teacherId);
        if (profile is null)
        {
            profile = new TeacherProfile { UserId = teacherId };
            db.TeacherProfiles.Add(profile);
        }
        return profile;
    }

    private static TeacherDto ToDto(TeacherProfile p, string displayName) => new(
        p.UserId, displayName, p.Bio, p.AccentColor,
        p.LogoKey is null ? null : $"/api/teachers/{p.UserId}/logo");
}
