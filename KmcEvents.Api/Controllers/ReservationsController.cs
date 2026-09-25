using KmcEvents.Api.Data;
using KmcEvents.Api.DTOs;
using KmcEvents.Api.Models;
using KmcEvents.Api.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using System.Globalization;
using System.Security.Claims;

namespace KmcEvents.Api.Controllers;

[ApiController]
[Route("api/reservations")]
public class ReservationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEmailService _email;
    private readonly QrCodeService _qr;
    private readonly ILogger<ReservationsController> _logger;

    public ReservationsController(
        AppDbContext db,
        IEmailService email,
        QrCodeService qr,
        ILogger<ReservationsController> logger)
    {
        _db = db;
        _email = email;
        _qr = qr;
        _logger = logger;
    }


    // ========================================================
    // PAY LATER RESERVATION
    // Reservation is created immediately.
    // Organizer approval is required.
    // Email failure does not cancel the reservation.
    // ========================================================

    [HttpPost]
    [Authorize(Roles = Roles.Public)]
    public async Task<IActionResult> Create(
        ReservationCreateDto dto)
    {
        if (dto.Quantity < 1 ||
            dto.Quantity > 10)
        {
            return BadRequest(
                new
                {
                    message =
                        "You can reserve between 1 and 10 seats."
                }
            );
        }


        // ----------------------------------------------------
        // CARD PAYMENT
        // Do not create reservation yet.
        // ----------------------------------------------------

        if (dto.PaymentMethod == "Card")
        {
            return Ok(
                new
                {
                    requiresPayment = true,
                    dto.EventId,
                    dto.TicketCategoryId,
                    dto.Quantity,

                    message =
                        "Continue to card payment. No reservation has been created yet."
                }
            );
        }


        // ----------------------------------------------------
        // VALIDATE PAY LATER
        // ----------------------------------------------------

        if (dto.PaymentMethod != "Pay Later")
        {
            return BadRequest(
                new
                {
                    message =
                        "Invalid payment method."
                }
            );
        }


        // ----------------------------------------------------
        // LOAD EVENT
        // ----------------------------------------------------

        var eventItem =
            await _db.Events
                .Include(x => x.TicketCategories)
                .Include(x => x.Organizer)
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == dto.EventId &&
                        x.IsPublished &&
                        x.Organizer != null &&
                        x.Organizer.IsActive
                );


        if (eventItem == null)
        {
            return NotFound(
                new
                {
                    message =
                        "Event not found or unavailable."
                }
            );
        }


        // ----------------------------------------------------
        // VALIDATE RESERVATION
        // ----------------------------------------------------

        var validation =
            await ValidateReservation(
                eventItem,
                dto.TicketCategoryId,
                dto.Quantity
            );


        if (!validation.Ok)
        {
            return BadRequest(
                new
                {
                    message =
                        validation.Message
                }
            );
        }


        var ticket =
            validation.Ticket!;


        // ----------------------------------------------------
        // CREATE PAY LATER RESERVATION
        // ----------------------------------------------------

        var reservation =
            new Reservation
            {
                EventId =
                    eventItem.Id,

                UserId =
                    UserId(),

                TicketCategoryId =
                    ticket.Id,

                Quantity =
                    dto.Quantity,

                UnitPrice =
                    ticket.Price,

                TotalAmount =
                    ticket.Price *
                    dto.Quantity,

                PaymentMethod =
                    "Pay Later",

                PaymentStatus =
                    "Pending",

                ApprovalStatus =
                    "Pending",

                SeatNumbers =
                    "",

                BookingReference =
                    "",

                QrToken =
                    Guid.NewGuid()
                        .ToString("N"),

                ReservedAt =
                    DateTime.UtcNow
            };


        _db.Reservations.Add(
            reservation
        );


        await _db.SaveChangesAsync();


        // ----------------------------------------------------
        // PUBLIC USER EMAIL
        // Email failure must not fail reservation.
        // ----------------------------------------------------

        var publicUser =
            await _db.Users.FindAsync(
                UserId()
            );


        if (publicUser != null)
        {
            await TrySendEmailAsync(
                publicUser.Email,
                "KMC Reservation Submitted",
                BuildPendingEmail(
                    publicUser.FullName,
                    eventItem.Title,
                    eventItem.Organizer,
                    ticket.Name,
                    dto.Quantity,
                    reservation.TotalAmount
                )
            );
        }


        // ----------------------------------------------------
        // ORGANIZER EMAIL
        // ----------------------------------------------------

        if (eventItem.Organizer != null)
        {
            await TrySendEmailAsync(
                eventItem.Organizer.Email,
                "New KMC Pay Later Reservation",
                $"""
                <div style="font-family:Arial,sans-serif;">

                    <h2>
                        New Reservation Request
                    </h2>

                    <p>
                        A new Pay Later reservation has been
                        submitted for
                        <strong>{eventItem.Title}</strong>.
                    </p>

                    <p>
                        Reservation ID:
                        <strong>#{reservation.Id}</strong>
                    </p>

                    <p>
                        Ticket Category:
                        <strong>{ticket.Name}</strong>
                    </p>

                    <p>
                        Quantity:
                        <strong>{reservation.Quantity}</strong>
                    </p>

                    <p>
                        Total:
                        <strong>
                            LKR {reservation.TotalAmount:N2}
                        </strong>
                    </p>

                    <p>
                        Please review the reservation in your
                        organizer dashboard.
                    </p>

                </div>
                """
            );
        }


        return Ok(
            new
            {
                reservation.Id,
                reservation.TotalAmount,
                reservation.PaymentMethod,
                reservation.PaymentStatus,
                reservation.ApprovalStatus,

                TicketCategory =
                    ticket.Name,

                message =
                    "Pay Later reservation submitted. Waiting for organizer approval."
            }
        );
    }


    // ========================================================
    // CARD CHECKOUT
    // No reservation exists before successful payment.
    // Successful payment:
    // - creates reservation
    // - records payment
    // - auto approves reservation
    // - generates seat numbers
    // - generates QR
    // - attempts email
    // ========================================================

    [HttpPost("card-checkout")]
    [Authorize(Roles = Roles.Public)]
    public async Task<IActionResult> CardCheckout(
        CardCheckoutDto dto)
    {
        // ----------------------------------------------------
        // QUANTITY VALIDATION
        // ----------------------------------------------------

        if (dto.Quantity < 1 ||
            dto.Quantity > 10)
        {
            return BadRequest(
                new
                {
                    message =
                        "You can reserve between 1 and 10 seats."
                }
            );
        }


        // ----------------------------------------------------
        // CARD NUMBER VALIDATION
        // ----------------------------------------------------

        if (string.IsNullOrWhiteSpace(
                dto.CardNumber) ||
            dto.CardNumber.Length != 16 ||
            !dto.CardNumber.All(char.IsDigit))
        {
            return BadRequest(
                new
                {
                    message =
                        "Card number must contain exactly 16 digits."
                }
            );
        }


        if (!IsValidLuhn(
                dto.CardNumber))
        {
            return BadRequest(
                new
                {
                    message =
                        "Invalid card number."
                }
            );
        }


        // ----------------------------------------------------
        // EXPIRY VALIDATION
        // ----------------------------------------------------

        if (!ExpiryValid(
                dto.Expiry))
        {
            return BadRequest(
                new
                {
                    message =
                        "Card expiry date is invalid or expired."
                }
            );
        }


        // ----------------------------------------------------
        // CVV VALIDATION
        // ----------------------------------------------------

        if (string.IsNullOrWhiteSpace(
                dto.Cvv) ||
            (dto.Cvv.Length != 3 &&
             dto.Cvv.Length != 4) ||
            !dto.Cvv.All(char.IsDigit))
        {
            return BadRequest(
                new
                {
                    message =
                        "CVV must contain 3 or 4 digits."
                }
            );
        }


        // ----------------------------------------------------
        // CARD HOLDER VALIDATION
        // ----------------------------------------------------

        if (string.IsNullOrWhiteSpace(
                dto.CardHolder))
        {
            return BadRequest(
                new
                {
                    message =
                        "Card holder name is required."
                }
            );
        }


        // ----------------------------------------------------
        // LOAD PUBLIC USER
        // ----------------------------------------------------

        var publicUser =
            await _db.Users
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == UserId() &&
                        x.IsActive
                );


        if (publicUser == null)
        {
            return Unauthorized(
                new
                {
                    message =
                        "User account is unavailable."
                }
            );
        }


        // ----------------------------------------------------
        // LOAD EVENT
        // ----------------------------------------------------

        var eventItem =
            await _db.Events
                .Include(x => x.TicketCategories)
                .Include(x => x.Organizer)
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == dto.EventId &&
                        x.IsPublished &&
                        x.Organizer != null &&
                        x.Organizer.IsActive
                );


        if (eventItem == null)
        {
            return NotFound(
                new
                {
                    message =
                        "Event not found or unavailable."
                }
            );
        }


        // ----------------------------------------------------
        // VALIDATE EVENT / TICKET / SEATS
        // ----------------------------------------------------

        var validation =
            await ValidateReservation(
                eventItem,
                dto.TicketCategoryId,
                dto.Quantity
            );


        if (!validation.Ok)
        {
            return BadRequest(
                new
                {
                    message =
                        validation.Message
                }
            );
        }


        var ticket =
            validation.Ticket!;


        var totalAmount =
            ticket.Price *
            dto.Quantity;


        var seatNumbers =
            await GenerateSeatNumbers(
                ticket,
                dto.Quantity
            );


        var bookingReference =
            GenerateBookingReference();


        var last4 =
            dto.CardNumber[^4..];


        // ----------------------------------------------------
        // DATABASE TRANSACTION
        // ----------------------------------------------------

        Reservation reservation;


        await using var transaction =
            await _db.Database
                .BeginTransactionAsync();


        try
        {
            // ------------------------------------------------
            // CREATE RESERVATION ONLY AFTER CARD VALIDATION
            // ------------------------------------------------

            reservation =
                new Reservation
                {
                    EventId =
                        eventItem.Id,

                    UserId =
                        publicUser.Id,

                    TicketCategoryId =
                        ticket.Id,

                    Quantity =
                        dto.Quantity,

                    UnitPrice =
                        ticket.Price,

                    TotalAmount =
                        totalAmount,

                    PaymentMethod =
                        "Card",

                    PaymentStatus =
                        "Paid",

                    ApprovalStatus =
                        "Approved",

                    SeatNumbers =
                        seatNumbers,

                    BookingReference =
                        bookingReference,

                    QrToken =
                        Guid.NewGuid()
                            .ToString("N"),

                    ReservedAt =
                        DateTime.UtcNow
                };


            _db.Reservations.Add(
                reservation
            );


            await _db.SaveChangesAsync();


            // ------------------------------------------------
            // SAVE SAFE PAYMENT INFORMATION
            // FULL CARD NUMBER AND CVV ARE NOT SAVED
            // ------------------------------------------------

            var payment =
                new Payment
                {
                    ReservationId =
                        reservation.Id,

                    CardHolder =
                        dto.CardHolder.Trim(),

                    MaskedCardNumber =
                        $"**** **** **** {last4}",

                    Last4 =
                        last4,

                    Expiry =
                        dto.Expiry,

                    Status =
                        "Success",

                    Reference =
                        $"PAY-{DateTime.UtcNow:yyyyMMddHHmmss}-{reservation.Id}",

                    Amount =
                        totalAmount,

                    PaidAt =
                        DateTime.UtcNow
                };


            _db.Payments.Add(
                payment
            );


            await _db.SaveChangesAsync();


            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();

            throw;
        }


        // ----------------------------------------------------
        // QR GENERATION
        // ----------------------------------------------------

        var organizerName =
            OrganizerName(
                eventItem.Organizer
            );


        var qrText =
            BuildQrText(
                reservation,
                eventItem.Title,
                organizerName,
                ticket.Name,
                publicUser.FullName
            );


        var qrBytes =
            _qr.GenerateBytes(
                qrText
            );


        // ----------------------------------------------------
        // QR EMAIL
        // Email failure does not cancel successful booking.
        // ----------------------------------------------------

        await TrySendEmailAsync(
            publicUser.Email,
            $"KMC QR Ticket - {eventItem.Title}",
            BuildTicketEmail(
                publicUser.FullName,
                eventItem.Title,
                organizerName,
                eventItem.Organizer?.Email ?? "",
                ticket.Name,
                reservation.SeatNumbers,
                reservation.Quantity,
                reservation.TotalAmount,
                "Paid",
                reservation.BookingReference
            ),
            qrBytes,
            $"{reservation.BookingReference}-QR.png"
        );


        return Ok(
            new
            {
                reservation.Id,
                reservation.BookingReference,
                reservation.SeatNumbers,
                reservation.TotalAmount,
                reservation.PaymentStatus,
                reservation.ApprovalStatus,

                QrCode =
                    _qr.GenerateDataUrl(
                        qrText
                    ),

                message =
                    "Card payment successful. Reservation approved automatically and QR ticket generated."
            }
        );
    }


    // ========================================================
    // PUBLIC USER - MY RESERVATIONS
    // ========================================================

    [HttpGet("mine")]
    [Authorize(Roles = Roles.Public)]
    public async Task<IActionResult> Mine()
    {
        var reservations =
            await _db.Reservations
                .AsNoTracking()
                .Where(
                    x =>
                        x.UserId ==
                        UserId()
                )
                .Include(x => x.Event)
                    .ThenInclude(
                        x => x!.Organizer
                    )
                .Include(x => x.User)
                .Include(x => x.TicketCategory)
                .Include(x => x.Payment)
                .OrderByDescending(
                    x => x.ReservedAt
                )
                .ToListAsync();


        var result =
            reservations
                .Select(
                    reservation =>
                    {
                        var organizerName =
                            OrganizerName(
                                reservation
                                    .Event?
                                    .Organizer
                            );


                        var qrText =
                            BuildQrText(
                                reservation,
                                reservation
                                    .Event?
                                    .Title ?? "",
                                organizerName,
                                reservation
                                    .TicketCategory?
                                    .Name ?? "",
                                reservation
                                    .User?
                                    .FullName ?? ""
                            );


                        return new
                        {
                            reservation.Id,

                            reservation.EventId,

                            EventTitle =
                                reservation
                                    .Event?
                                    .Title ?? "",

                            EventDate =
                                reservation
                                    .Event?
                                    .EventDate,

                            Location =
                                reservation
                                    .Event?
                                    .Location ?? "",

                            Organizer =
                                organizerName,

                            OrganizerEmail =
                                reservation
                                    .Event?
                                    .Organizer?
                                    .Email ?? "",

                            TicketCategory =
                                reservation
                                    .TicketCategory?
                                    .Name ?? "",

                            reservation.Quantity,

                            reservation.SeatNumbers,

                            reservation.BookingReference,

                            reservation.TotalAmount,

                            reservation.PaymentMethod,

                            reservation.PaymentStatus,

                            reservation.ApprovalStatus,

                            reservation.ReservedAt,

                            QrCode =
                                reservation.ApprovalStatus ==
                                "Approved"
                                    ? _qr.GenerateDataUrl(
                                        qrText
                                    )
                                    : null,

                            CancellationMessage =
                                "For cancellation due to an emergency or unavoidable situation, please contact the event organizer directly."
                        };
                    }
                )
                .ToList();


        return Ok(
            result
        );
    }


    // ========================================================
    // ORGANIZER RESERVATIONS
    // ========================================================

    [HttpGet("organizer")]
    [Authorize(Roles = Roles.Organizer)]
    public async Task<IActionResult> OrganizerReservations()
    {
        var organizerId =
            UserId();


        var reservations =
            await _db.Reservations
                .AsNoTracking()
                .Where(
                    x =>
                        x.Event != null &&
                        x.Event.OrganizerId ==
                        organizerId
                )
                .OrderByDescending(
                    x => x.ReservedAt
                )
                .Select(
                    x =>
                        new
                        {
                            x.Id,

                            EventTitle =
                                x.Event!.Title,

                            Customer =
                                x.User!.FullName,

                            x.User.Email,

                            TicketCategory =
                                x.TicketCategory!.Name,

                            x.Quantity,

                            x.SeatNumbers,

                            x.BookingReference,

                            x.TotalAmount,

                            x.PaymentMethod,

                            x.PaymentStatus,

                            x.ApprovalStatus,

                            AutoApproved =
                                x.PaymentMethod == "Card" &&
                                x.PaymentStatus == "Paid",

                            x.ReservedAt
                        }
                )
                .ToListAsync();


        return Ok(
            reservations
        );
    }


    // ========================================================
    // ORGANIZER APPROVAL
    // Manual approval only for Pay Later reservations.
    // ========================================================

    [HttpPatch("{id:int}/approval")]
    [Authorize(Roles = Roles.Organizer)]
    public async Task<IActionResult> Approval(
        int id,
        [FromQuery] string status)
    {
        if (status != "Approved" &&
            status != "Rejected")
        {
            return BadRequest(
                new
                {
                    message =
                        "Status must be Approved or Rejected."
                }
            );
        }


        var organizerId =
            UserId();


        var reservation =
            await _db.Reservations
                .Include(x => x.Event)
                    .ThenInclude(
                        x => x!.Organizer
                    )
                .Include(x => x.User)
                .Include(x => x.TicketCategory)
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == id
                );


        if (reservation == null)
        {
            return NotFound(
                new
                {
                    message =
                        "Reservation not found."
                }
            );
        }


        // ----------------------------------------------------
        // ORGANIZER OWNERSHIP CHECK
        // ----------------------------------------------------

        if (reservation
                .Event?
                .OrganizerId !=
            organizerId)
        {
            return Forbid();
        }


        // ----------------------------------------------------
        // CARD RESERVATIONS AUTO APPROVED
        // ----------------------------------------------------

        if (reservation.PaymentMethod == "Card")
        {
            return BadRequest(
                new
                {
                    message =
                        "Paid card reservations are automatically approved and cannot be manually approved."
                }
            );
        }


        // ----------------------------------------------------
        // PREVENT DUPLICATE APPROVAL / REJECTION
        // ----------------------------------------------------

        if (reservation.ApprovalStatus != "Pending")
        {
            return BadRequest(
                new
                {
                    message =
                        $"This reservation is already {reservation.ApprovalStatus.ToLowerInvariant()}."
                }
            );
        }


        reservation.ApprovalStatus =
            status;


        // ----------------------------------------------------
        // APPROVED
        // Generate seats + booking reference.
        // ----------------------------------------------------

        if (status == "Approved" &&
            string.IsNullOrWhiteSpace(
                reservation.SeatNumbers) &&
            reservation.TicketCategory != null)
        {
            reservation.SeatNumbers =
                await GenerateSeatNumbers(
                    reservation.TicketCategory,
                    reservation.Quantity
                );


            if (string.IsNullOrWhiteSpace(
                    reservation.BookingReference))
            {
                reservation.BookingReference =
                    GenerateBookingReference();
            }
        }


        await _db.SaveChangesAsync();


        if (reservation.User == null ||
            reservation.Event == null)
        {
            return Ok(
                new
                {
                    message =
                        $"Reservation {status.ToLowerInvariant()}."
                }
            );
        }


        // ----------------------------------------------------
        // REJECTED EMAIL
        // ----------------------------------------------------

        if (status == "Rejected")
        {
            await TrySendEmailAsync(
                reservation.User.Email,
                "KMC Reservation Rejected",
                $"""
                <div style="font-family:Arial,sans-serif;">

                    <h2>
                        Reservation Rejected
                    </h2>

                    <p>
                        Hello
                        <strong>
                            {reservation.User.FullName}
                        </strong>,
                    </p>

                    <p>
                        Your reservation for
                        <strong>
                            {reservation.Event.Title}
                        </strong>
                        has been rejected by the event organizer.
                    </p>

                    <p>
                        If you require further information,
                        please contact the event organizer.
                    </p>

                </div>
                """
            );


            return Ok(
                new
                {
                    message =
                        "Reservation rejected."
                }
            );
        }


        // ----------------------------------------------------
        // APPROVED QR
        // ----------------------------------------------------

        var organizerName =
            OrganizerName(
                reservation
                    .Event
                    .Organizer
            );


        var ticketName =
            reservation
                .TicketCategory?
                .Name ?? "";


        var qrText =
            BuildQrText(
                reservation,
                reservation.Event.Title,
                organizerName,
                ticketName,
                reservation.User.FullName
            );


        var qrBytes =
            _qr.GenerateBytes(
                qrText
            );


        // ----------------------------------------------------
        // APPROVED EMAIL + QR
        // ----------------------------------------------------

        await TrySendEmailAsync(
            reservation.User.Email,
            $"KMC QR Ticket - {reservation.Event.Title}",
            BuildTicketEmail(
                reservation.User.FullName,
                reservation.Event.Title,
                organizerName,
                reservation.Event.Organizer?.Email ?? "",
                ticketName,
                reservation.SeatNumbers,
                reservation.Quantity,
                reservation.TotalAmount,
                reservation.PaymentStatus,
                reservation.BookingReference
            ),
            qrBytes,
            $"{reservation.BookingReference}-QR.png"
        );


        return Ok(
            new
            {
                message =
                    "Reservation approved. QR ticket generated."
            }
        );
    }


    // ========================================================
    // RESERVATION VALIDATION
    // ========================================================

    private async Task<(
        bool Ok,
        string Message,
        TicketCategory? Ticket)>
        ValidateReservation(
            CityEvent eventItem,
            int ticketCategoryId,
            int quantity)
    {
        // ----------------------------------------------------
        // EVENT DATE
        // ----------------------------------------------------

        if (eventItem.EventDate <=
            DateTime.Now)
        {
            return (
                false,
                "This event has already started or ended.",
                null
            );
        }


        // ----------------------------------------------------
        // REGISTRATION CLOSE DATE
        // ----------------------------------------------------

        if (eventItem.RegistrationCloseDate <=
            DateTime.Now)
        {
            return (
                false,
                "Registration for this event is closed.",
                null
            );
        }


        // ----------------------------------------------------
        // TICKET CATEGORY
        // ----------------------------------------------------

        var ticket =
            eventItem
                .TicketCategories
                .FirstOrDefault(
                    x =>
                        x.Id ==
                        ticketCategoryId
                );


        if (ticket == null)
        {
            return (
                false,
                "Invalid ticket category.",
                null
            );
        }


        // ----------------------------------------------------
        // EVENT CAPACITY
        // ----------------------------------------------------

        var eventBookedSeats =
            await _db.Reservations
                .Where(
                    x =>
                        x.EventId ==
                        eventItem.Id &&
                        x.ApprovalStatus !=
                        "Rejected"
                )
                .SumAsync(
                    x =>
                        (int?)x.Quantity
                )
            ?? 0;


        if (eventBookedSeats +
            quantity >
            eventItem.Capacity)
        {
            return (
                false,
                $"Only {Math.Max(0, eventItem.Capacity - eventBookedSeats)} seats are available for this event.",
                null
            );
        }


        // ----------------------------------------------------
        // CATEGORY CAPACITY
        // ----------------------------------------------------

        var categoryBookedSeats =
            await _db.Reservations
                .Where(
                    x =>
                        x.EventId ==
                        eventItem.Id &&
                        x.TicketCategoryId ==
                        ticket.Id &&
                        x.ApprovalStatus !=
                        "Rejected"
                )
                .SumAsync(
                    x =>
                        (int?)x.Quantity
                )
            ?? 0;


        if (categoryBookedSeats +
            quantity >
            ticket.SeatLimit)
        {
            return (
                false,
                $"Only {Math.Max(0, ticket.SeatLimit - categoryBookedSeats)} seats are available for {ticket.Name}.",
                null
            );
        }


        return (
            true,
            "",
            ticket
        );
    }


    // ========================================================
    // SEAT NUMBER GENERATION
    // ========================================================

    private async Task<string> GenerateSeatNumbers(
        TicketCategory ticket,
        int quantity)
    {
        var used =
            await _db.Reservations
                .Where(
                    x =>
                        x.TicketCategoryId ==
                        ticket.Id &&
                        x.ApprovalStatus !=
                        "Rejected"
                )
                .SumAsync(
                    x =>
                        (int?)x.Quantity
                )
            ?? 0;


        var prefix =
            new string(
                ticket.Name
                    .Where(
                        char.IsLetterOrDigit
                    )
                    .ToArray()
            )
            .ToUpperInvariant();


        if (prefix.Length > 3)
        {
            prefix =
                prefix[..3];
        }


        if (string.IsNullOrWhiteSpace(
                prefix))
        {
            prefix =
                "SEAT";
        }


        return string.Join(
            ", ",
            Enumerable
                .Range(
                    used + 1,
                    quantity
                )
                .Select(
                    x =>
                        $"{prefix}-{x:000}"
                )
        );
    }


    // ========================================================
    // BOOKING REFERENCE GENERATION
    // ========================================================

    private static string GenerateBookingReference()
    {
        var code =
            Guid.NewGuid()
                .ToString("N")[..6]
                .ToUpperInvariant();


        return
            $"KMC-{DateTime.UtcNow:yyyyMMdd}-{code}";
    }


    // ========================================================
    // QR TICKET CONTENT
    // ========================================================

    private static string BuildQrText(
        Reservation reservation,
        string eventName,
        string organizer,
        string ticket,
        string participant)
    {
        return
            $"KMC EVENT TICKET\n" +
            $"Reference: {reservation.BookingReference}\n" +
            $"Event: {eventName}\n" +
            $"Organizer: {organizer}\n" +
            $"Participant: {participant}\n" +
            $"Ticket: {ticket}\n" +
            $"Seat Number: {reservation.SeatNumbers}\n" +
            $"Quantity: {reservation.Quantity}\n" +
            $"Payment: {reservation.PaymentStatus}\n" +
            $"Approval: {reservation.ApprovalStatus}\n" +
            $"Token: {reservation.QrToken}";
    }


    // ========================================================
    // QR TICKET EMAIL TEMPLATE
    // ========================================================

    private static string BuildTicketEmail(
        string participant,
        string eventName,
        string organizer,
        string organizerEmail,
        string ticket,
        string seats,
        int quantity,
        decimal total,
        string paymentStatus,
        string reference)
    {
        return $"""
        <div style="font-family:Arial,sans-serif;background:#f5f2f1;padding:30px;">

            <div style="max-width:650px;margin:auto;background:#ffffff;border-radius:14px;overflow:hidden;">

                <div style="background:#8d153a;color:#ffffff;padding:22px 28px;">

                    <h2 style="margin:0;">
                        KMC Event Ticket
                    </h2>

                    <p style="margin:5px 0 0;">
                        Kandy Municipal Council Event Management
                    </p>

                </div>


                <div style="padding:28px;">

                    <p>
                        Hello
                        <strong>{participant}</strong>,
                    </p>

                    <p>
                        Your reservation has been confirmed.
                        Your QR ticket is attached to this email.
                    </p>


                    <table style="width:100%;border-collapse:collapse;">

                        <tr>
                            <td style="padding:8px 0;">
                                <strong>
                                    Booking Reference
                                </strong>
                            </td>

                            <td>
                                {reference}
                            </td>
                        </tr>

                        <tr>
                            <td style="padding:8px 0;">
                                <strong>
                                    Event
                                </strong>
                            </td>

                            <td>
                                {eventName}
                            </td>
                        </tr>

                        <tr>
                            <td style="padding:8px 0;">
                                <strong>
                                    Organizer
                                </strong>
                            </td>

                            <td>
                                {organizer}
                            </td>
                        </tr>

                        <tr>
                            <td style="padding:8px 0;">
                                <strong>
                                    Ticket Category
                                </strong>
                            </td>

                            <td>
                                {ticket}
                            </td>
                        </tr>

                        <tr>
                            <td style="padding:8px 0;">
                                <strong>
                                    Seat Number
                                </strong>
                            </td>

                            <td>
                                {seats}
                            </td>
                        </tr>

                        <tr>
                            <td style="padding:8px 0;">
                                <strong>
                                    Quantity
                                </strong>
                            </td>

                            <td>
                                {quantity}
                            </td>
                        </tr>

                        <tr>
                            <td style="padding:8px 0;">
                                <strong>
                                    Total
                                </strong>
                            </td>

                            <td>
                                LKR {total:N2}
                            </td>
                        </tr>

                        <tr>
                            <td style="padding:8px 0;">
                                <strong>
                                    Payment
                                </strong>
                            </td>

                            <td>
                                {paymentStatus}
                            </td>
                        </tr>

                        <tr>
                            <td style="padding:8px 0;">
                                <strong>
                                    Status
                                </strong>
                            </td>

                            <td>
                                Approved
                            </td>
                        </tr>

                    </table>


                    <div style="margin-top:22px;padding:16px;background:#fff6e5;border-left:4px solid #ffb81c;">

                        <strong>
                            Need to cancel?
                        </strong>

                        <p style="margin:7px 0 0;">
                            If you need to cancel because of an
                            emergency or unavoidable situation,
                            please contact the event organizer directly.
                        </p>

                        <p style="margin:7px 0 0;">

                            <strong>
                                {organizer}
                            </strong>

                            <br />

                            {organizerEmail}

                        </p>

                    </div>

                </div>


                <div style="background:#2d0b13;color:#ffffff;padding:16px;text-align:center;">

                    Kandy Municipal Council

                </div>

            </div>

        </div>
        """;
    }


    // ========================================================
    // PAY LATER EMAIL TEMPLATE
    // ========================================================

    private static string BuildPendingEmail(
        string participant,
        string eventName,
        AppUser? organizer,
        string ticket,
        int quantity,
        decimal total)
    {
        return $"""
        <div style="font-family:Arial,sans-serif;">

            <h2>
                Reservation Submitted
            </h2>

            <p>
                Hello
                <strong>{participant}</strong>,
            </p>

            <p>
                Your Pay Later reservation for
                <strong>{eventName}</strong>
                has been submitted.
            </p>

            <p>
                Ticket:
                <strong>{ticket}</strong>
            </p>

            <p>
                Seats:
                <strong>{quantity}</strong>
            </p>

            <p>
                Total:
                <strong>
                    LKR {total:N2}
                </strong>
            </p>

            <p>
                Payment:
                <strong>Pending</strong>
            </p>

            <p>
                Approval:
                <strong>
                    Waiting for organizer approval
                </strong>
            </p>

            <p>
                Once approved, your QR ticket will be
                generated and emailed to you.
            </p>

            <p>
                If you need to cancel due to an emergency
                or unavoidable situation, please contact
                <strong>
                    {OrganizerName(organizer)}
                </strong>
                directly.
            </p>

        </div>
        """;
    }


    // ========================================================
    // ORGANIZER DISPLAY NAME
    // ========================================================

    private static string OrganizerName(
        AppUser? organizer)
    {
        if (organizer == null)
        {
            return
                "Event Organizer";
        }


        return
            !string.IsNullOrWhiteSpace(
                organizer.CompanyName
            )
                ? organizer.CompanyName
                : organizer.FullName;
    }


    // ========================================================
    // SAFE EMAIL SENDER
    // SMTP failure must not fail reservation/payment.
    // ========================================================

    private async Task TrySendEmailAsync(
        string to,
        string subject,
        string body,
        byte[]? attachment = null,
        string? attachmentName = null)
    {
        try
        {
            await _email.SendAsync(
                to,
                subject,
                body,
                attachment,
                attachmentName
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Email could not be sent to {Email}. The main reservation operation completed successfully.",
                to
            );
        }
    }


    // ========================================================
    // CURRENT USER ID
    // ========================================================

    private int UserId()
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );


        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new UnauthorizedAccessException(
                "User ID claim was not found."
            );
        }


        return int.Parse(
            value
        );
    }


    // ========================================================
    // CARD EXPIRY VALIDATION
    // ========================================================

    private static bool ExpiryValid(
        string expiry)
    {
        if (!DateTime.TryParseExact(
                "01/" + expiry,
                "dd/MM/yy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return false;
        }


        return
            date.AddMonths(1) >
            DateTime.Today;
    }


    // ========================================================
    // LUHN CARD NUMBER VALIDATION
    // ========================================================

    private static bool IsValidLuhn(
        string number)
    {
        if (number.Length != 16 ||
            !number.All(char.IsDigit))
        {
            return false;
        }


        var sum =
            0;


        var alternate =
            false;


        for (var i =
                 number.Length - 1;
             i >= 0;
             i--)
        {
            var digit =
                number[i] - '0';


            if (alternate)
            {
                digit *= 2;


                if (digit > 9)
                {
                    digit -= 9;
                }
            }


            sum +=
                digit;


            alternate =
                !alternate;
        }


        return
            sum % 10 == 0;
    }
}