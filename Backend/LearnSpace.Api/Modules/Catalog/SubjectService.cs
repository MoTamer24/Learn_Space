using LearnSpace.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LearnSpace.Api.Modules.Catalog;

public interface ICatalogLookup
{
    Task<bool> SubjectLevelExistsAsync(Guid subjectLevelId);
}

public class SubjectService(AppDbContext db) : ICatalogLookup
{
    public Task<bool> SubjectLevelExistsAsync(Guid subjectLevelId) => db.SubjectLevels.AnyAsync(x => x.Id == subjectLevelId);
    public Task<List<Subject>> GetSubjectsAsync(CancellationToken ct = default) => db.Subjects.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
    public Task<List<Level>> GetLevelsAsync(CancellationToken ct = default) => db.Levels.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
    public Task<List<SubjectLevel>> GetSubjectLevelsAsync(CancellationToken ct = default) => db.SubjectLevels.AsNoTracking().ToListAsync(ct);
    public async Task<Subject> AddSubjectAsync(string name, CancellationToken ct = default)
    { if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Name is required."); var item = new Subject { Name = name.Trim() }; db.Subjects.Add(item); await db.SaveChangesAsync(ct); return item; }
    public async Task<Level> AddLevelAsync(string name, CancellationToken ct = default)
    { if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Name is required."); var item = new Level { Name = name.Trim() }; db.Levels.Add(item); await db.SaveChangesAsync(ct); return item; }
    public async Task<SubjectLevel> AddSubjectLevelAsync(Guid subjectId, Guid levelId, CancellationToken ct = default)
    { if (!await db.Subjects.AnyAsync(x => x.Id == subjectId, ct) || !await db.Levels.AnyAsync(x => x.Id == levelId, ct)) throw new KeyNotFoundException("Subject or level not found."); if (await db.SubjectLevels.AnyAsync(x => x.SubjectId == subjectId && x.LevelId == levelId, ct)) throw new InvalidOperationException("Subject and level are already linked."); var item = new SubjectLevel { SubjectId = subjectId, LevelId = levelId }; db.SubjectLevels.Add(item); await db.SaveChangesAsync(ct); return item; }
}
