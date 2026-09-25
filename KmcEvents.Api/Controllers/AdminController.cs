using KmcEvents.Api.Data;
using KmcEvents.Api.Models;
using KmcEvents.Api.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KmcEvents.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = Roles.Admin)]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEmailService _email;
    private readonly AdminReportPdfService _pdf;
    public AdminController(
       AppDbContext db,
       IEmailService email,
       AdminReportPdfService pdf)
    {
        _db = db;
        _email = email;
        _pdf = pdf;
    }

    // ========================================================
    // ADMIN DASHBOARD SUMMARY
    // ========================================================

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var totalUsers = await _db.Users.CountAsync();
        var totalOrganizers = await _db.Users.CountAsync(x => x.Role == Roles.Organizer);
        var totalPublicUsers = await _db.Users.CountAsync(x => x.Role == Roles.Public);
        var totalEvents = await _db.Events.CountAsync();
        var publishedEvents = await _db.Events.CountAsync(x => x.IsPublished);
        var totalReservations = await _db.Reservations.CountAsync();
        var pendingApprovals = await _db.Reservations.CountAsync(x => x.ApprovalStatus == "Pending");
        var approvedReservations = await _db.Reservations.CountAsync(x => x.ApprovalStatus == "Approved");
        var pendingPayments = await _db.Reservations.CountAsync(x => x.PaymentStatus == "Pending");

        var paidRevenue = await _db.Reservations
            .Where(x => x.PaymentStatus == "Paid")
            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0;

        return Ok(new
        {
            Users = totalUsers,
            Organizers = totalOrganizers,
            PublicUsers = totalPublicUsers,
            Events = totalEvents,
            PublishedEvents = publishedEvents,
            Reservations = totalReservations,
            PendingApprovals = pendingApprovals,
            ApprovedReservations = approvedReservations,
            PendingPayments = pendingPayments,
            PaidRevenue = paidRevenue
        });
    }

    // ========================================================
    // USER MANAGEMENT
    // ========================================================

    [HttpGet("users")]
    public async Task<IActionResult> Users()
    {
        var users = await _db.Users
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.FullName,
                x.CompanyName,
                x.Nic,
                x.Email,
                x.Role,
                x.IsActive,
                x.CreatedAt
            })
            .ToListAsync();

        return Ok(users);
    }

    // ========================================================
    // ENABLE / DISABLE USER
    // Sends email when account status changes.
    // ========================================================

    [HttpPatch("users/{id:int}/active")]
    public async Task<IActionResult> Active(int id, [FromQuery] bool value)
    {
        var user = await _db.Users.FindAsync(id);

        if (user == null)
            return NotFound(new { message = "User not found." });

        if (user.Role == Roles.Admin)
            return BadRequest(new { message = "Admin account cannot be disabled." });

        if (user.IsActive == value)
        {
            return Ok(new
            {
                message = value
                    ? "User is already enabled."
                    : "User is already disabled."
            });
        }

        user.IsActive = value;

        await _db.SaveChangesAsync();

        var subject = value
            ? "KMC Account Re-Enabled"
            : "KMC Account Disabled";

        var body = value
            ? $@"
                <html>
                <body style='font-family:Arial;background:#f6f3f1;padding:30px;'>
                    <div style='max-width:600px;margin:auto;background:white;border-radius:12px;overflow:hidden;'>
                        <div style='background:#005b50;color:white;padding:25px;text-align:center;'>
                            <h2>KMC Account Re-Enabled</h2>
                        </div>

                        <div style='padding:30px;'>
                            <p>Dear {user.FullName},</p>

                            <p>Your KMC Event Management account has been re-enabled by the KMC System Administrator.</p>

                            <p>You can now login and use the system again.</p>

                            <p><strong>Email:</strong> {user.Email}</p>
                            <p><strong>Account Type:</strong> {user.Role}</p>
                        </div>

                        <div style='background:#2c1015;color:white;padding:20px;text-align:center;'>
                            Kandy Municipal Council
                        </div>
                    </div>
                </body>
                </html>"
            : $@"
                <html>
                <body style='font-family:Arial;background:#f6f3f1;padding:30px;'>
                    <div style='max-width:600px;margin:auto;background:white;border-radius:12px;overflow:hidden;'>
                        <div style='background:#8d0034;color:white;padding:25px;text-align:center;'>
                            <h2>KMC Account Disabled</h2>
                        </div>

                        <div style='padding:30px;'>
                            <p>Dear {user.FullName},</p>

                            <p>Your KMC Event Management account has been disabled by the KMC System Administrator.</p>

                            <p>You will not be able to login until your account is re-enabled.</p>

                            <p><strong>Email:</strong> {user.Email}</p>
                            <p><strong>Account Type:</strong> {user.Role}</p>

                            <p>If you believe this action was made by mistake, please contact KMC Administration.</p>
                        </div>

                        <div style='background:#2c1015;color:white;padding:20px;text-align:center;'>
                            Kandy Municipal Council
                        </div>
                    </div>
                </body>
                </html>";

        await _email.SendAsync(user.Email, subject, body);

        return Ok(new
        {
            message = value
                ? "User enabled successfully."
                : "User disabled successfully."
        });
    }

    // ========================================================
    // MANAGE ALL EVENTS
    // ========================================================

    [HttpGet("events")]
    public async Task<IActionResult> Events()
    {
        var events = await _db.Events
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Category,
                x.EventDate,
                x.RegistrationCloseDate,
                x.Location,
                x.Capacity,
                x.IsPublished,
                Organizer = x.Organizer != null ? x.Organizer.FullName : "",
                Company = x.Organizer != null ? x.Organizer.CompanyName : "",
                OrganizerActive = x.Organizer != null && x.Organizer.IsActive
            })
            .ToListAsync();

        return Ok(events);
    }

    // ========================================================
    // MANAGE ALL RESERVATIONS
    // ========================================================

    [HttpGet("reservations")]
    public async Task<IActionResult> Reservations()
    {
        var reservations = await _db.Reservations
            .AsNoTracking()
            .OrderByDescending(x => x.ReservedAt)
            .Select(x => new
            {
                x.Id,
                EventTitle = x.Event != null ? x.Event.Title : "",
                Customer = x.User != null ? x.User.FullName : "",
                Email = x.User != null ? x.User.Email : "",
                TicketCategory = x.TicketCategory != null ? x.TicketCategory.Name : "",
                x.Quantity,
                x.TotalAmount,
                x.PaymentMethod,
                x.PaymentStatus,
                x.ApprovalStatus,
                x.ReservedAt
            })
            .ToListAsync();

        return Ok(reservations);
    }


    // ========================================================
    // MONTHLY REVENUE REPORT
    // ========================================================
    [HttpGet("reports/monthly-revenue")]
    public async Task<IActionResult> MonthlyRevenue()
    {
        var report = await _db.Reservations
            .AsNoTracking()
            .Where(x => x.PaymentStatus == "Paid")
            .GroupBy(x => new
            {
                x.ReservedAt.Year,
                x.ReservedAt.Month
            })
            .Select(group => new
            {
                Year = group.Key.Year,
                Month = group.Key.Month,
                Reservations = group.Count(),
                Revenue = group.Sum(x => x.TotalAmount)
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToListAsync();

        return Ok(report);
    }

    // ========================================================
    // Public User Report
    // ========================================================
    [HttpGet("reports/public-users")]
    public async Task<IActionResult> PublicUserReport()
    {
        var users = await _db.Users
            .AsNoTracking()
            .Where(x => x.Role == Roles.Public)
            .Select(user => new
            {
                user.Id,
                user.FullName,
                user.Nic,
                user.Email,
                user.IsActive,

                TotalReservations = _db.Reservations.Count(r =>
                    r.UserId == user.Id),

                ApprovedReservations = _db.Reservations.Count(r =>
                    r.UserId == user.Id &&
                    r.ApprovalStatus == "Approved"),

                TotalEvents = _db.Reservations
                    .Where(r =>
                        r.UserId == user.Id &&
                        r.ApprovalStatus == "Approved")
                    .Select(r => r.EventId)
                    .Distinct()
                    .Count(),

                TotalSeats = _db.Reservations
                    .Where(r =>
                        r.UserId == user.Id &&
                        r.ApprovalStatus != "Rejected")
                    .Sum(r => (int?)r.Quantity) ?? 0,

                TotalSpent = _db.Reservations
                    .Where(r =>
                        r.UserId == user.Id &&
                        r.PaymentStatus == "Paid")
                    .Sum(r => (decimal?)r.TotalAmount) ?? 0
            })
            .OrderByDescending(x => x.TotalSpent)
            .ToListAsync();

        return Ok(users);
    }

    // ========================================================
    // ORGANIZER INCOME REPORT
    // ========================================================

    [HttpGet("reports/organizers")]
    public async Task<IActionResult> OrganizerIncomeReport()
    {
        var organizers = await _db.Users
            .AsNoTracking()
            .Where(x => x.Role == Roles.Organizer)
            .Select(organizer => new
            {
                organizer.Id,
                organizer.FullName,
                organizer.CompanyName,
                organizer.Email,
                organizer.IsActive,

                TotalEvents = _db.Events.Count(e =>
                    e.OrganizerId == organizer.Id),

                PublishedEvents = _db.Events.Count(e =>
                    e.OrganizerId == organizer.Id &&
                    e.IsPublished),

                TotalReservations = _db.Reservations.Count(r =>
                    r.Event != null &&
                    r.Event.OrganizerId == organizer.Id),

                ApprovedReservations = _db.Reservations.Count(r =>
                    r.Event != null &&
                    r.Event.OrganizerId == organizer.Id &&
                    r.ApprovalStatus == "Approved"),

                TotalSeatsSold = _db.Reservations
                    .Where(r =>
                        r.Event != null &&
                        r.Event.OrganizerId == organizer.Id &&
                        r.ApprovalStatus != "Rejected")
                    .Sum(r => (int?)r.Quantity) ?? 0,

                PaidIncome = _db.Reservations
                    .Where(r =>
                        r.Event != null &&
                        r.Event.OrganizerId == organizer.Id &&
                        r.PaymentStatus == "Paid")
                    .Sum(r => (decimal?)r.TotalAmount) ?? 0,

                PendingIncome = _db.Reservations
                    .Where(r =>
                        r.Event != null &&
                        r.Event.OrganizerId == organizer.Id &&
                        r.PaymentStatus != "Paid" &&
                        r.ApprovalStatus != "Rejected")
                    .Sum(r => (decimal?)r.TotalAmount) ?? 0
            })
            .OrderByDescending(x => x.PaidIncome)
            .ToListAsync();

        return Ok(organizers);
    }
    // ========================================================
    // GENERATE COMPLETE ADMIN PDF REPORT
    // ========================================================

    [HttpGet("reports/pdf")]
    public async Task<IActionResult> GeneratePdfReport()
    {
        var pdf = await _pdf.GenerateAsync();

        var fileName =
            $"KMC_Admin_Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

        return File(
            pdf,
            "application/pdf",
            fileName
        );
    }

    // ========================================================
    // CONTACT MESSAGES
    // KMC Admin can view all contact messages.
    // ========================================================

    [HttpGet("contact-messages")]
    public async Task<IActionResult> ContactMessages()
    {
        var messages = await _db.ContactMessages
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Email,
                x.Subject,
                x.Message,
                x.CreatedAt
            })
            .ToListAsync();

        return Ok(messages);
    }
}