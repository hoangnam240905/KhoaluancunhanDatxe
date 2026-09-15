using System.ComponentModel.DataAnnotations;
using CustomerWeb.Display;
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
    public int BookingRouteId { get; set; }
    public BookingResponse? Booking { get; set; }
    public string? CustomerName => auth.FullName;
    public string? ErrorMessage { get; set; }
    public bool CanReview { get; set; }
    public string ViewState { get; private set; } = "unavailable";
    public ReviewView Presentation { get; private set; } = ReviewUi.Placeholder(0);
    public bool AccessDenied { get; private set; }
    public bool NotFoundBooking { get; private set; }
    public bool NeedsLogin { get; private set; }

    public class InputModel
    {
        [Range(1, 5, ErrorMessage = ReviewUi.RatingRequired)]
        public byte Rating { get; set; }

        [MaxLength(ReviewUi.CommentMax, ErrorMessage = ReviewUi.CommentTooLong)]
        public string? Comment { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!auth.IsLoggedIn) return CustomerLoginRedirect.ToLogin(this);
        await LoadAsync(id);
        if (NeedsLogin) return CustomerLoginRedirect.ToLogin(this);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!auth.IsLoggedIn) return CustomerLoginRedirect.ToLogin(this);
        await LoadAsync(id);
        if (NeedsLogin) return CustomerLoginRedirect.ToLogin(this);
        if (AccessDenied || NotFoundBooking || Booking is null)
            return Page();

        if (Booking.Review is not null)
        {
            ViewState = "existing";
            return Page();
        }

        if (!CanReview)
            return Page();

        ApplyCommentRule();
        if (!ModelState.IsValid)
        {
            ViewState = "form";
            ErrorMessage = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));
            return Page();
        }

        var (created, error, status) = await api.CreateReviewAsync(id, new CreateReviewRequest(Input.Rating, Input.Comment));
        if (status is 401)
            return CustomerLoginRedirect.ToLogin(this);

        if (created is null)
        {
            ErrorMessage = ReviewUi.PresentPostError(error, status, eligible: true);
            await LoadAsync(id);
            if (Booking?.Review is not null)
            {
                ViewState = "existing";
                ErrorMessage = ReviewUi.Duplicate;
            }
            else
            {
                ViewState = CanReview ? "form" : ViewState;
            }
            return Page();
        }

        await LoadAsync(id);
        ViewState = "success";
        if (Booking is not null)
            Presentation = ReviewUi.From(Booking, Booking.Review ?? created);
        return Page();
    }

    private async Task LoadAsync(int id)
    {
        BookingRouteId = id;
        BookingId = id;
        CanReview = false;
        AccessDenied = false;
        NotFoundBooking = false;
        NeedsLogin = false;
        try
        {
            var (data, status) = await api.GetBookingWithStatusAsync(id);
            if (status is 401)
            {
                NeedsLogin = true;
                return;
            }

            if (status is 403)
            {
                AccessDenied = true;
                ErrorMessage = ReviewUi.AccessDenied;
                ViewState = "unavailable";
                Presentation = ReviewUi.Placeholder(id);
                return;
            }

            if (data is null)
            {
                NotFoundBooking = true;
                ErrorMessage = status >= 500 ? ReviewUi.LoadFailure : ReviewUi.NotFound;
                ViewState = "unavailable";
                Presentation = ReviewUi.Placeholder(id);
                return;
            }

            Booking = data;
            Presentation = ReviewUi.From(data);
            CanReview = ReviewUi.CanOfferReview(data) && data.Review is null;
            ViewState = data.Review is not null
                ? "existing"
                : CanReview ? "form" : "unavailable";
            if (ViewState == "unavailable")
                ErrorMessage ??= ReviewUi.Unavailable;
        }
        catch
        {
            ErrorMessage = ReviewUi.LoadFailure;
            ViewState = "unavailable";
            Presentation = ReviewUi.Placeholder(id);
        }
    }

    private void ApplyCommentRule()
    {
        var comment = string.IsNullOrWhiteSpace(Input.Comment) ? null : Input.Comment.Trim();
        if (Input.Rating is >= 1 and <= 3 && comment is null)
            ModelState.AddModelError("Input.Comment", ReviewUi.CommentRequired);
        else if (comment is { Length: > ReviewUi.CommentMax })
            ModelState.AddModelError("Input.Comment", ReviewUi.CommentTooLong);
        else
            Input.Comment = comment;
    }
}
