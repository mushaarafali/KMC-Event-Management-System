using System.ComponentModel.DataAnnotations;

namespace KmcEvents.Client.Models;


// ============================================================
// LOGIN VIEW MODEL
// Used when a user logs into the KMC Event Management System.
// ============================================================

public class LoginVm
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";
}


// ============================================================
// PUBLIC USER REGISTRATION VIEW MODEL
// Used when a public guest creates an account.
// Public users must login before reserving event seats.
// ============================================================

public class PublicRegisterVm
{
    [Required]
    public string FullName { get; set; } = "";

    [Required]
    [RegularExpression(
        @"^[A-Za-z0-9]{10,12}$",
        ErrorMessage = "NIC must be 10-12 letters/digits."
    )]
    public string Nic { get; set; } = "";

    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = "";
}


// ============================================================
// EVENT ORGANIZER REGISTRATION VIEW MODEL
// Inherits the common registration fields from PublicRegisterVm.
// Adds the organizer's company name.
// ============================================================

public class OrganizerRegisterVm : PublicRegisterVm
{
    [Required]
    public string CompanyName { get; set; } = "";
}


// ============================================================
// TICKET CATEGORY VIEW MODEL
// Represents ticket types such as:
// General, VIP and Premium.
// ============================================================

public class TicketVm
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    [Range(0, 10000000)]
    public decimal Price { get; set; }

    [Range(0, 100000)]
    public int SeatLimit { get; set; }

    // Total seats already reserved for this ticket category.
    public int BookedSeats { get; set; }

    // Remaining seats available for this ticket category.
    public int AvailableSeats { get; set; }
}


// ============================================================
// EVENT VIEW MODEL
// Used for creating, editing, displaying and managing events.
// ============================================================

public class EventVm
{
    public int Id { get; set; }


    // --------------------------------------------------------
    // Basic Event Information
    // --------------------------------------------------------

    [Required]
    public string Title { get; set; } = "";

    [Required]
    [MinLength(
        20,
        ErrorMessage = "Description must contain at least 20 characters."
    )]
    public string Description { get; set; } = "";

    [Required]
    public string Category { get; set; } = "";

    [Required]
    public string Location { get; set; } = "";


    // --------------------------------------------------------
    // Event Date and Registration Date
    // --------------------------------------------------------

    [Required]
    public DateTime EventDate { get; set; } =
        DateTime.Now.AddDays(10);

    [Required]
    public DateTime RegistrationCloseDate { get; set; } =
        DateTime.Now.AddDays(9);


    // --------------------------------------------------------
    // Event Capacity
    // --------------------------------------------------------

    [Range(
        1,
        100000,
        ErrorMessage = "Capacity must be at least 1."
    )]
    public int Capacity { get; set; } = 100;


    // --------------------------------------------------------
    // Event Image
    // --------------------------------------------------------

    [Url]
    public string? ImageUrl { get; set; }
    public IFormFile? ImageFile { get; set; }

    // --------------------------------------------------------
    // Publishing Status
    // --------------------------------------------------------

    public bool IsPublished { get; set; }


    // --------------------------------------------------------
    // Organizer Information
    // --------------------------------------------------------

    public string? OrganizerName { get; set; }

    public string? CompanyName { get; set; }


    // --------------------------------------------------------
    // Seat Availability
    // --------------------------------------------------------

    public int AvailableSeats { get; set; }


    // --------------------------------------------------------
    // Ticket Categories
    // Default ticket category is General.
    // --------------------------------------------------------

    public List<TicketVm> TicketCategories { get; set; } =
        new List<TicketVm>
        {
            new TicketVm
            {
                Name = "General",
                Price = 1000,
                SeatLimit = 100
            }
        };
}


// ============================================================
// RESERVATION VIEW MODEL
// Used for public event reservations and organizer/admin
// reservation management.
// ============================================================

public class ReservationVm
{
    public int Id { get; set; }

    public int EventId { get; set; }

    public int TicketCategoryId { get; set; }


    // --------------------------------------------------------
    // Number of Seats
    // Maximum 10 seats per reservation.
    // --------------------------------------------------------

    [Range(
        1,
        10,
        ErrorMessage = "You can reserve between 1 and 10 seats."
    )]
    public int Quantity { get; set; } = 1;


    // --------------------------------------------------------
    // Payment Method
    // Example: Pay Later or Card
    // --------------------------------------------------------

    public string PaymentMethod { get; set; } = "Pay Later";


    // --------------------------------------------------------
    // Event Information
    // --------------------------------------------------------

    public string? EventTitle { get; set; }

    public DateTime EventDate { get; set; }

    public string? Location { get; set; }


    // --------------------------------------------------------
    // Ticket Information
    // --------------------------------------------------------

    public string? TicketCategory { get; set; }

    public decimal TotalAmount { get; set; }


    // --------------------------------------------------------
    // Payment and Approval Status
    // --------------------------------------------------------

    public string? PaymentStatus { get; set; }

    public string? ApprovalStatus { get; set; }


    // --------------------------------------------------------
    // Reservation Information
    // --------------------------------------------------------

    public DateTime ReservedAt { get; set; }


    // --------------------------------------------------------
    // QR Ticket
    // QR is available after organizer approval.
    // --------------------------------------------------------

    public string? QrCode { get; set; }


    // --------------------------------------------------------
    // Customer Information
    // Used by Organizer and Admin dashboards.
    // --------------------------------------------------------

    public string? Customer { get; set; }

    public string? Email { get; set; }
}


// ============================================================
// CARD PAYMENT VIEW MODEL
// Used for card checkout before reservation creation.
// Reservation is created only after successful payment.
// ============================================================

public class CardVm
{
    public int EventId { get; set; }

    public int TicketCategoryId { get; set; }

    [Range(
        1,
        10,
        ErrorMessage = "You can reserve between 1 and 10 seats."
    )]
    public int Quantity { get; set; } = 1;

    public decimal Amount { get; set; }

    [Required]
    public string CardHolder { get; set; } = "";

    [Required]
    [RegularExpression(
        @"^\d{16}$",
        ErrorMessage = "Card number must contain exactly 16 digits."
    )]
    public string CardNumber { get; set; } = "";

    [Required]
    [RegularExpression(
        @"^(0[1-9]|1[0-2])/\d{2}$",
        ErrorMessage = "Expiry must be in MM/YY format."
    )]
    public string Expiry { get; set; } = "";

    [Required]
    [RegularExpression(
        @"^\d{3,4}$",
        ErrorMessage = "CVV must contain 3 or 4 digits."
    )]
    public string Cvv { get; set; } = "";
}

// ============================================================
// CONTACT FORM VIEW MODEL
// Used by the Contact Us page.
// ============================================================

public class ContactVm
{
    [Required]
    public string Name { get; set; } = "";

    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    public string Subject { get; set; } = "";

    [Required]
    [MinLength(
        10,
        ErrorMessage = "Message must contain at least 10 characters."
    )]
    public string Message { get; set; } = "";
}


// ============================================================
// ADMIN DASHBOARD VIEW MODEL
// Contains summary statistics displayed on the
// KMC Administrator Dashboard.
// ============================================================

public class AdminDashboardVm
{
    // Total number of accounts including Admin,
    // Organizers and Public Users.
    public int Users { get; set; }


    // Total registered Event Organizer accounts.
    public int Organizers { get; set; }


    // Total registered Public User accounts.
    public int PublicUsers { get; set; }


    // Total events stored in the system.
    public int Events { get; set; }


    // Total events currently published to the public.
    public int PublishedEvents { get; set; }


    // Total event reservations.
    public int Reservations { get; set; }


    // Reservations waiting for organizer approval.
    public int PendingApprovals { get; set; }


    // Reservations approved by event organizers.
    public int ApprovedReservations { get; set; }


    // Reservations where payment is still pending.
    public int PendingPayments { get; set; }


    // Total revenue received from reservations
    // where PaymentStatus is Paid.
    public decimal PaidRevenue { get; set; }
}


// ============================================================
// ADMIN USER ROW VIEW MODEL
// Represents users displayed in the Admin User Management table.
// ============================================================

public class UserRowVm
{
    public int Id { get; set; }

    public string FullName { get; set; } = "";

    public string? CompanyName { get; set; }

    public string Nic { get; set; } = "";

    public string Email { get; set; } = "";

    public string Role { get; set; } = "";

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}


// ============================================================
// ADMIN EVENT ROW VIEW MODEL
// Represents an event displayed in the Admin Event table.
// ============================================================

public class AdminEventRowVm
{
    public int Id { get; set; }

    public string Title { get; set; } = "";

    public string Category { get; set; } = "";

    public DateTime EventDate { get; set; }

    public DateTime RegistrationCloseDate { get; set; }

    public string Location { get; set; } = "";

    public int Capacity { get; set; }

    public bool IsPublished { get; set; }

    public string Organizer { get; set; } = "";

    public string? Company { get; set; }

    public bool OrganizerActive { get; set; }
}

// ============================================================
// API MESSAGE VIEW MODEL
// Used to receive success/error messages from the Web API.
// ============================================================

public class ApiMessage
{
    public string Message { get; set; } = "";
}

public class ForgotPasswordVm
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";
}

public class ResetPasswordVm
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, RegularExpression(@"^\d{6}$", ErrorMessage = "Reset code must contain exactly 6 digits.")]
    public string Code { get; set; } = "";

    [Required, MinLength(8)]
    public string NewPassword { get; set; } = "";

    [Required, Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";
}
// ============================================================
// User Report View Model
// ============================================================
public class PublicUserReportVm
{
    public int Id { get; set; }

    public string FullName { get; set; } = "";

    public string Nic { get; set; } = "";

    public string Email { get; set; } = "";

    public bool IsActive { get; set; }

    public int TotalReservations { get; set; }

    public int ApprovedReservations { get; set; }

    public int TotalEvents { get; set; }

    public int TotalSeats { get; set; }

    public decimal TotalSpent { get; set; }
}

public class MonthlyRevenueVm
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int Reservations { get; set; }
    public decimal Revenue { get; set; }
}
public class OrganizerIncomeVm
{
    public int Id { get; set; }

    public string FullName { get; set; } = "";

    public string? CompanyName { get; set; }

    public string Email { get; set; } = "";

    public bool IsActive { get; set; }

    public int TotalEvents { get; set; }

    public int PublishedEvents { get; set; }

    public int TotalReservations { get; set; }

    public int ApprovedReservations { get; set; }

    public int TotalSeatsSold { get; set; }

    public decimal PaidIncome { get; set; }

    public decimal PendingIncome { get; set; }
}

public class ContactMessageVm
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string Email { get; set; } = "";

    public string Subject { get; set; } = "";

    public string Message { get; set; } = "";

    public DateTime CreatedAt { get; set; }
}