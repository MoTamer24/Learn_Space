using LearnSpace.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LearnSpace.Api.Modules.Identity;

/// <summary>Lets other modules turn user ids into display names without touching ApplicationUser.</summary>
public interface IUserLookup
{
    Task<Dictionary<Guid, string>> GetDisplayNamesAsync(IEnumerable<Guid> userIds);
}

public class UserLookup(AppDbContext db) : IUserLookup
{
    public async Task<Dictionary<Guid, string>> GetDisplayNamesAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();

        return await db.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName);
    }
}
