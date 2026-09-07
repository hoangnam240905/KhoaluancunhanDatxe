using System.ComponentModel.DataAnnotations;
using CustomerWeb.Models;
using CustomerWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CustomerWeb.Pages.Bookings;

public class ReviewModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public int BookingId { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public bool CanReview { get; set; } = true;

    public class InputModel
    {
        [Required, Range(1, 5)]
        public byte Rating { get; set; } = 5;

        [MaxLength(500, ErrorMessage = "Nhận xét không được vượt quá 500 ký tự.")]
        public string? Comment { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
        BookingId = id;
        var booking = await api.GetBookingAsync(id);
        if (booking is null) return RedirectToPage("Index");
        if (IndexModel.IsSelfDrive(booking.RentalMode))
        {
            CanReview = false;
            ErrorMessage = "Đơn tự lái không đánh giá tài xế trên hệ thống hiện tại.";
            return Page();
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");

        BookingId = id;
        var booking = await api.GetBookingAsync(id);
        if (booking is null) return RedirectToPage("Index");
        if (IndexModel.IsSelfDrive(booking.RentalMode))
        {
            CanReview = false;
            ErrorMessage = "Đơn tự lái không đánh giá tài xế trên hệ thống hiện tại.";
            return Page();
        }
        if (!ModelState.IsValid) return Page();
        ApplyCommentRule();
        if (!ModelState.IsValid) return Page();

        var (success, error) = await api.CreateReviewAsync(id, new CreateReviewRequest(Input.Rating, Input.Comment));
        if (!success)
        {
            ErrorMessage = error;
            return Page();
        }

        SuccessMessage = "Cam on ban da danh gia!";
        return Page();
    }

    private void ApplyCommentRule()
    {
        var comment = string.IsNullOrWhiteSpace(Input.Comment) ? null : Input.Comment.Trim();
        if (Input.Rating is >= 1 and <= 3 && comment is null)
            ModelState.AddModelError("Input.Comment", "Vui lòng nhập nhận xét khi đánh giá từ 1 đến 3 sao.");
        else if (comment is { Length: > 500 })
            ModelState.AddModelError("Input.Comment", "Nhận xét không được vượt quá 500 ký tự.");
        else
            Input.Comment = comment;
    }
}
