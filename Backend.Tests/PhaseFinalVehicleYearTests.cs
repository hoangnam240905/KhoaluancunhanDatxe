using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Constants;
using Backend.DTOs.Auth;
using Backend.DTOs.Vehicles;
using Backend.Services;
using Backend.Validation;
using Xunit;

namespace Backend.Tests;

public class PhaseFinalVehicleYearTests
{
    private static CreateVehicleRequest NewVehicle(string plate, int year)
        => new(1, plate, "Toyota", "Vios", year, "Trắng", 1000);

    [Theory]
    [InlineData(1990, true)]
    [InlineData(2100, true)]
    [InlineData(2022, true)]
    [InlineData(1989, false)]
    [InlineData(2101, false)]
    public void Year_bounds_match_rule(int year, bool valid)
    {
        var error = VehicleLegalRules.ValidateYear(year);
        if (valid)
            Assert.Null(error);
        else
            Assert.Equal(VehicleLegalRules.InvalidYear, error);
    }

    [Fact]
    public async Task Create_and_update_reject_out_of_range_year()
    {
        using var iso = new IsolatedCarRentalDb();
        var service = new VehicleService(iso.Db);

        var low = await service.CreateVehicleAsync(NewVehicle("51P-YR1989", 1989));
        Assert.Equal(400, low.StatusCode);
        Assert.Equal(VehicleLegalRules.InvalidYear, low.Error);
        Assert.False(iso.Db.Vehicles.Any(v => v.LicensePlate == "51P-YR1989"));

        var high = await service.CreateVehicleAsync(NewVehicle("51P-YR2101", 2101));
        Assert.Equal(400, high.StatusCode);
        Assert.Equal(VehicleLegalRules.InvalidYear, high.Error);

        var min = await service.CreateVehicleAsync(NewVehicle("51P-YR1990", 1990));
        Assert.Equal(201, min.StatusCode);
        Assert.Equal(1990, min.Data!.Year);

        var max = await service.CreateVehicleAsync(NewVehicle("51P-YR2100", 2100));
        Assert.Equal(201, max.StatusCode);
        Assert.Equal(2100, max.Data!.Year);

        var original = iso.Db.Vehicles.Single(v => v.VehicleId == 2);
        var badUpdate = await service.UpdateVehicleAsync(2, new UpdateVehicleRequest(
            original.TypeId, original.LicensePlate, original.Brand, original.Model, 1989,
            original.Color, original.Status, original.CurrentKm));
        Assert.Equal(400, badUpdate.StatusCode);
        Assert.Equal(VehicleLegalRules.InvalidYear, badUpdate.Error);
        Assert.Equal(original.Year, iso.Db.Vehicles.Find(2)!.Year);

        var okUpdate = await service.UpdateVehicleAsync(2, new UpdateVehicleRequest(
            original.TypeId, original.LicensePlate, original.Brand, original.Model, 1990,
            original.Color, original.Status, original.CurrentKm));
        Assert.Equal(200, okUpdate.StatusCode);
        Assert.Equal(1990, okUpdate.Data!.Year);
    }

    [Fact]
    public async Task Api_invalid_year_is_400()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("admin@carrental.vn", "Password123!"));
        var token = (await login.Content.ReadFromJsonAsync<AuthResponse>())!.Token;

        var created = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles")
        {
            Content = JsonContent.Create(NewVehicle("51P-YRAPI1", 1888))
        };
        created.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(created);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
