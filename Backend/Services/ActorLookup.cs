using Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

/// <summary>The user who performed an action: id, full name and role as they were at that moment.</summary>
public sealed record ActorInfo(int UserId, string Name, string Role);

/// <summary>
/// Resolves "who did it" from the authenticated user id (taken from the JWT by the controller, never
/// from the request body). The name is copied onto the record so a later rename does not rewrite history.
/// </summary>
public static class ActorLookup
{
    public static async Task<ActorInfo?> FindAsync(CarRentalDbContext db, int? userId)
    {
        if (userId is not > 0)
            return null;
        return await db.Users
            .AsNoTracking()
            .Where(u => u.UserId == userId.Value)
            .Select(u => new ActorInfo(u.UserId, u.FullName, u.Role.RoleName))
            .FirstOrDefaultAsync();
    }
}
