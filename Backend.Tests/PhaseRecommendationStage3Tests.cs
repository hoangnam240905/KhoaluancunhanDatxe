using Backend.Constants;
using Backend.DTOs.Bookings;
using Backend.DTOs.Recommendations;
using Backend.Entities;
using Backend.Options;
using Backend.Services;
using Backend.Services.Recommendation;
using Backend.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Tests;

/// <summary>
/// plan1.md (gợi ý xe) — Giai đoạn 3: server-side BOOK/RENT/CANCEL + rental history sent to V7.
/// </summary>
public class PhaseRecommendationStage3Tests
{
    private const int Customer = 3;
    private const int Accent = 2;

    // ---------- Server-side logging ----------

    [Fact]
    public async Task Booking_with_vehicle_logs_one_BOOK_with_booking_id()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var withVehicle = await s.Bookings.CreateBookingAsync(Customer, Request(RentalModes.SelfDrive, Accent));
        var typeOnly = await s.Bookings.CreateBookingAsync(Customer, Request(RentalModes.SelfDrive, null, hoursAhead: 200));

        var rows = Interactions(iso, Customer);
        var book = Assert.Single(rows, i => i.InteractionType == InteractionLog.Book);
        Assert.Equal(withVehicle!.BookingId, book.BookingId);
        Assert.Equal(Accent, book.VehicleId);
        Assert.DoesNotContain(rows, i => i.BookingId == typeOnly!.BookingId);
    }

    [Fact]
    public async Task Self_drive_handover_logs_RENT_for_actual_vehicle()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var id = await AssignedAsync(iso, s, RentalModes.SelfDrive);

        var handover = await s.Dispatch.HandoverSelfDriveAsync(id, 2, TestDispatchReady.InspectionForBooking(iso.Db, id));

        Assert.Null(handover.Error);
        var rent = Assert.Single(Interactions(iso, Customer), i => i.InteractionType == InteractionLog.Rent);
        Assert.Equal((id, Accent), (rent.BookingId!.Value, rent.VehicleId));
    }

    [Fact]
    public async Task Driver_start_logs_RENT_for_trip_vehicle()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var id = await AssignedAsync(iso, s, RentalModes.WithDriver);
        var assignmentId = iso.Db.TripAssignments.Single(t => t.BookingId == id).AssignmentId;

        Assert.True(await s.Drivers.AcceptTripAsync(6, assignmentId));
        Assert.True(await s.Drivers.StartTripAsync(6, assignmentId));

        var rent = Assert.Single(Interactions(iso, Customer), i => i.InteractionType == InteractionLog.Rent);
        Assert.Equal((id, Accent), (rent.BookingId!.Value, rent.VehicleId));
    }

    [Fact]
    public async Task Cancelling_a_booking_with_vehicle_logs_CANCEL()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var created = await s.Bookings.CreateBookingAsync(Customer, Request(RentalModes.SelfDrive, Accent));

        await s.Bookings.UpdateStatusAsync(created!.BookingId, BookingStatuses.Cancelled, 2, "test");

        Assert.Single(Interactions(iso, Customer), i =>
            i.InteractionType == InteractionLog.Cancel && i.BookingId == created.BookingId);
    }

    [Fact]
    public async Task Same_event_for_same_booking_is_recorded_once()
    {
        using var iso = new IsolatedCarRentalDb();
        var booking = iso.AddDepositBooking(0);

        Assert.True(await InteractionLog.TryRecordAsync(iso.Db, Customer, Accent, InteractionLog.Rent, booking.BookingId));
        Assert.False(await InteractionLog.TryRecordAsync(iso.Db, Customer, Accent, InteractionLog.Rent, booking.BookingId));
        Assert.Single(Interactions(iso, Customer), i => i.InteractionType == InteractionLog.Rent);
    }

    [Fact]
    public async Task Logging_failure_is_swallowed_and_context_stays_usable()
    {
        using var iso = new IsolatedCarRentalDb();
        var booking = iso.AddDepositBooking(0);

        // Unknown vehicle -> FK violation inside the log write.
        Assert.False(await InteractionLog.TryRecordAsync(iso.Db, Customer, 999_999, InteractionLog.Rent, booking.BookingId));

        booking.Notes = "still saves";
        await iso.Db.SaveChangesAsync();
        Assert.Equal("still saves", iso.Db.Bookings.Find(booking.BookingId)!.Notes);
        Assert.Empty(Interactions(iso, Customer));
    }

    // ---------- History built for V7 ----------

    [Fact]
    public async Task History_weights_rent_over_book_drops_cancelled_and_ignores_client_events()
    {
        using var iso = new IsolatedCarRentalDb();
        var now = new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc);
        var rented = iso.AddDepositBooking(0).BookingId;
        var booked = iso.AddDepositBooking(0).BookingId;
        var cancelled = iso.AddDepositBooking(0).BookingId;
        var old = iso.AddDepositBooking(0).BookingId;
        Add(iso, rented, 1, InteractionLog.Book, now);
        Add(iso, rented, 1, InteractionLog.Rent, now);           // RENT supersedes BOOK of same booking
        Add(iso, booked, Accent, InteractionLog.Book, now);       // only booked -> half weight
        Add(iso, cancelled, 3, InteractionLog.Book, now);
        Add(iso, cancelled, 3, InteractionLog.Cancel, now);       // cancelled -> dropped
        Add(iso, old, 1, InteractionLog.Rent, now.AddDays(-90));  // one half-life old
        Add(iso, null, 1, InteractionLog.ClickBook, now);         // client intent -> ignored
        Add(iso, null, 1, InteractionLog.Book, now);              // legacy row without booking -> ignored

        var carIds = new Dictionary<int, string> { [1] = "car_vios" };
        var history = await RentalHistoryBuilder.BuildAsync(
            iso.Db, Customer, carIds, new RecommendationServiceOptions(), now);

        Assert.Equal(3, history.Count);
        Assert.Contains(history, h => h is { CarId: "car_vios", Weight: 1.0 });
        Assert.Contains(history, h => h is { CarId: null, Weight: 0.5 } && h.Seats > 0);
        Assert.Contains(history, h => h is { CarId: "car_vios", Weight: 0.5 });
    }

    [Fact]
    public async Task Recommendation_request_carries_history_only_when_customer_has_one()
    {
        using var iso = new IsolatedCarRentalDb();
        var python = new CapturingPython();
        var service = Mapping(iso, python);

        await service.GetRecommendationsForCustomerAsync(Customer, topK: 5);
        Assert.Null(python.Last!.History);           // cold-start: pure V7
        Assert.Equal(0, python.Last.HistoryWeight);

        var booking = iso.AddDepositBooking(0);
        Add(iso, booking.BookingId, Accent, InteractionLog.Rent, DateTime.UtcNow);
        await service.GetRecommendationsForCustomerAsync(Customer, topK: 5);

        var item = Assert.Single(python.Last!.History!);
        Assert.Equal(1.0, item.Weight, precision: 3);
        Assert.Equal(0.2, python.Last.HistoryWeight);
    }

    // ---------- helpers ----------

    private sealed record Svc(BookingService Bookings, DispatchService Dispatch, DriverService Drivers);

    private static Svc Services(IsolatedCarRentalDb iso)
    {
        var pricing = new PricingService();
        var bookings = new BookingService(iso.Db, pricing);
        var inspections = new VehicleInspectionService(iso.Db);
        var fees = new BookingFeeService(iso.Db, pricing);
        var schedule = new ScheduleConflictService(iso.Db);
        var drivers = new DriverService(iso.Db, bookings, inspections, fees, schedule);
        return new Svc(bookings, new DispatchService(iso.Db, bookings, drivers, inspections, fees, schedule), drivers);
    }

    private static CreateBookingRequest Request(string mode, int? vehicleId, double hoursAhead = 48)
    {
        var start = VietnamTime.Now.AddHours(hoursAhead);
        start = new DateTime(start.Year, start.Month, start.Day, start.Hour, 0, 0);
        return new CreateBookingRequest(1, "A", "B", null, null, null, null, start, start.AddHours(10), 20, null,
            mode, mode == RentalModes.SelfDrive ? vehicleId : null);
    }

    private static async Task<int> AssignedAsync(IsolatedCarRentalDb iso, Svc s, string mode)
    {
        var created = await s.Bookings.CreateBookingAsync(Customer, Request(mode, Accent));
        await s.Dispatch.ConfirmBookingAsync(created!.BookingId, 2);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);
        var assigned = await s.Dispatch.AssignTripAsync(
            created.BookingId, new AssignTripRequest(mode == RentalModes.WithDriver ? 6 : null, Accent), 2);
        Assert.Null(assigned.Error);
        return created.BookingId;
    }

    private static List<CustomerVehicleInteraction> Interactions(IsolatedCarRentalDb iso, int customerId)
        => iso.Db.CustomerVehicleInteractions.Where(i => i.CustomerId == customerId).ToList();

    private static void Add(IsolatedCarRentalDb iso, int? bookingId, int vehicleId, string type, DateTime at)
    {
        iso.Db.CustomerVehicleInteractions.Add(new CustomerVehicleInteraction
        {
            CustomerId = Customer, VehicleId = vehicleId, InteractionType = type, BookingId = bookingId, CreatedAt = at
        });
        iso.Db.SaveChanges();
    }

    private static RecommendationMappingService Mapping(IsolatedCarRentalDb iso, IRecommendationService python)
        => new(iso.Db, python,
            Microsoft.Extensions.Options.Options.Create(new RecommendationServiceOptions()),
            new V7CarIdVehicleMapStore(),
            NullLogger<RecommendationMappingService>.Instance);

    private sealed class CapturingPython : IRecommendationService
    {
        public PythonRecommendationRequest? Last { get; private set; }

        public Task<(PythonRecommendationResponse? Data, string? Error, int StatusCode)> GetRecommendationsAsync(
            PythonRecommendationRequest request, CancellationToken cancellationToken = default)
        {
            Last = request;
            var items = new List<PythonRecommendationItem>
            {
                new(CarId: "car_x", Make: "Toyota", Model: "Vios", Segment: "Sedan", FinalScore: 0.5)
            };
            return Task.FromResult<(PythonRecommendationResponse?, string?, int)>(
                (new PythonRecommendationResponse(true, "Sedan", items, 1), null, StatusCodes.Status200OK));
        }
    }
}
