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

    /// <summary>
    /// Travel-rental profile (plan1.md): personal info + 4 recommendation fields. The commuter questions of the
    /// car-buying dataset (children, income, area, km/day, km/year, monthly budget, dropdown preferences) were
    /// removed; saving sends null for them and the recommender uses training-median defaults.
    /// </summary>
    public class ProfileInputModel
    {
        [DataType(DataType.Date)]
        [Display(Name = "Ngày sinh")]
        public DateOnly? DateOfBirth { get; set; }

        [StringLength(300)]
        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }

        [Range(1, 30, ErrorMessage = "Số người đi phải từ 1 đến 30.")]
        [Display(Name = "Số người đi")]
        public int? FamilySize { get; set; }

        [Display(Name = "Loại hành trình")]
        public string? RentalPurpose { get; set; }

        [Range(0, 100_000_000, ErrorMessage = "Ngân sách thuê xe mỗi ngày không hợp lệ.")]
        [Display(Name = "Ngân sách thuê xe mỗi ngày (VNĐ/ngày)")]
        public double? TripBudgetPerDay { get; set; }

        [StringLength(1000, ErrorMessage = "Ghi chú sở thích tối đa 1000 ký tự.")]
        [Display(Name = "Ghi chú sở thích")]
        public string? PreferencesText { get; set; }
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

    // Removed commuter fields are sent as null (the API replaces these columns) -> recommender defaults.
    private UpdateCustomerProfileRequest ToUpdateRequest() => new(
        DateOfBirth: ProfileInput.DateOfBirth,
        NumberOfChildren: null,
        FamilySize: ProfileInput.FamilySize,
        IncomeRange: null,
        CityType: null,
        CommuteKmPerDay: null,
        AnnualMileage: null,
        RentalPurpose: ProfileUi.NormalizeSelect(ProfileInput.RentalPurpose),
        BudgetMin: null,
        BudgetMax: null,
        PreferredVehicleType: null,
        PreferredFuelType: null,
        PreferredTransmission: null,
        PreferredSeats: null,
        PreferencesText: ProfileUi.NormalizeSelect(ProfileInput.PreferencesText),
        Address: ProfileInput.Address,
        TripBudgetPerDay: ProfileInput.TripBudgetPerDay);

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
            Address = data.Address,
            FamilySize = data.FamilySize,
            RentalPurpose = ProfileUi.ToTripType(data.RentalPurpose),
            TripBudgetPerDay = data.TripBudgetPerDay,
            PreferencesText = data.PreferencesText
        };
    }
}
