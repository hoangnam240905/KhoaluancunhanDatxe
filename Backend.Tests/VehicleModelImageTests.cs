using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Constants;
using Backend.DTOs.Auth;
using Backend.DTOs.Vehicles;
using Backend.Entities;
using Backend.Services;
using Xunit;

namespace Backend.Tests;

/// <summary>plan1.md Giai đoạn 5 — real photo per car model, "Chưa có ảnh" otherwise (T26–T30).</summary>
public class VehicleModelImageTests : IClassFixture<IsolatedApiFactory>
{
    private readonly IsolatedApiFactory _factory;

    public VehicleModelImageTests(IsolatedApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("Toyota", "Vios", "toyota-vios.jpg")]
    [InlineData("Kia", "Sorento Plus", "kia-sorento.jpg")]
    [InlineData("kia", "sorento sport", "kia-sorento.jpg")]
    [InlineData("MINI", "Cooper 3-Door Urban", "mini-cooper-3-door.jpg")]
    [InlineData("Mazda", "3 Comfort", "mazda-3.jpg")]
    [InlineData("Mazda", "CX-5 Premium", "mazda-cx-5.jpg")]
    [InlineData("Fiat", "500 Base", "fiat-500.jpg")]
    public void T26_trims_share_the_photo_of_their_own_model(string brand, string model, string file)
        => Assert.Equal("/images/vehicles/" + file, VehicleModelImages.Resolve(brand, model));

    [Theory]
    [InlineData("T", "T")]
    [InlineData("Toyota", "Camry")]      // model we have no photo for
    [InlineData("Mazda", "30")]          // "3" must not match "30"
    [InlineData("Hyundai", "Vios")]      // right model name, wrong brand
    [InlineData("Toyota", "ViosX")]
    public void T27_unknown_model_gets_no_photo_instead_of_a_borrowed_one(string brand, string model)
        => Assert.Null(VehicleModelImages.Resolve(brand, model));

    [Fact]
    public void T26_two_different_models_never_share_a_file()
        => Assert.Equal(VehicleModelImages.Files.Count, VehicleModelImages.Files.Distinct().Count());

    [Fact]
    public void T29_seed_twice_adds_no_duplicate_and_keeps_an_admin_photo()
    {
        using var iso = new IsolatedCarRentalDb();
        iso.Db.Vehicles.Add(new Vehicle { TypeId = 1, LicensePlate = "51K-55501", Brand = "T", Model = "T", Year = 2024, Status = VehicleStatuses.Available });
        iso.Db.VehicleImages.Add(new VehicleImage { VehicleId = 2, ImageUrl = "/images/vehicles/uploads/v2-own.jpg", IsPrimary = true, CreatedAt = DateTime.UtcNow });
        iso.Db.SaveChanges();
        var known = iso.Db.Vehicles.AsEnumerable().Count(v => VehicleModelImages.Resolve(v.Brand, v.Model) is not null);

        var first = VehicleModelImages.Seed(iso.Db);
        var second = VehicleModelImages.Seed(iso.Db);

        Assert.Equal(known - 1, first); // vehicle 2 keeps the admin's photo
        Assert.Equal(0, second);
        Assert.All(iso.Db.VehicleImages.GroupBy(i => i.VehicleId), g => Assert.Single(g, i => i.IsPrimary));
        Assert.Equal("/images/vehicles/uploads/v2-own.jpg", iso.Db.VehicleImages.Single(i => i.VehicleId == 2).ImageUrl);
        Assert.DoesNotContain(iso.Db.VehicleImages, i => i.Vehicle.Brand == "T");
    }

    [Fact]
    public void T30_every_model_photo_exists_and_is_credited()
    {
        var dir = Path.Combine(RepoRoot(), "PortalWeb", "wwwroot", "images", "vehicles");
        var credits = File.ReadAllText(Path.Combine(dir, "CREDITS.md"));
        Assert.All(VehicleModelImages.Files, file =>
        {
            var info = new FileInfo(Path.Combine(dir, file));
            Assert.True(info.Exists, file);
            Assert.InRange(info.Length, 10 * 1024, 300 * 1024);
            var row = credits.Split('\n').SingleOrDefault(l => l.StartsWith($"| `{file}` |"));
            Assert.NotNull(row);
            Assert.Contains("commons.wikimedia.org", row);
            Assert.Matches(@"\[(CC BY|CC BY-SA|CC0|Public domain)[^\]]*\]\(https://", row);
        });
    }

    [Fact]
    public async Task T28_api_returns_each_cars_own_photo_and_admin_can_replace_it()
    {
        var client = _factory.CreateClient();

        // Seeded at startup: the Vios (vehicle 1) has the Vios photo, never another model's.
        var vios = await client.GetFromJsonAsync<VehicleResponse>("/api/vehicles/1");
        Assert.Equal("/images/vehicles/toyota-vios.jpg", vios!.PrimaryImageUrl);
        var all = await client.GetFromJsonAsync<List<VehicleResponse>>("/api/vehicles");
        Assert.All(all!, v => Assert.Equal(VehicleModelImages.Resolve(v.Brand, v.Model), v.PrimaryImageUrl));

        // Admin uploads a new photo for vehicle 3: it becomes the only primary one.
        var admin = await LoginAsync(client, "admin@carrental.vn");
        var create = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles/3/images")
        {
            Content = JsonContent.Create(new CreateVehicleImageRequest("/images/vehicles/uploads/v3-new.jpg", "upload", IsPrimary: true))
        };
        create.Headers.Authorization = new AuthenticationHeaderValue("Bearer", admin);
        Assert.Equal(HttpStatusCode.Created, (await client.SendAsync(create)).StatusCode);

        var car = await client.GetFromJsonAsync<VehicleResponse>("/api/vehicles/3");
        Assert.Equal("/images/vehicles/uploads/v3-new.jpg", car!.PrimaryImageUrl);
        var images = await client.GetFromJsonAsync<List<VehicleImageResponse>>("/api/vehicles/3/images");
        Assert.Single(images!, i => i.IsPrimary);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "PortalWeb")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("PortalWeb folder not found above the test output.");
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
    }
}
