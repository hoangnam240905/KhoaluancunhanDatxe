using Backend.Data;
using Backend.DTOs.Vehicles;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class VehicleImageService(CarRentalDbContext db)
{
    public async Task<(List<VehicleImageResponse>? Data, string? Error, int Status)> GetByVehicleAsync(int vehicleId)
    {
        if (!await db.Vehicles.AnyAsync(v => v.VehicleId == vehicleId))
            return (null, "Không tìm thấy xe.", StatusCodes.Status404NotFound);

        var rows = await db.VehicleImages.AsNoTracking()
            .Where(i => i.VehicleId == vehicleId)
            .OrderBy(i => i.DisplayOrder)
            .ThenBy(i => i.VehicleImageId)
            .ToListAsync();
        return (rows.Select(Map).ToList(), null, StatusCodes.Status200OK);
    }

    /// <summary>Primary photo URL per vehicle (vehicles without one are absent).</summary>
    public async Task<IReadOnlyDictionary<int, string>> GetPrimaryUrlsAsync(IEnumerable<int> vehicleIds)
    {
        var ids = vehicleIds.Distinct().ToList();
        var rows = await db.VehicleImages.AsNoTracking()
            .Where(i => i.IsPrimary && ids.Contains(i.VehicleId))
            .OrderBy(i => i.DisplayOrder).ThenByDescending(i => i.VehicleImageId)
            .Select(i => new { i.VehicleId, i.ImageUrl })
            .ToListAsync();
        return rows.GroupBy(r => r.VehicleId).ToDictionary(g => g.Key, g => g.First().ImageUrl);
    }

    public async Task<(VehicleImageResponse? Data, string? Error, int Status)> GetByIdAsync(
        int vehicleId, int imageId)
    {
        var row = await db.VehicleImages.AsNoTracking()
            .FirstOrDefaultAsync(i => i.VehicleId == vehicleId && i.VehicleImageId == imageId);
        if (row is null)
            return (null, "Không tìm thấy ảnh xe.", StatusCodes.Status404NotFound);
        return (Map(row), null, StatusCodes.Status200OK);
    }

    public async Task<(VehicleImageResponse? Data, string? Error, int Status)> CreateAsync(
        int vehicleId, CreateVehicleImageRequest request)
    {
        if (!await db.Vehicles.AnyAsync(v => v.VehicleId == vehicleId))
            return (null, "Không tìm thấy xe.", StatusCodes.Status404NotFound);

        var url = request.ImageUrl?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(url))
            return (null, "Vui lòng nhập đường dẫn ảnh.", StatusCodes.Status400BadRequest);
        if (request.DisplayOrder < 0)
            return (null, "Thứ tự hiển thị không được âm.", StatusCodes.Status400BadRequest);

        var row = new VehicleImage
        {
            VehicleId = vehicleId,
            ImageUrl = url,
            ImageType = TrimOrNull(request.ImageType),
            IsPrimary = request.IsPrimary,
            DisplayOrder = request.DisplayOrder,
            CreatedAt = DateTime.UtcNow
        };
        if (row.IsPrimary) await ClearPrimaryAsync(vehicleId, exceptImageId: null);
        db.VehicleImages.Add(row);
        await db.SaveChangesAsync();
        return (Map(row), null, StatusCodes.Status201Created);
    }

    public async Task<(VehicleImageResponse? Data, string? Error, int Status)> UpdateAsync(
        int vehicleId, int imageId, UpdateVehicleImageRequest request)
    {
        var row = await db.VehicleImages
            .FirstOrDefaultAsync(i => i.VehicleId == vehicleId && i.VehicleImageId == imageId);
        if (row is null)
            return (null, "Không tìm thấy ảnh xe.", StatusCodes.Status404NotFound);

        var url = request.ImageUrl?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(url))
            return (null, "Vui lòng nhập đường dẫn ảnh.", StatusCodes.Status400BadRequest);
        if (request.DisplayOrder < 0)
            return (null, "Thứ tự hiển thị không được âm.", StatusCodes.Status400BadRequest);

        row.ImageUrl = url;
        row.ImageType = TrimOrNull(request.ImageType);
        row.IsPrimary = request.IsPrimary;
        row.DisplayOrder = request.DisplayOrder;
        if (row.IsPrimary) await ClearPrimaryAsync(vehicleId, exceptImageId: imageId);
        await db.SaveChangesAsync();
        return (Map(row), null, StatusCodes.Status200OK);
    }

    public async Task<(bool Ok, string? Error, int Status)> DeleteAsync(int vehicleId, int imageId)
    {
        var row = await db.VehicleImages
            .FirstOrDefaultAsync(i => i.VehicleId == vehicleId && i.VehicleImageId == imageId);
        if (row is null)
            return (false, "Không tìm thấy ảnh xe.", StatusCodes.Status404NotFound);

        db.VehicleImages.Remove(row);
        await db.SaveChangesAsync();
        return (true, null, StatusCodes.Status204NoContent);
    }

    /// <summary>One primary photo per vehicle: making an image primary demotes the others.</summary>
    private async Task ClearPrimaryAsync(int vehicleId, int? exceptImageId)
    {
        var others = await db.VehicleImages
            .Where(i => i.VehicleId == vehicleId && i.IsPrimary && i.VehicleImageId != exceptImageId)
            .ToListAsync();
        foreach (var other in others) other.IsPrimary = false;
    }

    private static VehicleImageResponse Map(VehicleImage i) => new(
        i.VehicleImageId, i.VehicleId, i.ImageUrl, i.ImageType, i.IsPrimary, i.DisplayOrder, i.CreatedAt);

    private static string? TrimOrNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
