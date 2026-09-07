using System.ComponentModel.DataAnnotations;
using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Customer.Bookings;

public class ReviewModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public int BookingId { get; set; }
    public BookingResponse? Booking { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public bool CanReview { get; set; }

    public class InputModel
    {
        [Required, Range(1, 5)]
        public byte Rating { get; set; } = 5;

        [MaxLength(500, ErrorMessage = "Nhận xét không được vượt quá 500 ký tự.")]
        public string? Comment { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;
        if (!await LoadAsync(id)) return RedirectToPage("Index");
        ApplyEligibility();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;
        if (!await LoadAsync(id)) return RedirectToPage("Index");
        ApplyEligibility();
        if (!CanReview) return Page();
        ApplyCommentRule();
        if (!ModelState.IsValid) return Page();

        var (success, error) = await api.CreateReviewAsync(id, new CreateReviewRequest(Input.Rating, Input.Comment));
        if (!success)
        {
            ErrorMessage = error;
            return Page();
        }

        CanReview = false;
        SuccessMessage = "Cảm ơn bạn đã đánh giá!";
        return Page();
    }

    private async Task<bool> LoadAsync(int id)
    {
        BookingId = id;
        Booking = await api.GetBookingAsync(id);
        return Booking is not null;
    }

    private void ApplyEligibility()
    {
        if (Booking is null)
        {
            CanReview = false;
            return;
        }

        if (IndexModel.IsSelfDrive(Booking.RentalMode))
        {
            CanReview = false;
            ErrorMessage = "Đơn tự lái không đánh giá tài xế trên hệ thống hiện tại.";
            return;
        }

        if (!IndexModel.CanOfferReview(Booking))
        {
            CanReview = false;
            ErrorMessage = "Chỉ đánh giá đơn đã hoàn thành và đã có tài xế.";
            return;
        }

        CanReview = true;
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
