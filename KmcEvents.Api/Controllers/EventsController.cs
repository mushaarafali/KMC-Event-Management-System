using KmcEvents.Api.Data;
using KmcEvents.Api.DTOs;
using KmcEvents.Api.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using System.Security.Claims;

namespace KmcEvents.Api.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly AppDbContext _db;

    public EventsController(AppDbContext db)
    {
        _db = db;
    }

    // ========================================================
    // PUBLIC EVENT SEARCH
    // Shows only published upcoming events of active organizers.
    // ========================================================

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Search(
        [FromQuery] string? q,
        [FromQuery] string? category,
        [FromQuery] string? location,
        [FromQuery] DateTime? date)
    {
        var query = _db.Events
            .AsNoTracking()
            .Where(x =>
                x.IsPublished &&
                x.EventDate >= DateTime.Today &&
                x.Organizer != null &&
                x.Organizer.IsActive
            );

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(x =>
                x.Title.Contains(q) ||
                x.Description.Contains(q)
            );
        }

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(x => x.Category == category);

        if (!string.IsNullOrWhiteSpace(location))
            query = query.Where(x => x.Location.Contains(location));

        if (date.HasValue)
        {
            var selectedDate = date.Value.Date;
            query = query.Where(x => x.EventDate.Date == selectedDate);
        }

        var result = await query
            .OrderBy(x => x.EventDate)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Description,
                x.Category,
                x.Location,
                x.EventDate,
                x.RegistrationCloseDate,
                x.Capacity,
                x.ImageUrl,
                x.OrganizerId,

                OrganizerName = x.Organizer != null
                    ? x.Organizer.FullName
                    : "",

                CompanyName = x.Organizer != null
                    ? x.Organizer.CompanyName
                    : "",

                TicketCategories = x.TicketCategories.Select(ticket => new
                {
                    ticket.Id,
                    ticket.Name,
                    ticket.Price,
                    ticket.SeatLimit,

                    BookedSeats = _db.Reservations
                        .Where(r =>
                            r.EventId == x.Id &&
                            r.TicketCategoryId == ticket.Id &&
                            r.ApprovalStatus != "Rejected")
                        .Sum(r => (int?)r.Quantity) ?? 0,

                    AvailableSeats = Math.Max(
                        0,
                        ticket.SeatLimit -
                        (
                            _db.Reservations
                                .Where(r =>
                                    r.EventId == x.Id &&
                                    r.TicketCategoryId == ticket.Id &&
                                    r.ApprovalStatus != "Rejected")
                                .Sum(r => (int?)r.Quantity) ?? 0
                        )
                    )
                }).ToList()
            })
            .ToListAsync();

        return Ok(result);
    }

    // ========================================================
    // PUBLIC EVENT DETAILS
    // Disabled organizer events cannot be opened directly.
    // ========================================================

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(int id)
    {
        var eventItem = await _db.Events
            .AsNoTracking()
            .Where(x =>
                x.Id == id &&
                x.IsPublished &&
                x.Organizer != null &&
                x.Organizer.IsActive
            )
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Description,
                x.Category,
                x.Location,
                x.EventDate,
                x.RegistrationCloseDate,
                x.Capacity,
                x.ImageUrl,
                x.IsPublished,
                x.OrganizerId,

                OrganizerName = x.Organizer != null
                    ? x.Organizer.FullName
                    : "",

                CompanyName = x.Organizer != null
                    ? x.Organizer.CompanyName
                    : "",

                TicketCategories = x.TicketCategories.Select(ticket => new
                {
                    ticket.Id,
                    ticket.Name,
                    ticket.Price,
                    ticket.SeatLimit,

                    BookedSeats = _db.Reservations
                        .Where(r =>
                            r.EventId == x.Id &&
                            r.TicketCategoryId == ticket.Id &&
                            r.ApprovalStatus != "Rejected")
                        .Sum(r => (int?)r.Quantity) ?? 0,

                    AvailableSeats = Math.Max(
                        0,
                        ticket.SeatLimit -
                        (
                            _db.Reservations
                                .Where(r =>
                                    r.EventId == x.Id &&
                                    r.TicketCategoryId == ticket.Id &&
                                    r.ApprovalStatus != "Rejected")
                                .Sum(r => (int?)r.Quantity) ?? 0
                        )
                    )
                }).ToList()
            })
            .FirstOrDefaultAsync();

        if (eventItem == null)
            return NotFound(new { message = "Event not found or currently unavailable." });

        var bookedSeats = await _db.Reservations
            .Where(r =>
                r.EventId == id &&
                r.ApprovalStatus != "Rejected")
            .SumAsync(r => (int?)r.Quantity) ?? 0;

        return Ok(new
        {
            eventItem.Id,
            eventItem.Title,
            eventItem.Description,
            eventItem.Category,
            eventItem.Location,
            eventItem.EventDate,
            eventItem.RegistrationCloseDate,
            eventItem.Capacity,
            eventItem.ImageUrl,
            eventItem.IsPublished,
            eventItem.OrganizerId,
            eventItem.OrganizerName,
            eventItem.CompanyName,

            BookedSeats = bookedSeats,
            AvailableSeats = Math.Max(0, eventItem.Capacity - bookedSeats),

            eventItem.TicketCategories
        });
    }

    // ========================================================
    // ORGANIZER - MY EVENTS
    // ========================================================

    [HttpGet("mine")]
    [Authorize(Roles = Roles.Organizer)]
    public async Task<IActionResult> Mine()
    {
        var userId = UserId();

        var events = await _db.Events
            .AsNoTracking()
            .Where(x => x.OrganizerId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Description,
                x.Category,
                x.Location,
                x.EventDate,
                x.RegistrationCloseDate,
                x.Capacity,
                x.ImageUrl,
                x.IsPublished,
                x.OrganizerId,
                x.CreatedAt,

                TicketCategories = x.TicketCategories.Select(ticket => new
                {
                    ticket.Id,
                    ticket.Name,
                    ticket.Price,
                    ticket.SeatLimit,

                    BookedSeats = _db.Reservations
                        .Where(r =>
                            r.EventId == x.Id &&
                            r.TicketCategoryId == ticket.Id &&
                            r.ApprovalStatus != "Rejected")
                        .Sum(r => (int?)r.Quantity) ?? 0,

                    AvailableSeats = Math.Max(
                        0,
                        ticket.SeatLimit -
                        (
                            _db.Reservations
                                .Where(r =>
                                    r.EventId == x.Id &&
                                    r.TicketCategoryId == ticket.Id &&
                                    r.ApprovalStatus != "Rejected")
                                .Sum(r => (int?)r.Quantity) ?? 0
                        )
                    )
                }).ToList()
            })
            .ToListAsync();

        return Ok(events);
    }

    // ========================================================
    // CREATE EVENT
    // Disabled organizer cannot create events.
    // ========================================================

    [HttpPost]
    [Authorize(Roles = Roles.Organizer)]
    public async Task<IActionResult> Create(EventUpsertDto dto)
    {
        var userId = UserId();

        if (!await OrganizerIsActive(userId))
        {
            return Unauthorized(new
            {
                message = "Your organizer account is disabled. You cannot create events."
            });
        }

        var validationResult = ValidateEvent(dto);

        if (validationResult != null)
            return validationResult;

        var eventItem = Map(dto, new CityEvent());

        eventItem.OrganizerId = userId;
        eventItem.CreatedAt = DateTime.UtcNow;

        _db.Events.Add(eventItem);
        await _db.SaveChangesAsync();

        return CreatedAtAction(
            nameof(Get),
            new { id = eventItem.Id },
            new
            {
                eventItem.Id,
                message = "Event created successfully."
            }
        );
    }

    // ========================================================
    // UPDATE EVENT
    // Organizer can update only their own event.
    // Existing ticket category IDs are preserved.
    // Ticket categories used by reservations are never deleted.
    // Disabled organizer cannot update.
    // ========================================================

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Organizer)]
    public async Task<IActionResult> Update(
        int id,
        EventUpsertDto dto)
    {
        var userId = UserId();

        // ====================================================
        // CHECK ORGANIZER ACCOUNT
        // ====================================================

        if (!await OrganizerIsActive(userId))
        {
            return Unauthorized(new
            {
                message =
                    "Your organizer account is disabled. " +
                    "You cannot update events."
            });
        }

        // ====================================================
        // VALIDATE EVENT
        // ====================================================

        var validationResult = ValidateEvent(dto);

        if (validationResult != null)
        {
            return validationResult;
        }

        // ====================================================
        // LOAD EVENT WITH TICKET CATEGORIES
        // ====================================================

        var eventItem = await _db.Events
            .Include(x => x.TicketCategories)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (eventItem == null)
        {
            return NotFound(new
            {
                message = "Event not found."
            });
        }

        // ====================================================
        // OWNERSHIP CHECK
        // ====================================================

        if (eventItem.OrganizerId != userId)
        {
            return Forbid();
        }

        // ====================================================
        // UPDATE BASIC EVENT DETAILS
        // ====================================================

        eventItem.Title = dto.Title.Trim();

        eventItem.Description =
            dto.Description.Trim();

        eventItem.Category =
            dto.Category.Trim();

        eventItem.Location =
            dto.Location.Trim();

        eventItem.EventDate =
            dto.EventDate;

        eventItem.RegistrationCloseDate =
            dto.RegistrationCloseDate;

        eventItem.Capacity =
            dto.Capacity;

        eventItem.ImageUrl =
            dto.ImageUrl;

        eventItem.IsPublished =
            dto.IsPublished;

        // ====================================================
        // UPDATE TICKET CATEGORIES
        // DO NOT DELETE EXISTING CATEGORIES
        // ====================================================

        foreach (var ticketDto in dto.TicketCategories)
        {
            TicketCategory? existingTicket = null;

            // ------------------------------------------------
            // FIRST TRY TO FIND BY ID
            // ------------------------------------------------

            if (ticketDto.Id > 0)
            {
                existingTicket =
                    eventItem.TicketCategories
                        .FirstOrDefault(x =>
                            x.Id == ticketDto.Id);
            }

            // ------------------------------------------------
            // IF ID NOT FOUND, TRY NAME
            // ------------------------------------------------

            if (existingTicket == null)
            {
                existingTicket =
                    eventItem.TicketCategories
                        .FirstOrDefault(x =>
                            x.Name.ToLower() ==
                            ticketDto.Name
                                .Trim()
                                .ToLower());
            }

            // ------------------------------------------------
            // UPDATE EXISTING TICKET CATEGORY
            // ------------------------------------------------

            if (existingTicket != null)
            {
                var bookedSeats =
                    await _db.Reservations
                        .Where(r =>
                            r.EventId == id &&
                            r.TicketCategoryId ==
                                existingTicket.Id &&
                            r.ApprovalStatus != "Rejected")
                        .SumAsync(r =>
                            (int?)r.Quantity) ?? 0;

                if (ticketDto.SeatLimit < bookedSeats)
                {
                    return BadRequest(new
                    {
                        message =
                            $"{existingTicket.Name} already has " +
                            $"{bookedSeats} reserved seats. " +
                            $"Seat limit cannot be lower than " +
                            $"{bookedSeats}."
                    });
                }

                existingTicket.Name =
                    ticketDto.Name.Trim();

                existingTicket.Price =
                    ticketDto.Price;

                existingTicket.SeatLimit =
                    ticketDto.SeatLimit;
            }

            // ------------------------------------------------
            // CREATE NEW TICKET CATEGORY
            // ------------------------------------------------

            else
            {
                var newTicket =
                    new TicketCategory
                    {
                        EventId = eventItem.Id,

                        Name =
                            ticketDto.Name.Trim(),

                        Price =
                            ticketDto.Price,

                        SeatLimit =
                            ticketDto.SeatLimit
                    };

                eventItem.TicketCategories.Add(
                    newTicket
                );
            }
        }

        // ====================================================
        // HANDLE REMOVED TICKET CATEGORIES SAFELY
        // ====================================================

        var submittedIds =
            dto.TicketCategories
                .Where(x => x.Id > 0)
                .Select(x => x.Id)
                .ToHashSet();

        var submittedNames =
            dto.TicketCategories
                .Select(x =>
                    x.Name.Trim().ToLower())
                .ToHashSet();

        var removedTickets =
            eventItem.TicketCategories
                .Where(x =>
                    !submittedIds.Contains(x.Id) &&
                    !submittedNames.Contains(
                        x.Name.ToLower()))
                .ToList();

        foreach (var removedTicket in removedTickets)
        {
            var hasReservations =
                await _db.Reservations
                    .AnyAsync(r =>
                        r.TicketCategoryId ==
                        removedTicket.Id);

            if (hasReservations)
            {
                return BadRequest(new
                {
                    message =
                        $"Ticket category " +
                        $"'{removedTicket.Name}' cannot be removed " +
                        $"because reservations already exist for it."
                });
            }

            _db.TicketCategories.Remove(
                removedTicket
            );
        }

        // ====================================================
        // FINAL CAPACITY CHECK
        // ====================================================

        var totalSeatLimit =
            dto.TicketCategories
                .Sum(x => x.SeatLimit);

        if (totalSeatLimit > dto.Capacity)
        {
            return BadRequest(new
            {
                message =
                    "Total ticket category seat limits " +
                    "cannot exceed the event capacity."
            });
        }

        // ====================================================
        // SAVE
        // ====================================================

        await _db.SaveChangesAsync();

        return Ok(new
        {
            id = eventItem.Id,
            message = "Event updated successfully."
        });
    }

    // ========================================================
    // DELETE EVENT
    // Organizer can delete own event.
    // Admin can delete any event.
    // Event with reservations cannot be deleted.
    // ========================================================

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Organizer + "," + Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var eventItem = await _db.Events.FindAsync(id);

        if (eventItem == null)
            return NotFound(new { message = "Event not found." });

        if (!User.IsInRole(Roles.Admin))
        {
            var userId = UserId();

            if (!await OrganizerIsActive(userId))
            {
                return Unauthorized(new
                {
                    message = "Your organizer account is disabled."
                });
            }

            if (eventItem.OrganizerId != userId)
                return Forbid();
        }

        var hasReservations = await _db.Reservations
            .AnyAsync(r => r.EventId == id);

        if (hasReservations)
        {
            return BadRequest(new
            {
                message = "Cannot delete an event with reservations. Unpublish it instead."
            });
        }

        _db.Events.Remove(eventItem);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Event deleted."
        });
    }

    // ========================================================
    // PUBLISH / UNPUBLISH EVENT
    // Disabled organizer cannot publish or unpublish.
    // ========================================================

    [HttpPatch("{id:int}/publish")]
    [Authorize(Roles = Roles.Organizer)]
    public async Task<IActionResult> Publish(int id, [FromQuery] bool value)
    {
        var userId = UserId();

        if (!await OrganizerIsActive(userId))
        {
            return Unauthorized(new
            {
                message = "Your organizer account is disabled. You cannot manage event publication."
            });
        }

        var eventItem = await _db.Events.FindAsync(id);

        if (eventItem == null)
            return NotFound(new { message = "Event not found." });

        if (eventItem.OrganizerId != userId)
            return Forbid();

        eventItem.IsPublished = value;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = value
                ? "Event published."
                : "Event unpublished."
        });
    }

    // ========================================================
    // CHECK ORGANIZER ACCOUNT STATUS
    // ========================================================

    private async Task<bool> OrganizerIsActive(int userId)
    {
        return await _db.Users
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id == userId &&
                x.Role == Roles.Organizer &&
                x.IsActive
            );
    }

    // ========================================================
    // CURRENT LOGGED-IN USER ID
    // ========================================================

    private int UserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userIdValue))
            throw new UnauthorizedAccessException("User ID claim was not found.");

        return int.Parse(userIdValue);
    }

    // ========================================================
    // EVENT VALIDATION
    // ========================================================

    private IActionResult? ValidateEvent(EventUpsertDto dto)
    {
        if (dto.EventDate.Date < DateTime.Today.AddDays(10))
        {
            return BadRequest(new
            {
                message = "Event date must be at least 10 days from today."
            });
        }

        if (dto.RegistrationCloseDate < DateTime.Today)
        {
            return BadRequest(new
            {
                message = "Registration close date cannot be in the past."
            });
        }

        if (dto.RegistrationCloseDate >= dto.EventDate)
        {
            return BadRequest(new
            {
                message = "Registration must close before the event starts."
            });
        }

        if (dto.Capacity <= 0)
        {
            return BadRequest(new
            {
                message = "Event capacity must be greater than zero."
            });
        }

        if (dto.TicketCategories == null || dto.TicketCategories.Count == 0)
        {
            return BadRequest(new
            {
                message = "At least one ticket category is required."
            });
        }

        var duplicateTicketName = dto.TicketCategories
            .GroupBy(x => x.Name.Trim().ToLowerInvariant())
            .Any(x => x.Count() > 1);

        if (duplicateTicketName)
        {
            return BadRequest(new
            {
                message = "Ticket category names must be unique."
            });
        }

        foreach (var ticket in dto.TicketCategories)
        {
            if (string.IsNullOrWhiteSpace(ticket.Name))
            {
                return BadRequest(new
                {
                    message = "Ticket category name is required."
                });
            }

            if (ticket.Price < 0)
            {
                return BadRequest(new
                {
                    message = "Ticket price cannot be negative."
                });
            }

            if (ticket.SeatLimit <= 0)
            {
                return BadRequest(new
                {
                    message = "Ticket seat limit must be greater than zero."
                });
            }
        }

        var totalTicketSeats = dto.TicketCategories.Sum(x => x.SeatLimit);

        if (totalTicketSeats > dto.Capacity)
        {
            return BadRequest(new
            {
                message =
                    $"Total ticket seat limits ({totalTicketSeats}) " +
                    $"cannot exceed event capacity ({dto.Capacity})."
            });
        }

        return null;
    }

    // ========================================================
    // MAP DTO TO EVENT ENTITY
    // ========================================================

    private static CityEvent Map(EventUpsertDto dto, CityEvent eventItem)
    {
        eventItem.Title = dto.Title.Trim();
        eventItem.Description = dto.Description.Trim();
        eventItem.Category = dto.Category.Trim();
        eventItem.Location = dto.Location.Trim();
        eventItem.EventDate = dto.EventDate;
        eventItem.RegistrationCloseDate = dto.RegistrationCloseDate;
        eventItem.Capacity = dto.Capacity;
        eventItem.ImageUrl = dto.ImageUrl;
        eventItem.IsPublished = dto.IsPublished;

        eventItem.TicketCategories = dto.TicketCategories
            .Select(ticket => new TicketCategory
            {
                Name = ticket.Name.Trim(),
                Price = ticket.Price,
                SeatLimit = ticket.SeatLimit
            })
            .ToList();

        return eventItem;
    }
}