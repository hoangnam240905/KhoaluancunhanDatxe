using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Bookings;
using Backend.DTOs.Contracts;
using Backend.DTOs.Payments;
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
        => new(odometerKm, fuel, exterior, notes, exterior, technical);

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
            JsonContent.Create(new CreatePaymentRequest(bookingId, PaymentTypes.Deposit, PaymentMethods.Cash))));
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
