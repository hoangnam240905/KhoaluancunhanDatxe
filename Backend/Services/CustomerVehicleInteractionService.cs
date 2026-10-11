using Backend.Data;
using Backend.DTOs.Interactions;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class CustomerVehicleInteractionService(CarRentalDbContext db)
{
    public static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "VIEW", "SEARCH", "FAVORITE", "COMPARE", "BOOK", "CANCEL", "RENT", "REVIEW"
    };

    public async Task<(CustomerVehicleInteractionResponse? Data, string? Error, int Status)> CreateAsync(
        int customerId, CreateCustomerVehicleInteractionRequest request)
    {
        if (!await db.Customers.AnyAsync(c => c.CustomerId == customerId))
            return (null, "Không tìm thấy khách hàng.", StatusCodes.Status404NotFound);
        if (!await db.Vehicles.AnyAsync(v => v.VehicleId == request.VehicleId))
            return (null, "Không tìm thấy xe.", StatusCodes.Status404NotFound);

        var type = request.InteractionType?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(type))
            return (null, "Vui lòng nhập loại tương tác.", StatusCodes.Status400BadRequest);
        if (!AllowedTypes.Contains(type))
            return (null, "Loại tương tác không hợp lệ.", StatusCodes.Status400BadRequest);

        var row = new CustomerVehicleInteraction
        {
            CustomerId = customerId,
            VehicleId = request.VehicleId,
            InteractionType = type.ToUpperInvariant(),
            CreatedAt = DateTime.UtcNow
        };
        db.CustomerVehicleInteractions.Add(row);
        await db.SaveChangesAsync();
        return (Map(row), null, StatusCodes.Status201Created);
    }

    public async Task<(List<CustomerVehicleInteractionResponse>? Data, string? Error, int Status)> GetByCustomerAsync(
        int customerId, string? interactionType = null)
    {
        if (!await db.Customers.AnyAsync(c => c.CustomerId == customerId))
            return (null, "Không tìm thấy khách hàng.", StatusCodes.Status404NotFound);

        var query = db.CustomerVehicleInteractions.AsNoTracking()
            .Where(i => i.CustomerId == customerId);
        if (!string.IsNullOrWhiteSpace(interactionType))
        {
            var type = interactionType.Trim().ToUpperInvariant();
            query = query.Where(i => i.InteractionType == type);
        }

        var rows = await query
            .OrderByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.InteractionId)
            .ToListAsync();
        return (rows.Select(Map).ToList(), null, StatusCodes.Status200OK);
    }

    public async Task<(List<CustomerVehicleInteractionResponse>? Data, string? Error, int Status)> GetByVehicleAsync(
        int vehicleId, string? interactionType = null)
    {
        if (!await db.Vehicles.AnyAsync(v => v.VehicleId == vehicleId))
            return (null, "Không tìm thấy xe.", StatusCodes.Status404NotFound);

        var query = db.CustomerVehicleInteractions.AsNoTracking()
            .Where(i => i.VehicleId == vehicleId);
        if (!string.IsNullOrWhiteSpace(interactionType))
        {
            var type = interactionType.Trim().ToUpperInvariant();
            query = query.Where(i => i.InteractionType == type);
        }

        var rows = await query
            .OrderByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.InteractionId)
            .ToListAsync();
        return (rows.Select(Map).ToList(), null, StatusCodes.Status200OK);
    }

    private static CustomerVehicleInteractionResponse Map(CustomerVehicleInteraction i) => new(
        i.InteractionId, i.CustomerId, i.VehicleId, i.InteractionType, i.CreatedAt);
}
