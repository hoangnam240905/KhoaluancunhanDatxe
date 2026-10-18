using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using PortalWeb.Display;
using PortalWeb.Models;
using PortalWeb.Services;
using PortalWeb.Validation;

namespace PortalWeb.Pages.Customer;

public class ProfileModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    [BindProperty]
    public PasswordInputModel PasswordInput { get; set; } = new();

    [BindProperty]
    public ProfileInputModel ProfileInput { get; set; } = new();

    public ProfileView Presentation { get; private set; } = ProfileUi.Empty();
    public string? LoadError { get; private set; }
    public string? ProfileError { get; private set; }
    public string? ProfileSuccess { get; private set; }
    public string? PasswordError { get; private set; }
    public string? PasswordSuccess { get; private set; }
    public bool ShowPasswordPanel { get; private set; }

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Presentation.FullName) ? (auth.FullName ?? "") : Presentation.FullName;

    public class PasswordInputModel
    {
        [Required(ErrorMessage = ProfileUi.OldPasswordRequired)]
        [DataType(DataType.Password)]
        public string OldPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
        [StrongPassword]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới.")]
        [Compare(nameof(NewPassword), ErrorMessage = ProfileUi.ConfirmMismatch)]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class ProfileInputModel
    {
        [DataType(DataType.Date)]
        [Display(Name = "Ngày sinh")]
        public DateOnly? DateOfBirth { get; set; }

        [Range(0, 20, ErrorMessage = "Số con phải từ 0 đến 20.")]
        [Display(Name = "Số con")]
        public int? NumberOfChildren { get; set; }

        [Range(1, 30, ErrorMessage = "Quy mô gia đình phải từ 1 đến 30.")]
        [Display(Name = "Quy mô gia đình")]
        public int? FamilySize { get; set; }

        [Display(Name = "Mức thu nhập")]
        public string? IncomeRange { get; set; }

        [Display(Name = "Loại khu vực sinh sống")]
        public string? CityType { get; set; }

        [Range(0, 500, ErrorMessage = "Quãng đường đi mỗi ngày không hợp lệ.")]
        [Display(Name = "Quãng đường đi mỗi ngày (km)")]
        public double? CommuteKmPerDay { get; set; }

        [Range(0, 200000, ErrorMessage = "Số km mỗi năm không hợp lệ.")]
        [Display(Name = "Số km di chuyển mỗi năm")]
        public int? AnnualMileage { get; set; }

        [Display(Name = "Mục đích thuê xe")]
        public string? RentalPurpose { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Ngân sách tối thiểu không được âm.")]
        [Display(Name = "Ngân sách thuê xe tối thiểu (VND/tháng)")]
        public double? BudgetMin { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Ngân sách tối đa không được âm.")]
        [Display(Name = "Ngân sách thuê xe tối đa (VND/tháng)")]
        public double? BudgetMax { get; set; }

        [Display(Name = "Loại xe ưa thích")]
        public string? PreferredVehicleType { get; set; }

        [Display(Name = "Nhiên liệu ưa thích")]
        public string? PreferredFuelType { get; set; }

        [Display(Name = "Hộp số ưa thích")]
        public string? PreferredTransmission { get; set; }

        [Range(1, 50, ErrorMessage = "Số chỗ mong muốn không hợp lệ.")]
        [Display(Name = "Số chỗ mong muốn")]
        public int? PreferredSeats { get; set; }

        [StringLength(1000, ErrorMessage = "Ghi chú sở thích tối đa 1000 ký tự.")]
        [Display(Name = "Ghi chú sở thích")]
        public string? PreferencesText { get; set; }

        [StringLength(300)]
        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;
        return await LoadProfileAsync() ?? Page();
    }

    public async Task<IActionResult> OnPostSaveProfileAsync()
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;

        if (!ValidateProfileInput())
            return await LoadAccountShellAsync() ?? Page();

        try
        {
            var body = ToUpdateRequest();
            var (data, error, status) = await api.UpdateCustomerProfileAsync(body);
            if (status is 401 or 403) return RedirectToPage("/Account/Login");
            if (data is null)
            {
                ProfileError = error ?? "Không thể cập nhật hồ sơ.";
                return await LoadAccountShellAsync() ?? Page();
            }

            ApplyCustomerProfile(data);
            ProfileSuccess = ProfileUi.SaveSuccess;
            return Page();
        }
        catch
        {
            ProfileError = "Không thể cập nhật hồ sơ. Vui lòng thử lại.";
            return await LoadAccountShellAsync() ?? Page();
        }
    }

    public async Task<IActionResult> OnPostChangePasswordAsync()
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;

        var load = await LoadProfileAsync();
        if (load is not null) return load;

        ShowPasswordPanel = true;
        if (!ModelState.IsValid)
        {
            PasswordError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage
                ?? ProfileUi.ConfirmMismatch;
            return Page();
        }

        try
        {
            var (ok, error, status) = await api.ChangePasswordWithStatusAsync(
                PasswordInput.OldPassword, PasswordInput.NewPassword);
            if (status is 401 or 403) return RedirectToPage("/Account/Login");
            if (!ok)
            {
                PasswordError = error ?? "Không thể đổi mật khẩu.";
                return Page();
            }
        }
        catch
        {
            PasswordError = "Không thể đổi mật khẩu. Vui lòng thử lại.";
            return Page();
        }

        PasswordInput = new PasswordInputModel();
        ShowPasswordPanel = false;
        PasswordSuccess = ProfileUi.PasswordUpdated;
        return Page();
    }

    private bool ValidateProfileInput()
    {
        // Same page also binds empty PasswordInput on POST — ignore those errors entirely.
        foreach (var key in ModelState.Keys.ToList())
        {
            if (!key.StartsWith(nameof(ProfileInput), StringComparison.OrdinalIgnoreCase))
                ModelState.Remove(key);
        }

        if (ProfileInput.BudgetMin is double min && ProfileInput.BudgetMax is double max && min > max)
            ModelState.AddModelError($"{nameof(ProfileInput)}.{nameof(ProfileInput.BudgetMin)}",
                "Ngân sách tối thiểu không được lớn hơn ngân sách tối đa.");

        if (ProfileInput.NumberOfChildren is int kids && ProfileInput.FamilySize is int family && kids >= family)
            ModelState.AddModelError($"{nameof(ProfileInput)}.{nameof(ProfileInput.FamilySize)}",
                "Số thành viên gia đình phải lớn hơn số con.");

        if (ProfileInput.DateOfBirth is DateOnly dob)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (dob > today)
                ModelState.AddModelError($"{nameof(ProfileInput)}.{nameof(ProfileInput.DateOfBirth)}",
                    "Ngày sinh không được ở tương lai.");
            else
            {
                var age = today.Year - dob.Year;
                if (dob > today.AddYears(-age)) age--;
                if (age is < 16 or > 100)
                    ModelState.AddModelError($"{nameof(ProfileInput)}.{nameof(ProfileInput.DateOfBirth)}",
                        "Ngày sinh phải tương ứng độ tuổi từ 16 đến 100.");
            }
        }

        var profileErrors = ModelState
            .Where(kv => kv.Key.StartsWith(nameof(ProfileInput), StringComparison.OrdinalIgnoreCase))
            .SelectMany(kv => kv.Value?.Errors ?? Enumerable.Empty<Microsoft.AspNetCore.Mvc.ModelBinding.ModelError>())
            .Where(e => !string.IsNullOrWhiteSpace(e.ErrorMessage))
            .ToList();

        if (profileErrors.Count > 0)
        {
            ProfileError = profileErrors[0].ErrorMessage;
            return false;
        }

        return true;
    }

    private UpdateCustomerProfileRequest ToUpdateRequest() => new(
        DateOfBirth: ProfileInput.DateOfBirth,
        NumberOfChildren: ProfileInput.NumberOfChildren,
        FamilySize: ProfileInput.FamilySize,
        IncomeRange: ProfileUi.NormalizeSelect(ProfileInput.IncomeRange),
        CityType: ProfileUi.NormalizeSelect(ProfileInput.CityType),
        CommuteKmPerDay: ProfileInput.CommuteKmPerDay,
        AnnualMileage: ProfileInput.AnnualMileage,
        RentalPurpose: ProfileUi.NormalizeSelect(ProfileInput.RentalPurpose),
        BudgetMin: ProfileInput.BudgetMin,
        BudgetMax: ProfileInput.BudgetMax,
        PreferredVehicleType: ProfileUi.NormalizeSelect(ProfileInput.PreferredVehicleType),
        PreferredFuelType: ProfileUi.NormalizeSelect(ProfileInput.PreferredFuelType),
        PreferredTransmission: ProfileUi.NormalizeSelect(ProfileInput.PreferredTransmission),
        PreferredSeats: ProfileInput.PreferredSeats,
        PreferencesText: ProfileUi.NormalizeSelect(ProfileInput.PreferencesText),
        Address: ProfileInput.Address);

    private async Task<IActionResult?> LoadProfileAsync()
    {
        try
        {
            var (data, error, status) = await api.GetCustomerProfileAsync();
            if (status is 401 or 403) return RedirectToPage("/Account/Login");
            if (data is null || status is < 200 or >= 300)
            {
                // Fallback: keep page usable with auth/me basics if customer profile endpoint fails.
                var (basic, basicStatus) = await api.GetProfileWithStatusAsync();
                if (basicStatus is 401 or 403) return RedirectToPage("/Account/Login");
                if (basic is null)
                {
                    LoadError = error ?? ProfileUi.LoadFailure;
                    Presentation = ProfileUi.Empty();
                    return null;
                }

                Presentation = ProfileUi.From(basic.FullName, basic.Email, basic.Phone, basic.Role);
                LoadError = error ?? "Không tải được phần hồ sơ gợi ý xe.";
                return null;
            }

            ApplyCustomerProfile(data);
            return null;
        }
        catch
        {
            LoadError = ProfileUi.LoadFailure;
            Presentation = ProfileUi.Empty();
            return null;
        }
    }

    private async Task<IActionResult?> LoadAccountShellAsync()
    {
        try
        {
            var (basic, status) = await api.GetProfileWithStatusAsync();
            if (status is 401 or 403) return RedirectToPage("/Account/Login");
            if (basic is not null)
                Presentation = ProfileUi.From(basic.FullName, basic.Email, basic.Phone, basic.Role);
            return null;
        }
        catch
        {
            return null;
        }
    }

    private void ApplyCustomerProfile(CustomerProfileResponse data)
    {
        Presentation = ProfileUi.From(data.FullName, data.Email, data.Phone, "Customer");
        ProfileInput = new ProfileInputModel
        {
            DateOfBirth = data.DateOfBirth,
            NumberOfChildren = data.NumberOfChildren,
            FamilySize = data.FamilySize,
            IncomeRange = data.IncomeRange,
            CityType = data.CityType,
            CommuteKmPerDay = data.CommuteKmPerDay,
            AnnualMileage = data.AnnualMileage,
            RentalPurpose = data.RentalPurpose,
            BudgetMin = data.BudgetMin,
            BudgetMax = data.BudgetMax,
            PreferredVehicleType = data.PreferredVehicleType,
            PreferredFuelType = data.PreferredFuelType,
            PreferredTransmission = data.PreferredTransmission,
            PreferredSeats = data.PreferredSeats,
            PreferencesText = data.PreferencesText,
            Address = data.Address
        };
    }
}
