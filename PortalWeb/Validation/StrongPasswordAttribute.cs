using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace PortalWeb.Validation;

public class StrongPasswordAttribute : ValidationAttribute
{
    public StrongPasswordAttribute() : base("Mật khẩu không hợp lệ.") { }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string password || string.IsNullOrWhiteSpace(password))
            return new ValidationResult("Vui lòng nhập mật khẩu.");

        if (password.Length < 8)
            return new ValidationResult("Mật khẩu phải có ít nhất 8 ký tự.");

        if (!Regex.IsMatch(password, @"[A-Z]"))
            return new ValidationResult("Mật khẩu phải có ít nhất 1 chữ hoa (A-Z).");

        if (!Regex.IsMatch(password, @"[^a-zA-Z0-9]"))
            return new ValidationResult("Mật khẩu phải có ít nhất 1 ký tự đặc biệt (!@#$...).");

        return ValidationResult.Success;
    }
}
