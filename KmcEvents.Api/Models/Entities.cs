using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KmcEvents.Api.Models;

public static class Roles
{
    public const string Admin = "KMC Admin";
    public const string Organizer = "Event Organizer";
    public const string Public = "Public";
}

public class AppUser
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string FullName { get; set; } = "";

    [MaxLength(150)]
    public string? CompanyName { get; set; }

    [MaxLength(12)]
    public string Nic { get; set; } = "";

    [MaxLength(180)]
    public string Email { get; set; } = "";

    public string PasswordHash { get; set; } = "";

    [MaxLength(30)]
    public string Role { get; set; } = Roles.Public;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Forgot password reset code.
    [MaxLength(120)]
    public string? PasswordResetCode { get; set; }

    // Reset code expiration time.
    public DateTime? PasswordResetCodeExpiry { get; set; }
}
public class CityEvent
{
    public int Id { get; set; }
    [MaxLength(160)] public string Title { get; set; } = "";
    [MaxLength(1200)] public string Description { get; set; } = "";
    [MaxLength(80)] public string Category { get; set; } = "";
    [MaxLength(180)] public string Location { get; set; } = "";
    public DateTime EventDate { get; set; }
    public DateTime RegistrationCloseDate { get; set; }
    public int Capacity { get; set; }
    [MaxLength(400)] public string? ImageUrl { get; set; }
    public bool IsPublished { get; set; }
    public int OrganizerId { get; set; }
    public AppUser? Organizer { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<TicketCategory> TicketCategories { get; set; } = [];
}

public class TicketCategory
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public CityEvent? Event { get; set; }
    [MaxLength(60)] public string Name { get; set; } = "General";
    [Column(TypeName="decimal(18,2)")] public decimal Price { get; set; }
    public int SeatLimit { get; set; }
}

public class Reservation
{
    public int Id { get; set; }

    public int EventId { get; set; }

    public CityEvent? Event { get; set; }

    public int UserId { get; set; }

    public AppUser? User { get; set; }

    public int TicketCategoryId { get; set; }

    public TicketCategory? TicketCategory { get; set; }

    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [MaxLength(30)]
    public string PaymentMethod { get; set; } = "Pay Later";

    [MaxLength(30)]
    public string PaymentStatus { get; set; } = "Pending";

    [MaxLength(30)]
    public string ApprovalStatus { get; set; } = "Pending";

    [MaxLength(200)]
    public string SeatNumbers { get; set; } = "";

    [MaxLength(50)]
    public string BookingReference { get; set; } = "";

    [MaxLength(80)]
    public string QrToken { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime ReservedAt { get; set; } = DateTime.UtcNow;

    public Payment? Payment { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public Reservation? Reservation { get; set; }
    [MaxLength(120)] public string CardHolder { get; set; } = "";
    [MaxLength(19)] public string MaskedCardNumber { get; set; } = "";
    [MaxLength(4)] public string Last4 { get; set; } = "";
    [MaxLength(5)] public string Expiry { get; set; } = "";
    [MaxLength(40)] public string Status { get; set; } = "Success";
    [MaxLength(80)] public string Reference { get; set; } = "";
    [Column(TypeName="decimal(18,2)")] public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
}

public class ContactMessage
{
    public int Id { get; set; }
    [MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(180)] public string Email { get; set; } = "";
    [MaxLength(160)] public string Subject { get; set; } = "";
    [MaxLength(2000)] public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
