using LearnSpace.Api.Common.Errors;
using LearnSpace.Api.Common.Storage;
using LearnSpace.Api.Data;
using LearnSpace.Api.Modules.Catalog;
using LearnSpace.Api.Modules.Enrollments;
using Microsoft.EntityFrameworkCore;

namespace LearnSpace.Api.Modules.Content;

public class MaterialService(
    AppDbContext db,
    ICourseService courses,
    IEnrollmentService enrollments,
    IStorageService storage,
    UploadValidator uploads)
{
    public async Task<MaterialDto> UploadAsync(Guid teacherId, Guid courseId, string title, MaterialType type, IFormFile file)
    {
        await courses.RequireOwnerAsync(teacherId, courseId);

        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
            throw AppException.BadRequest("Title is required and must be at most 200 characters.");

        uploads.Validate(file, type == MaterialType.Video ? UploadKinds.Video : UploadKinds.Document);

        // Save the file first, then the DB row. If the save fails nothing is written.
        await using var stream = file.OpenReadStream();
        var key = await storage.SaveAsync(stream, file.FileName);

        try
        {
            var lastPosition = await db.Materials
                .Where(m => m.CourseId == courseId)
                .MaxAsync(m => (int?)m.Position) ?? 0;

            var material = new Material
            {
                CourseId = courseId,
                Title = title.Trim(),
                Type = type,
                StorageKey = key,
                FileName = UploadValidator.CleanFileName(file.FileName),
                ContentType = UploadValidator.ContentTypeFor(file.FileName),
                Position = lastPosition + 1
            };
            db.Materials.Add(material);
            await db.SaveChangesAsync();
            return ToDto(material);
        }
        catch
        {
            await storage.TryDeleteAsync(key);
            throw;
        }
    }

    public async Task<List<MaterialDto>> ListAsync(Guid userId, Guid courseId)
    {
        await enrollments.RequireCanViewCourseAsync(userId, courseId);

        var materials = await db.Materials.AsNoTracking()
            .Where(m => m.CourseId == courseId)
            .OrderBy(m => m.Position)
            .ToListAsync();

        return materials.Select(ToDto).ToList();
    }

    public async Task<(Stream Content, string ContentType)> OpenAsync(Guid userId, Guid materialId)
    {
        var material = await db.Materials.AsNoTracking().FirstOrDefaultAsync(m => m.Id == materialId)
            ?? throw AppException.NotFound("Material not found.");

        await enrollments.RequireCanViewCourseAsync(userId, material.CourseId);

        var stream = await storage.OpenReadAsync(material.StorageKey);
        return (stream, material.ContentType);
    }

    private static MaterialDto ToDto(Material m)
        => new(m.Id, m.CourseId, m.Title, m.Type, m.FileName, m.ContentType, m.Position);
}
