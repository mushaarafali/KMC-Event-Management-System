using KmcEvents.Api.Data;
using KmcEvents.Api.Models;

using Microsoft.EntityFrameworkCore;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KmcEvents.Api.Services;

// ============================================================
// ADMIN REPORT PDF SERVICE
// Generates the complete KMC administrative PDF report.
// ============================================================

public class AdminReportPdfService
{
    private readonly AppDbContext _db;

    public AdminReportPdfService(AppDbContext db)
    {
        _db = db;
    }

    // ========================================================
    // GENERATE COMPLETE ADMIN REPORT
    // ========================================================

    public async Task<byte[]> GenerateAsync()
    {
        var generatedAt = DateTime.Now;

        // ====================================================
        // MONTHLY REVENUE DATA
        // ====================================================

        var monthlyRevenue = await _db.Reservations
            .AsNoTracking()
            .Where(x => x.PaymentStatus == "Paid")
            .GroupBy(x => new
            {
                x.ReservedAt.Year,
                x.ReservedAt.Month
            })
            .Select(group => new MonthlyRevenuePdfRow
            {
                Year = group.Key.Year,

                Month = group.Key.Month,

                Reservations = group.Count(),

                Revenue = group.Sum(x => x.TotalAmount)
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToListAsync();


        // ====================================================
        // PUBLIC USER REPORT DATA
        // ====================================================

        var publicUsers = await _db.Users
            .AsNoTracking()
            .Where(x => x.Role == Roles.Public)
            .Select(user => new PublicUserPdfRow
            {
                FullName = user.FullName,

                Nic = user.Nic,

                Email = user.Email,

                IsActive = user.IsActive,

                TotalEvents = _db.Reservations
                    .Where(r =>
                        r.UserId == user.Id &&
                        r.ApprovalStatus == "Approved")
                    .Select(r => r.EventId)
                    .Distinct()
                    .Count(),

                Reservations = _db.Reservations
                    .Count(r =>
                        r.UserId == user.Id),

                Approved = _db.Reservations
                    .Count(r =>
                        r.UserId == user.Id &&
                        r.ApprovalStatus == "Approved"),

                Seats = _db.Reservations
                    .Where(r =>
                        r.UserId == user.Id &&
                        r.ApprovalStatus != "Rejected")
                    .Sum(r => (int?)r.Quantity)
                    ?? 0,

                TotalSpent = _db.Reservations
                    .Where(r =>
                        r.UserId == user.Id &&
                        r.PaymentStatus == "Paid")
                    .Sum(r => (decimal?)r.TotalAmount)
                    ?? 0
            })
            .OrderByDescending(x => x.TotalSpent)
            .ToListAsync();


        // ====================================================
        // ORGANIZER REPORT DATA
        // ====================================================

        var organizers = await _db.Users
            .AsNoTracking()
            .Where(x => x.Role == Roles.Organizer)
            .Select(organizer => new OrganizerPdfRow
            {
                FullName = organizer.FullName,

                CompanyName = organizer.CompanyName,

                Email = organizer.Email,

                IsActive = organizer.IsActive,

                TotalEvents = _db.Events
                    .Count(e =>
                        e.OrganizerId == organizer.Id),

                PublishedEvents = _db.Events
                    .Count(e =>
                        e.OrganizerId == organizer.Id &&
                        e.IsPublished),

                Reservations = _db.Reservations
                    .Count(r =>
                        r.Event != null &&
                        r.Event.OrganizerId == organizer.Id),

                Approved = _db.Reservations
                    .Count(r =>
                        r.Event != null &&
                        r.Event.OrganizerId == organizer.Id &&
                        r.ApprovalStatus == "Approved"),

                SeatsSold = _db.Reservations
                    .Where(r =>
                        r.Event != null &&
                        r.Event.OrganizerId == organizer.Id &&
                        r.ApprovalStatus != "Rejected")
                    .Sum(r => (int?)r.Quantity)
                    ?? 0,

                PaidIncome = _db.Reservations
                    .Where(r =>
                        r.Event != null &&
                        r.Event.OrganizerId == organizer.Id &&
                        r.PaymentStatus == "Paid")
                    .Sum(r => (decimal?)r.TotalAmount)
                    ?? 0,

                PendingIncome = _db.Reservations
                    .Where(r =>
                        r.Event != null &&
                        r.Event.OrganizerId == organizer.Id &&
                        r.PaymentStatus != "Paid" &&
                        r.ApprovalStatus != "Rejected")
                    .Sum(r => (decimal?)r.TotalAmount)
                    ?? 0
            })
            .OrderByDescending(x => x.PaidIncome)
            .ToListAsync();


        // ====================================================
        // SYSTEM SUMMARY DATA
        // ====================================================

        var totalRevenue =
            monthlyRevenue.Sum(x => x.Revenue);

        var totalReservations =
            await _db.Reservations.CountAsync();

        var totalUsers =
            await _db.Users.CountAsync();

        var totalPublicUsers =
            await _db.Users.CountAsync(
                x => x.Role == Roles.Public
            );

        var totalOrganizers =
            await _db.Users.CountAsync(
                x => x.Role == Roles.Organizer
            );

        var totalEvents =
            await _db.Events.CountAsync();

        var publishedEvents =
            await _db.Events.CountAsync(
                x => x.IsPublished
            );


        // ====================================================
        // GENERATE PDF DOCUMENT
        // ====================================================

        return Document
            .Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());

                    page.Margin(28);

                    page.DefaultTextStyle(
                        style => style
                            .FontSize(8)
                            .FontFamily("Arial")
                    );


                    // ========================================
                    // HEADER
                    // ========================================

                    page.Header()
                        .Column(column =>
                        {
                            column.Item()
                                .Text("KANDY MUNICIPAL COUNCIL")
                                .FontSize(10)
                                .Bold()
                                .FontColor("#8D0034");

                            column.Item()
                                .Text("Event Management Platform")
                                .FontSize(21)
                                .Bold()
                                .FontColor("#2C1015");

                            column.Item()
                                .Text("Administrative Report")
                                .FontSize(14)
                                .SemiBold()
                                .FontColor("#005B50");

                            column.Item()
                                .PaddingTop(4)
                                .Text(
                                    $"Generated: {generatedAt:dd MMMM yyyy | hh:mm tt}"
                                )
                                .FontSize(8)
                                .FontColor(Colors.Grey.Darken1);

                            column.Item()
                                .PaddingTop(10)
                                .LineHorizontal(2)
                                .LineColor("#8D0034");
                        });


                    // ========================================
                    // CONTENT
                    // ========================================

                    page.Content()
                        .PaddingVertical(18)
                        .Column(column =>
                        {
                            column.Spacing(20);


                            // =================================
                            // SYSTEM SUMMARY
                            // =================================

                            column.Item()
                                .Text("System Summary")
                                .FontSize(15)
                                .Bold()
                                .FontColor("#2C1015");


                            column.Item()
                                .Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn();

                                        columns.RelativeColumn();

                                        columns.RelativeColumn();

                                        columns.RelativeColumn();

                                        columns.RelativeColumn();

                                        columns.RelativeColumn();

                                        columns.RelativeColumn();
                                    });


                                    SummaryCell(
                                        table,
                                        totalUsers.ToString(),
                                        "Total Users"
                                    );

                                    SummaryCell(
                                        table,
                                        totalPublicUsers.ToString(),
                                        "Public Users"
                                    );

                                    SummaryCell(
                                        table,
                                        totalOrganizers.ToString(),
                                        "Organizers"
                                    );

                                    SummaryCell(
                                        table,
                                        totalEvents.ToString(),
                                        "Total Events"
                                    );

                                    SummaryCell(
                                        table,
                                        publishedEvents.ToString(),
                                        "Published"
                                    );

                                    SummaryCell(
                                        table,
                                        totalReservations.ToString(),
                                        "Reservations"
                                    );

                                    SummaryCell(
                                        table,
                                        $"LKR {totalRevenue:N2}",
                                        "Paid Revenue"
                                    );
                                });


                            // =================================
                            // MONTHLY REVENUE
                            // =================================

                            column.Item()
                                .Text("Monthly Revenue")
                                .FontSize(14)
                                .Bold()
                                .FontColor("#8D0034");


                            if (monthlyRevenue.Count == 0)
                            {
                                column.Item()
                                    .Text(
                                        "No paid reservation revenue is currently available."
                                    )
                                    .FontColor(Colors.Grey.Darken1);
                            }
                            else
                            {
                                column.Item()
                                    .Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.RelativeColumn(2);

                                            columns.RelativeColumn();

                                            columns.RelativeColumn(2);

                                            columns.RelativeColumn(2);
                                        });


                                        HeaderCell(
                                            table,
                                            "Month"
                                        );

                                        HeaderCell(
                                            table,
                                            "Year"
                                        );

                                        HeaderCell(
                                            table,
                                            "Paid Reservations"
                                        );

                                        HeaderCell(
                                            table,
                                            "Revenue"
                                        );


                                        foreach (var row in monthlyRevenue)
                                        {
                                            var monthName =
                                                new DateTime(
                                                    row.Year,
                                                    row.Month,
                                                    1
                                                )
                                                .ToString("MMMM");


                                            BodyCell(
                                                table,
                                                monthName
                                            );

                                            BodyCell(
                                                table,
                                                row.Year.ToString()
                                            );

                                            BodyCell(
                                                table,
                                                row.Reservations.ToString()
                                            );

                                            BodyCell(
                                                table,
                                                $"LKR {row.Revenue:N2}"
                                            );
                                        }
                                    });
                            }


                            // =================================
                            // PUBLIC USER PARTICIPATION
                            // =================================

                            column.Item()
                                .Text("Public User Participation Report")
                                .FontSize(14)
                                .Bold()
                                .FontColor("#8D0034");


                            if (publicUsers.Count == 0)
                            {
                                column.Item()
                                    .Text(
                                        "No public user information is currently available."
                                    )
                                    .FontColor(Colors.Grey.Darken1);
                            }
                            else
                            {
                                column.Item()
                                    .Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.RelativeColumn(2.4f);

                                            columns.RelativeColumn(1.3f);

                                            columns.RelativeColumn();

                                            columns.RelativeColumn();

                                            columns.RelativeColumn();

                                            columns.RelativeColumn();

                                            columns.RelativeColumn();

                                            columns.RelativeColumn(1.5f);
                                        });


                                        HeaderCell(table, "User");

                                        HeaderCell(table, "NIC");

                                        HeaderCell(table, "Status");

                                        HeaderCell(table, "Events");

                                        HeaderCell(table, "Reservations");

                                        HeaderCell(table, "Approved");

                                        HeaderCell(table, "Seats");

                                        HeaderCell(table, "Total Spent");


                                        foreach (var user in publicUsers)
                                        {
                                            BodyCell(
                                                table,
                                                $"{user.FullName}\n{user.Email}"
                                            );

                                            BodyCell(
                                                table,
                                                user.Nic
                                            );

                                            BodyCell(
                                                table,
                                                user.IsActive
                                                    ? "Active"
                                                    : "Disabled"
                                            );

                                            BodyCell(
                                                table,
                                                user.TotalEvents.ToString()
                                            );

                                            BodyCell(
                                                table,
                                                user.Reservations.ToString()
                                            );

                                            BodyCell(
                                                table,
                                                user.Approved.ToString()
                                            );

                                            BodyCell(
                                                table,
                                                user.Seats.ToString()
                                            );

                                            BodyCell(
                                                table,
                                                $"LKR {user.TotalSpent:N2}"
                                            );
                                        }
                                    });
                            }


                            // =================================
                            // ORGANIZER INCOME REPORT
                            // =================================

                            column.Item()
                                .Text("Organizer Income Report")
                                .FontSize(14)
                                .Bold()
                                .FontColor("#8D0034");


                            if (organizers.Count == 0)
                            {
                                column.Item()
                                    .Text(
                                        "No event organizer information is currently available."
                                    )
                                    .FontColor(Colors.Grey.Darken1);
                            }
                            else
                            {
                                column.Item()
                                    .Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.RelativeColumn(2.5f);

                                            columns.RelativeColumn();

                                            columns.RelativeColumn();

                                            columns.RelativeColumn();

                                            columns.RelativeColumn();

                                            columns.RelativeColumn();

                                            columns.RelativeColumn();

                                            columns.RelativeColumn(1.5f);

                                            columns.RelativeColumn(1.5f);
                                        });


                                        HeaderCell(
                                            table,
                                            "Organizer"
                                        );

                                        HeaderCell(
                                            table,
                                            "Status"
                                        );

                                        HeaderCell(
                                            table,
                                            "Events"
                                        );

                                        HeaderCell(
                                            table,
                                            "Published"
                                        );

                                        HeaderCell(
                                            table,
                                            "Reservations"
                                        );

                                        HeaderCell(
                                            table,
                                            "Approved"
                                        );

                                        HeaderCell(
                                            table,
                                            "Seats"
                                        );

                                        HeaderCell(
                                            table,
                                            "Paid Income"
                                        );

                                        HeaderCell(
                                            table,
                                            "Pending Income"
                                        );


                                        foreach (var organizer in organizers)
                                        {
                                            var organizerDetails =
                                                organizer.FullName;


                                            if (
                                                !string.IsNullOrWhiteSpace(
                                                    organizer.CompanyName
                                                )
                                            )
                                            {
                                                organizerDetails +=
                                                    $"\n{organizer.CompanyName}";
                                            }


                                            organizerDetails +=
                                                $"\n{organizer.Email}";


                                            BodyCell(
                                                table,
                                                organizerDetails
                                            );

                                            BodyCell(
                                                table,
                                                organizer.IsActive
                                                    ? "Active"
                                                    : "Disabled"
                                            );

                                            BodyCell(
                                                table,
                                                organizer.TotalEvents.ToString()
                                            );

                                            BodyCell(
                                                table,
                                                organizer.PublishedEvents.ToString()
                                            );

                                            BodyCell(
                                                table,
                                                organizer.Reservations.ToString()
                                            );

                                            BodyCell(
                                                table,
                                                organizer.Approved.ToString()
                                            );

                                            BodyCell(
                                                table,
                                                organizer.SeatsSold.ToString()
                                            );

                                            BodyCell(
                                                table,
                                                $"LKR {organizer.PaidIncome:N2}"
                                            );

                                            BodyCell(
                                                table,
                                                $"LKR {organizer.PendingIncome:N2}"
                                            );
                                        }
                                    });
                            }
                        });


                    // ========================================
                    // FOOTER
                    // ========================================

                    page.Footer()
                        .Column(column =>
                        {
                            column.Item()
                                .LineHorizontal(1)
                                .LineColor("#E4D9D4");

                            column.Item()
                                .PaddingTop(6)
                                .Row(row =>
                                {
                                    row.RelativeItem()
                                        .Text(
                                            "Kandy Municipal Council | KMC Event Management"
                                        )
                                        .FontSize(7)
                                        .FontColor(Colors.Grey.Darken1);

                                    row.RelativeItem()
                                        .AlignRight()
                                        .Text(text =>
                                        {
                                            text.DefaultTextStyle(
                                                style =>
                                                    style
                                                        .FontSize(7)
                                                        .FontColor(
                                                            Colors.Grey.Darken1
                                                        )
                                            );

                                            text.Span("Page ");

                                            text.CurrentPageNumber();

                                            text.Span(" of ");

                                            text.TotalPages();
                                        });
                                });
                        });
                });
            })
            .GeneratePdf();
    }


    // ========================================================
    // SUMMARY CELL
    // ========================================================

    private static void SummaryCell(
        TableDescriptor table,
        string value,
        string title)
    {
        table.Cell()
            .Border(1)
            .BorderColor("#E4D9D4")
            .Background("#FFF9F6")
            .Padding(9)
            .Column(column =>
            {
                column.Item()
                    .Text(value)
                    .Bold()
                    .FontSize(11)
                    .FontColor("#8D0034");

                column.Item()
                    .PaddingTop(2)
                    .Text(title)
                    .FontSize(7)
                    .FontColor(Colors.Grey.Darken1);
            });
    }


    // ========================================================
    // TABLE HEADER CELL
    // ========================================================

    private static void HeaderCell(
        TableDescriptor table,
        string text)
    {
        table.Cell()
            .Background("#8D0034")
            .PaddingVertical(6)
            .PaddingHorizontal(5)
            .Text(text)
            .Bold()
            .FontSize(7)
            .FontColor(Colors.White);
    }


    // ========================================================
    // TABLE BODY CELL
    // ========================================================

    private static void BodyCell(
        TableDescriptor table,
        string text)
    {
        table.Cell()
            .BorderBottom(1)
            .BorderColor("#E9E1DD")
            .PaddingVertical(6)
            .PaddingHorizontal(5)
            .Text(text)
            .FontSize(7);
    }


    // ========================================================
    // MONTHLY REVENUE PDF MODEL
    // ========================================================

    private class MonthlyRevenuePdfRow
    {
        public int Year { get; set; }

        public int Month { get; set; }

        public int Reservations { get; set; }

        public decimal Revenue { get; set; }
    }


    // ========================================================
    // PUBLIC USER PDF MODEL
    // ========================================================

    private class PublicUserPdfRow
    {
        public string FullName { get; set; } = "";

        public string Nic { get; set; } = "";

        public string Email { get; set; } = "";

        public bool IsActive { get; set; }

        public int TotalEvents { get; set; }

        public int Reservations { get; set; }

        public int Approved { get; set; }

        public int Seats { get; set; }

        public decimal TotalSpent { get; set; }
    }


    // ========================================================
    // ORGANIZER PDF MODEL
    // ========================================================

    private class OrganizerPdfRow
    {
        public string FullName { get; set; } = "";

        public string? CompanyName { get; set; }

        public string Email { get; set; } = "";

        public bool IsActive { get; set; }

        public int TotalEvents { get; set; }

        public int PublishedEvents { get; set; }

        public int Reservations { get; set; }

        public int Approved { get; set; }

        public int SeatsSold { get; set; }

        public decimal PaidIncome { get; set; }

        public decimal PendingIncome { get; set; }
    }
}