using Backend.Data;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

/// <summary>
/// Real photos per car MODEL (plan1.md Giai đoạn 5). Files live in PortalWeb/wwwroot/images/vehicles
/// (sources and licences in CREDITS.md there). Trims of one model (Kia Sorento Base / Plus / Sport…)
/// share that model's photo; a car that matches no model gets no image ("Chưa có ảnh" on the web),
/// never another model's photo.
/// </summary>
public static class VehicleModelImages
{
    public const string ImageType = "model";

    // Brand, base model (the trim after it is ignored), file. Longest model first within a brand is not
    // needed: no base model here is a prefix of another of the same brand.
    private static readonly (string Brand, string Model, string File)[] Models =
    [
        ("Fiat", "500", "fiat-500.jpg"),
        ("Ford", "Everest", "ford-everest.jpg"),
        ("Ford", "Transit", "ford-transit.jpg"),
        ("Hyundai", "Accent", "hyundai-accent.jpg"),
        ("Hyundai", "Solati", "hyundai-solati.jpg"),
        ("Hyundai", "Tucson", "hyundai-tucson.jpg"),
        ("Hyundai", "i20", "hyundai-i20.jpg"),
        ("Jeep", "Wrangler", "jeep-wrangler.jpg"),
        ("Kia", "Picanto", "kia-picanto.jpg"),
        ("Kia", "Sorento", "kia-sorento.jpg"),
        ("Kia", "Sportage", "kia-sportage.jpg"),
        ("MINI", "Cooper 3-Door", "mini-cooper-3-door.jpg"),
        ("Mazda", "3", "mazda-3.jpg"),
        ("Mazda", "CX-5", "mazda-cx-5.jpg"),
        ("Nissan", "Qashqai", "nissan-qashqai.jpg"),
        ("Porsche", "Macan", "porsche-macan.jpg"),
        ("Skoda", "Kodiaq", "skoda-kodiaq.jpg"),
        ("Skoda", "Octavia", "skoda-octavia.jpg"),
        ("Smart", "Fortwo", "smart-fortwo.jpg"),
        ("Toyota", "Corolla", "toyota-corolla.jpg"),
        ("Toyota", "Fortuner", "toyota-fortuner.jpg"),
        ("Toyota", "RAV4", "toyota-rav4.jpg"),
        ("Toyota", "Vios", "toyota-vios.jpg"),
        ("Toyota", "Yaris", "toyota-yaris.jpg")
    ];

    public static IReadOnlyList<string> Files => Models.Select(m => m.File).ToList();

    /// <summary>Web path of the model photo for this brand + model, or null when the model is unknown.</summary>
    public static string? Resolve(string? brand, string? model)
    {
        var b = brand?.Trim() ?? "";
        var m = model?.Trim() ?? "";
        var hit = Models.FirstOrDefault(x =>
            string.Equals(x.Brand, b, StringComparison.OrdinalIgnoreCase)
            && (string.Equals(x.Model, m, StringComparison.OrdinalIgnoreCase)
                || m.StartsWith(x.Model + " ", StringComparison.OrdinalIgnoreCase)));
        return hit.File is null ? null : "/images/vehicles/" + hit.File;
    }

    /// <summary>
    /// Idempotent: gives each vehicle WITHOUT a primary image the photo of its model. Vehicles that already
    /// have a primary image (e.g. uploaded by an admin) are left alone; running twice adds nothing.
    /// </summary>
    public static int Seed(CarRentalDbContext db)
    {
        var withPrimary = db.VehicleImages.Where(i => i.IsPrimary).Select(i => i.VehicleId).ToHashSet();
        var added = 0;
        foreach (var v in db.Vehicles.AsNoTracking().Select(v => new { v.VehicleId, v.Brand, v.Model }).ToList())
        {
            if (withPrimary.Contains(v.VehicleId)) continue;
            var url = Resolve(v.Brand, v.Model);
            if (url is null) continue;
            db.VehicleImages.Add(new VehicleImage
            {
                VehicleId = v.VehicleId,
                ImageUrl = url,
                ImageType = ImageType,
                IsPrimary = true,
                DisplayOrder = 0,
                CreatedAt = DateTime.UtcNow
            });
            added++;
        }
        if (added > 0) db.SaveChanges();
        return added;
    }
}
