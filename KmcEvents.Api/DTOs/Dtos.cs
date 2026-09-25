using System.ComponentModel.DataAnnotations;

namespace KmcEvents.Api.DTOs;

// ============================================================
// LOGIN
// ============================================================

public class LoginDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";
}

// ============================================================
// PUBLIC USER REGISTRATION
// ============================================================

public class PublicRegisterDto
{
    [Required, StringLength(120, MinimumLength = 3)]
    public string FullName { get; set; } = "";

    [Required, RegularExpression(@"^[A-Za-z0-9]{10,12}$", ErrorMessage = "NIC must contain 10 to 12 letters/digits only.")]
    public string Nic { get; set; } = "";

    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, MinLength(8, ErrorMessage = "Password must contain at least 8 characters.")]
    public string Password { get; set; } = "";
}

// ============================================================
// EVENT ORGANIZER REGISTRATION
// ============================================================

public class OrganizerRegisterDto : PublicRegisterDto
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string CompanyName { get; set; } = "";
}

// ============================================================
// FORGOT PASSWORD
// ============================================================

public class ForgotPasswordDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";
}

// ============================================================
// RESET PASSWORD
// ============================================================

public class ResetPasswordDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, RegularExpression(@"^\d{6}$", ErrorMessage = "Reset code must contain exactly 6 digits.")]
    public string Code { get; set; } = "";

    [Required, MinLength(8, ErrorMessage = "New password must contain at least 8 characters.")]
    public string NewPassword { get; set; } = "";

    [Required, Compare(nameof(NewPassword), ErrorMessage = "New password and confirm password do not match.")]
    public string ConfirmPassword { get; set; } = "";
}

// ============================================================
// TICKET CATEGORY
// ============================================================

public class TicketCategoryDto
{
    public int Id { get; set; }

    [Required, StringLength(60)]
    public string Name { get; set; } = "General";

    [Range(0, 10000000)]
    public decimal Price { get; set; }

    [Range(1, 100000)]
    public int SeatLimit { get; set; }
}

// ============================================================
// EVENT CREATE / UPDATE
// ============================================================

public class EventUpsertDto : IValidatableObject
{
    [Required, StringLength(160)]
    public string Title { get; set; } = "";

    [Required, StringLength(1200, MinimumLength = 20)]
    public string Description { get; set; } = "";

    [Required, StringLength(80)]
    public string Category { get; set; } = "";

    [Required, StringLength(180)]
    public string Location { get; set; } = "";

    [Required]
    public DateTime EventDate { get; set; }

    [Required]
    public DateTime RegistrationCloseDate { get; set; }

    [Range(1, 100000)]
    public int Capacity { get; set; }

    [Url]
    public string? ImageUrl { get; set; }

    public bool IsPublished { get; set; }

    public List<TicketCategoryDto> TicketCategories { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var today = DateTime.Today;

        if (TicketCategories.Count == 0)
            yield return new ValidationResult("At least one ticket category is required.", [nameof(TicketCategories)]);

        if (EventDate.Date < today.AddDays(10))
            yield return new ValidationResult("Event date must be at least 10 days from today.", [nameof(EventDate)]);

        if (RegistrationCloseDate.Date < today)
            yield return new ValidationResult("Registration close date cannot be in the past.", [nameof(RegistrationCloseDate)]);

        if (RegistrationCloseDate >= EventDate)
            yield return new ValidationResult("Registration must close before the event starts.", [nameof(RegistrationCloseDate)]);

        if (TicketCategories.Sum(x => x.SeatLimit) > Capacity)
            yield return new ValidationResult("Ticket category seat limits cannot exceed total event capacity.", [nameof(TicketCategories)]);
    }
}

// ============================================================
// RESERVATION CREATE
// Pay Later creates reservation immediately.
// Card redirects to checkout without saving reservation.
// ============================================================

public class ReservationCreateDto
{
    [Range(1, int.MaxValue)]
    public int EventId { get; set; }

    [Range(1, int.MaxValue)]
    public int TicketCategoryId { get; set; }

    [Range(1, 10)]
    public int Quantity { get; set; } = 1;

    [Required, RegularExpression("^(Pay Later|Card)$", ErrorMessage = "Invalid payment method.")]
    public string PaymentMethod { get; set; } = "Pay Later";
}

// ============================================================
// CARD CHECKOUT
// Used by the new payment flow.
// Reservation is created only after successful card validation.
// ============================================================

public class CardCheckoutDto
{
    [Range(1, int.MaxValue)]
    public int EventId { get; set; }

    [Range(1, int.MaxValue)]
    public int TicketCategoryId { get; set; }

    [Range(1, 10)]
    public int Quantity { get; set; } = 1;

    [Required, StringLength(120)]
    public string CardHolder { get; set; } = "";

    [Required, RegularExpression(@"^\d{16}$", ErrorMessage = "Card number must contain exactly 16 digits.")]
    public string CardNumber { get; set; } = "";

    [Required, RegularExpression(@"^(0[1-9]|1[0-2])/\d{2}$", ErrorMessage = "Expiry must be MM/YY.")]
    public string Expiry { get; set; } = "";

    [Required, RegularExpression(@"^\d{3,4}$", ErrorMessage = "CVV must contain 3 or 4 digits.")]
    public string Cvv { get; set; } = "";
}

// ============================================================
// OLD CARD PAYMENT DTO
// Remove this class after old /{id}/pay endpoint is removed.
// ============================================================

public class CardPaymentDto
{
    [Required, StringLength(120)]
    public string CardHolder { get; set; } = "";

    [Required, RegularExpression(@"^\d{16}$", ErrorMessage = "Card number must contain exactly 16 digits.")]
    public string CardNumber { get; set; } = "";

    [Required, RegularExpression(@"^(0[1-9]|1[0-2])/\d{2}$", ErrorMessage = "Expiry must be MM/YY.")]
    public string Expiry { get; set; } = "";

    [Required, RegularExpression(@"^\d{3,4}$", ErrorMessage = "CVV must be 3 or 4 digits.")]
    public string Cvv { get; set; } = "";
}

// ============================================================
// CONTACT FORM
// ============================================================

public class ContactDto
{
    [Required, StringLength(120)]
    public string Name { get; set; } = "";

    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, StringLength(160)]
    public string Subject { get; set; } = "";

    [Required, StringLength(2000, MinimumLength = 10)]
    public string Message { get; set; } = "";
}