using System.ComponentModel.DataAnnotations;
using CustomerWeb.Display;
using CustomerWeb.Services;
using CustomerWeb.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CustomerWeb.Pages;

public class ProfileModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    [BindProperty]
    public PasswordInputModel PasswordInput { get; set; } = new();

    public ProfileView Presentation { get; private set; } = ProfileUi.Empty();
    public string? LoadError { get; private set; }
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

    public async Task<IActionResult> OnGetAsync()
    {
        if (!auth.IsLoggedIn) return CustomerLoginRedirect.ToLogin(this);
        return await LoadProfileAsync() ?? Page();
    }

    public async Task<IActionResult> OnPostChangePasswordAsync()
    {
        if (!auth.IsLoggedIn) return CustomerLoginRedirect.ToLogin(this);

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
            if (status is 401 or 403) return CustomerLoginRedirect.ToLogin(this);
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

    private async Task<IActionResult?> LoadProfileAsync()
    {
        try
        {
            var (data, status) = await api.GetProfileWithStatusAsync();
            if (status is 401 or 403) return CustomerLoginRedirect.ToLogin(this);
            if (data is null || status is < 200 or >= 300)
            {
                LoadError = ProfileUi.LoadFailure;
                Presentation = ProfileUi.Empty();
                return null;
            }

            Presentation = ProfileUi.From(data.FullName, data.Email, data.Phone, data.Role);
            return null;
        }
        catch
        {
            LoadError = ProfileUi.LoadFailure;
            Presentation = ProfileUi.Empty();
            return null;
        }
    }
}
