using System.ComponentModel.DataAnnotations;
using PortalWeb.Display;
using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Admin.Vehicles;

public class EditModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public List<VehicleTypeResponse> VehicleTypes { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public int VehicleId { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Vui lòng chọn loại xe.")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập biển số xe.")]
        [MaxLength(20, ErrorMessage = "Biển số xe không được vượt quá 20 ký tự.")]
        [RegularExpression(@"^[0-9]{2}[A-Za-z]-([0-9]{4,5}|[A-Za-z][A-Za-z0-9]{3,7})$", ErrorMessage = "Biển số không đúng định dạng (ví dụ 51A-12345).")]
        public string LicensePlate
        {
            get => _licensePlate;
            set => _licensePlate = (value ?? string.Empty).Trim();
        }
        private string _licensePlate = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập hãng xe.")]
        public string Brand { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập model.")]
        public string Model { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập năm sản xuất.")]
        [Range(1990, 2100, ErrorMessage = "Năm sản xuất phải từ 1990 đến 2100.")]
        public int Year { get; set; }

        public string? Color { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn trạng thái.")]
        public string Status { get; set; } = "Available";

        public int CurrentKm { get; set; }

        [MaxLength(30, ErrorMessage = "Số giấy đăng ký không được vượt quá 30 ký tự.")]
        public string? RegistrationNumber { get; set; }
        public DateOnly? RegistrationExpiryDate { get; set; }
        public DateOnly? InspectionExpiryDate { get; set; }
        public DateOnly? InsuranceExpiryDate { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        VehicleId = id;
        VehicleTypes = await api.GetVehicleTypesAsync();
        var v = await api.GetVehicleAsync(id);
        if (v is null) return NotFound();
        Input = new InputModel
        {
            TypeId = v.TypeId, LicensePlate = v.LicensePlate, Brand = v.Brand, Model = v.Model, Year = v.Year,
            Color = v.Color, Status = v.Status, CurrentKm = v.CurrentKm,
            RegistrationNumber = v.RegistrationNumber,
            RegistrationExpiryDate = v.RegistrationExpiryDate,
            InspectionExpiryDate = v.InspectionExpiryDate,
            InsuranceExpiryDate = v.InsuranceExpiryDate
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        VehicleId = id;
        VehicleTypes = await api.GetVehicleTypesAsync();
        if (!ModelState.IsValid) return Page();
        VehicleResponse? data;
        string? error;
        try
        {
            (data, error) = await api.UpdateVehicleAsync(id, new UpdateVehicleRequest(
                Input.TypeId, Input.LicensePlate, Input.Brand, Input.Model, Input.Year, Input.Color, Input.Status, Input.CurrentKm,
                Input.RegistrationNumber, Input.RegistrationExpiryDate, Input.InspectionExpiryDate, Input.InsuranceExpiryDate));
        }
        catch (HttpRequestException)
        {
            ErrorMessage = UiDisplay.ApiFailure("⚠️ Không thể cập nhật xe.", "Không kết nối được máy chủ.");
            return Page();
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = UiDisplay.ApiFailure("⚠️ Không thể cập nhật xe.", "Hết thời gian chờ máy chủ.");
            return Page();
        }

        if (data is null)
        {
            ErrorMessage = UiDisplay.ApiFailure("⚠️ Không thể cập nhật xe.", error);
            return Page();
        }

        TempData["Message"] = "✅ Cập nhật xe thành công.";
        return RedirectToPage("Index");
    }
}
