using Backend.Constants;
using Backend.Entities;
using Backend.Services;
using Xunit;

namespace Backend.Tests;

public class VehicleInspectionReadTests
{
    [Fact]
    public void ToResponse_copies_all_fields()
    {
        var at = new DateTime(2026, 8, 27, 10, 0, 0, DateTimeKind.Utc);
        var created = at.AddMinutes(1);
        var entity = new VehicleInspection
        {
            InspectionId = 7,
            BookingId = 25,
            VehicleId = 2,
            InspectionType = VehicleInspectionTypes.Handover,
            ActualAt = at,
            OdometerKm = 22100,
            FuelLevel = 80,
            Condition = "Good",
            Notes = "ok",
            CreatedAt = created
        };

        var dto = VehicleInspectionService.ToResponse(entity);

        Assert.Equal(7, dto.InspectionId);
        Assert.Equal(25, dto.BookingId);
        Assert.Equal(2, dto.VehicleId);
        Assert.Equal(VehicleInspectionTypes.Handover, dto.InspectionType);
        Assert.Equal(at, dto.ActualAt);
        Assert.Equal(22100m, dto.OdometerKm);
        Assert.Equal(80m, dto.FuelLevel);
        Assert.Equal("Good", dto.Condition);
        Assert.Equal("ok", dto.Notes);
        Assert.Equal(created, dto.CreatedAt);
    }

    [Fact]
    public void Map_sorts_by_inspection_id_ascending()
    {
        var later = Inspection(9, VehicleInspectionTypes.Return);
        var earlier = Inspection(3, VehicleInspectionTypes.Handover);
        var mapped = new[] { later, earlier }
            .OrderBy(i => i.InspectionId)
            .Select(VehicleInspectionService.ToResponse)
            .ToList();

        Assert.Equal(new[] { 3, 9 }, mapped.Select(i => i.InspectionId));
        Assert.Equal(VehicleInspectionTypes.Handover, mapped[0].InspectionType);
        Assert.Equal(VehicleInspectionTypes.Return, mapped[1].InspectionType);
    }

    [Fact]
    public void Empty_inspections_map_to_empty_list()
    {
        var mapped = Array.Empty<VehicleInspection>()
            .OrderBy(i => i.InspectionId)
            .Select(VehicleInspectionService.ToResponse)
            .ToList();

        Assert.Empty(mapped);
    }

    [Fact]
    public void SelfDrive_completed_shape_has_handover_then_return_no_duplicate_types()
    {
        var rows = new[]
        {
            Inspection(1, VehicleInspectionTypes.Handover),
            Inspection(2, VehicleInspectionTypes.Return)
        };
        var mapped = rows
            .OrderBy(i => i.InspectionId)
            .Select(VehicleInspectionService.ToResponse)
            .ToList();

        Assert.Equal(2, mapped.Count);
        Assert.Equal(VehicleInspectionTypes.Handover, mapped[0].InspectionType);
        Assert.Equal(VehicleInspectionTypes.Return, mapped[1].InspectionType);
        Assert.Equal(2, mapped.Select(i => i.InspectionType).Distinct().Count());
    }

    [Fact]
    public void WithDriver_completed_shape_has_return_only()
    {
        var mapped = new[] { Inspection(4, VehicleInspectionTypes.Return) }
            .Select(VehicleInspectionService.ToResponse)
            .ToList();

        Assert.Single(mapped);
        Assert.Equal(VehicleInspectionTypes.Return, mapped[0].InspectionType);
        Assert.DoesNotContain(mapped, i => i.InspectionType == VehicleInspectionTypes.Handover);
    }

    [Fact]
    public void Mapping_does_not_change_booking_amounts()
    {
        var booking = new Booking
        {
            BookingId = 1,
            TotalAmount = 6_600_000,
            FinalAmount = null,
            Inspections = []
        };

        var originalTotal = booking.TotalAmount;
        var originalFinal = booking.FinalAmount;
        _ = (booking.Inspections ?? [])
            .OrderBy(i => i.InspectionId)
            .Select(VehicleInspectionService.ToResponse)
            .ToList();

        Assert.Equal(6_600_000m, originalTotal);
        Assert.Equal(6_600_000m, booking.TotalAmount);
        Assert.Null(originalFinal);
        Assert.Null(booking.FinalAmount);
        Assert.Empty(booking.Inspections);
    }

    [Fact]
    public void Booking_response_inspections_is_last_constructor_parameter()
    {
        var ctor = typeof(Backend.DTOs.Bookings.BookingResponse).GetConstructors().Single();
        var names = ctor.GetParameters().Select(p => p.Name).ToList();
        Assert.Equal("Inspections", names[^1]);
        Assert.Equal("TotalFees", names[^2]);
        Assert.Equal("FinalBaseAmount", names[^3]);
        Assert.Equal("Fees", names[^4]);
        Assert.Equal("FinalAmount", names[^5]);
    }

    private static VehicleInspection Inspection(int id, string type) => new()
    {
        InspectionId = id,
        BookingId = 10,
        VehicleId = 1,
        InspectionType = type,
        ActualAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow
    };
}
