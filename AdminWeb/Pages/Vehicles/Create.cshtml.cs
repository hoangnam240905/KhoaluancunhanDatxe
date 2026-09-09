using System.ComponentModel.DataAnnotations;
using AdminWeb.Display;
using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminWeb.Pages.Vehicles;

public class CreateModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public List<VehicleTypeResponse> VehicleTypes { get; set; } = [];
    public string? ErrorMessage { get; set; }

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
        public int Year { get; set; } = DateTime.Now.Year;

        public string? Color { get; set; }
        public int CurrentKm { get; set; }

        [MaxLength(30, ErrorMessage = "Số giấy đăng ký không được vượt quá 30 ký tự.")]
        public string? RegistrationNumber { get; set; }
        public DateOnly? RegistrationExpiryDate { get; set; }
        public DateOnly? InspectionExpiryDate { get; set; }
        public DateOnly? InsuranceExpiryDate { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
        VehicleTypes = await api.GetVehicleTypesAsync();
        if (VehicleTypes.Count > 0) Input.TypeId = VehicleTypes[0].TypeId;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
        VehicleTypes = await api.GetVehicleTypesAsync();
        if (!ModelState.IsValid) return Page();
        VehicleResponse? data;
        string? error;
        try
        {
            (data, error) = await api.CreateVehicleAsync(new CreateVehicleRequest(
                Input.TypeId, Input.LicensePlate, Input.Brand, Input.Model, Input.Year, Input.Color, Input.CurrentKm,
                Input.RegistrationNumber, Input.RegistrationExpiryDate, Input.InspectionExpiryDate, Input.InsuranceExpiryDate));
        }
        catch (HttpRequestException)
        {
            ErrorMessage = UiDisplay.ApiFailure("⚠️ Không thể thêm xe.", "Không kết nối được máy chủ.");
            return Page();
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = UiDisplay.ApiFailure("⚠️ Không thể thêm xe.", "Hết thời gian chờ máy chủ.");
            return Page();
        }

        if (data is null)
        {
            ErrorMessage = UiDisplay.ApiFailure("⚠️ Không thể thêm xe.", error);
            return Page();
        }

        TempData["Message"] = "✅ Thêm xe thành công.";
        return RedirectToPage("Index");
    }
}
