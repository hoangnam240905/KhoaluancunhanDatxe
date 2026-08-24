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

    public class InputModel
    {
        [Required, Range(1, 5)]
        public byte Rating { get; set; } = 5;

        public string? Comment { get; set; }
    }

    public IActionResult OnGet(int id)
    {
        if (!auth.IsLoggedIn) return RedirectToPage("/Account/Login");
        BookingId = id;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!auth.IsLoggedIn) return RedirectToPage("/Account/Login");

        BookingId = id;
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
}
