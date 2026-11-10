using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Bookings;
using Backend.DTOs.Contracts;
using Backend.DTOs.Payments;
using Backend.DTOs.Vehicles;
using Backend.Entities;

namespace Backend.Tests;

internal static class TestDispatchReady
{
    public static void EnsureSignedAndPaid(CarRentalDbContext db, int bookingId)
    {
        var booking = db.Bookings.Find(bookingId)
            ?? throw new InvalidOperationException($"Missing booking {bookingId}.");

        var contract = db.Contracts.FirstOrDefault(c => c.BookingId == bookingId);
        if (contract is null)
        {
            db.Contracts.Add(new Contract
            {
                BookingId = bookingId,
                ContractNumber = $"CTR-{bookingId:D6}",
                Status = ContractStatuses.Signed,
                CustomerId = booking.CustomerId,
                CustomerName = "Test",
                VehicleTypeName = "Test",
                RentalMode = booking.RentalMode,
                PickupAddress = booking.PickupAddress,
                DropoffAddress = booking.DropoffAddress,
                StartDate = booking.StartDate,
                EndDate = booking.EndDate,
                TotalAmount = booking.TotalAmount,
                DepositAmount = booking.QuotedDepositAmount,
                CreatedAt = DateTime.UtcNow,
                SignedAt = DateTime.UtcNow
            });
        }
        else if (contract.Status != ContractStatuses.Signed)
        {
            contract.Status = ContractStatuses.Signed;
            contract.SignedAt = DateTime.UtcNow;
        }

        var paid = db.Payments.FirstOrDefault(p =>
            p.BookingId == bookingId
            && p.PaymentType == PaymentTypes.Deposit
            && p.Status == PaymentStatuses.Paid);
        if (paid is null)
        {
            var pending = db.Payments.FirstOrDefault(p =>
                p.BookingId == bookingId
                && p.PaymentType == PaymentTypes.Deposit
                && p.Status == PaymentStatuses.Pending);
            if (pending is not null)
            {
                pending.Status = PaymentStatuses.Paid;
                pending.PaidAt = DateTime.UtcNow;
            }
            else
            {
                db.Payments.Add(new Payment
                {
                    BookingId = bookingId,
                    PaymentType = PaymentTypes.Deposit,
                    Amount = booking.QuotedDepositAmount ?? 100_000m,
                    Method = PaymentMethods.Cash,
                    Status = PaymentStatuses.Paid,
                    PaidAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        db.SaveChanges();
    }

    public static VehicleConditionRequest Inspection(
        int odometerKm,
        decimal fuel = 50m,
        string? notes = null,
        string exterior = "Ngoại thất tốt",
        string technical = "Kỹ thuật tốt")
        => new(odometerKm, fuel, exterior, notes, exterior, technical, OriginalLicenseChecked: true);

    public static VehicleConditionRequest InspectionForVehicle(
        CarRentalDbContext db,
        int vehicleId,
        int extraKm = 0,
        decimal fuel = 50m,
        string? notes = null)
    {
        var km = db.Vehicles.Find(vehicleId)!.CurrentKm + extraKm;
        return Inspection(km, fuel, notes);
    }

    public static VehicleConditionRequest InspectionForBooking(
        CarRentalDbContext db,
        int bookingId,
        int extraKm = 0,
        decimal fuel = 50m,
        string? notes = null)
    {
        var booking = db.Bookings.Find(bookingId)
            ?? throw new InvalidOperationException($"Missing booking {bookingId}.");
        var vehicleId = booking.AssignedVehicleId
            ?? db.TripAssignments.FirstOrDefault(t => t.BookingId == bookingId)?.VehicleId
            ?? throw new InvalidOperationException($"Booking {bookingId} has no assigned vehicle.");
        return InspectionForVehicle(db, vehicleId, extraKm, fuel, notes);
    }

    /// <summary>
    /// With-driver trips need the dispatcher's pre-handover check before the driver can start
    /// (plan1.md Phần A). Writes one at the vehicle's current km unless the booking already has it.
    /// </summary>
    public static void EnsurePreHandover(CarRentalDbContext db, int assignmentId)
    {
        var trip = db.TripAssignments.Find(assignmentId)
            ?? throw new InvalidOperationException($"Missing assignment {assignmentId}.");
        if (db.VehicleInspections.Any(i => i.BookingId == trip.BookingId && i.InspectionType == VehicleInspectionTypes.PreHandover))
            return;
        var km = db.Vehicles.Find(trip.VehicleId)!.CurrentKm;
        db.VehicleInspections.Add(new VehicleInspection
        {
            BookingId = trip.BookingId,
            VehicleId = trip.VehicleId,
            InspectionType = VehicleInspectionTypes.PreHandover,
            ActualAt = DateTime.UtcNow,
            OdometerKm = km,
            FuelLevel = 50m,
            Condition = "Ngoại thất tốt",
            ExteriorCondition = "Ngoại thất tốt",
            TechnicalCondition = "Kỹ thuật tốt",
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = 2,
            CreatedByName = "Trần Thị Điều Phối",
            CreatedByRole = RoleNames.Dispatcher
        });
        db.SaveChanges();
    }

    /// <summary>Same through the API, as the dispatcher.</summary>
    public static async Task EnsurePreHandoverHttpAsync(HttpClient client, string dispatcherToken, int bookingId)
    {
        var bookingResponse = await client.SendAsync(Authed(HttpMethod.Get, $"/api/bookings/{bookingId}", dispatcherToken));
        bookingResponse.EnsureSuccessStatusCode();
        var booking = await bookingResponse.Content.ReadFromJsonAsync<BookingResponse>();
        var vehicle = await client.GetFromJsonAsync<VehicleResponse>($"/api/vehicles/{booking!.AssignedVehicle!.VehicleId}");
        var saved = await client.SendAsync(Authed(HttpMethod.Post, $"/api/dispatch/bookings/{bookingId}/pre-handover",
            dispatcherToken, JsonContent.Create(Inspection(vehicle!.CurrentKm))));
        saved.EnsureSuccessStatusCode();
    }

    public static async Task EnsureSignedAndPaidHttpAsync(HttpClient client, string customerToken, int bookingId)
    {
        var contractResponse = await client.SendAsync(Authed(
            HttpMethod.Post, $"/api/bookings/{bookingId}/contract", customerToken));
        contractResponse.EnsureSuccessStatusCode();
        var contract = await contractResponse.Content.ReadFromJsonAsync<ContractResponse>();
        var sign = await client.SendAsync(Authed(
            HttpMethod.Post, $"/api/contracts/{contract!.ContractId}/simulate-sign", customerToken));
        sign.EnsureSuccessStatusCode();

        var pay = await client.SendAsync(Authed(
            HttpMethod.Post, "/api/payments", customerToken,
            JsonContent.Create(new CreatePaymentRequest(
                bookingId, PaymentTypes.Deposit, PaymentMethods.Cash, VehicleId: 2))));
        pay.EnsureSuccessStatusCode();
        var payment = await pay.Content.ReadFromJsonAsync<PaymentResponse>();
        var paid = await client.SendAsync(Authed(
            HttpMethod.Post, $"/api/payments/{payment!.PaymentId}/simulate-success", customerToken));
        paid.EnsureSuccessStatusCode();
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token, HttpContent? body = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = body };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
