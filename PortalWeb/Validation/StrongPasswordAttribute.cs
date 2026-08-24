using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace PortalWeb.Validation;

public class StrongPasswordAttribute : ValidationAttribute
{
    public StrongPasswordAttribute() : base("Mat khau khong hop le.") { }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string password || string.IsNullOrWhiteSpace(password))
            return new ValidationResult("Vui long nhap mat khau.");

        if (password.Length < 8)
            return new ValidationResult("Mat khau phai co it nhat 8 ky tu.");

        if (!Regex.IsMatch(password, @"[A-Z]"))
            return new ValidationResult("Mat khau phai co it nhat 1 chu hoa (A-Z).");

        if (!Regex.IsMatch(password, @"[^a-zA-Z0-9]"))
            return new ValidationResult("Mat khau phai co it nhat 1 ky tu dac biet (!@#$...).");

        return ValidationResult.Success;
    }
}
