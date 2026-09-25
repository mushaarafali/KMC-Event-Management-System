using KmcEvents.Client.Filters;
using KmcEvents.Client.Models;
using KmcEvents.Client.Services;

using Microsoft.AspNetCore.Mvc;

namespace KmcEvents.Client.Controllers;

// ============================================================
// EVENT ORGANIZER CONTROLLER
// Handles organizer dashboard, event CRUD,
// image upload, publish/unpublish and reservation approval.
// ============================================================

[SessionAuthorize("Event Organizer")]
public class OrganizerController : Controller
{
    private readonly ApiClient _api;

    public OrganizerController(ApiClient api)
    {
        _api = api;
    }

    // ========================================================
    // ORGANIZER DASHBOARD
    // ========================================================

    public async Task<IActionResult> Dashboard()
    {
        var events = await _api.GetAsync<List<EventVm>>(
            "events/mine"
        ) ?? new List<EventVm>();

        var reservations = await _api.GetAsync<List<ReservationVm>>(
            "reservations/organizer"
        ) ?? new List<ReservationVm>();

        ViewBag.Reservations = reservations;

        return View(events);
    }

    // ========================================================
    // CREATE EVENT - GET
    // ========================================================

    [HttpGet]
    public IActionResult Create()
    {
        return View(
            "EventForm",
            new EventVm()
        );
    }

    // ========================================================
    // CREATE EVENT - POST
    // ========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EventVm vm)
    {
        NormalizeTickets(vm);

        if (!ValidDates(vm))
        {
            return View(
                "EventForm",
                vm
            );
        }

        if (!ModelState.IsValid)
        {
            return View(
                "EventForm",
                vm
            );
        }

        // ----------------------------------------------------
        // UPLOAD EVENT IMAGE FROM LAPTOP
        // ----------------------------------------------------

        if (vm.ImageFile != null &&
            vm.ImageFile.Length > 0)
        {
            var upload = await _api.UploadImageAsync(
                "uploads/event-image",
                vm.ImageFile
            );

            if (!upload.ok)
            {
                ModelState.AddModelError(
                    nameof(vm.ImageFile),
                    upload.message
                );

                return View(
                    "EventForm",
                    vm
                );
            }

            vm.ImageUrl = upload.imageUrl;
        }

        // ----------------------------------------------------
        // CREATE EVENT
        // ----------------------------------------------------

        var result = await _api.SendAsync<ApiMessage>(
            HttpMethod.Post,
            "events",
            vm
        );

        if (result.ok)
        {
            TempData["Success"] = result.message;

            return RedirectToAction(
                nameof(Dashboard)
            );
        }

        TempData["Error"] = result.message;

        return View(
            "EventForm",
            vm
        );
    }

    // ========================================================
    // EDIT EVENT - GET
    // ========================================================

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var events = await _api.GetAsync<List<EventVm>>(
            "events/mine"
        ) ?? new List<EventVm>();

        var selectedEvent = events.FirstOrDefault(
            x => x.Id == id
        );

        if (selectedEvent == null)
        {
            return NotFound();
        }

        return View(
            "EventForm",
            selectedEvent
        );
    }

    // ========================================================
    // EDIT EVENT - POST
    // ========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EventVm vm)
    {
        NormalizeTickets(vm);

        if (!ValidDates(vm))
        {
            return View(
                "EventForm",
                vm
            );
        }

        if (!ModelState.IsValid)
        {
            return View(
                "EventForm",
                vm
            );
        }

        // ----------------------------------------------------
        // OPTIONAL NEW IMAGE UPLOAD
        // If organizer selects a new image,
        // replace the existing image.
        // ----------------------------------------------------

        if (vm.ImageFile != null &&
            vm.ImageFile.Length > 0)
        {
            var upload = await _api.UploadImageAsync(
                "uploads/event-image",
                vm.ImageFile
            );

            if (!upload.ok)
            {
                ModelState.AddModelError(
                    nameof(vm.ImageFile),
                    upload.message
                );

                return View(
                    "EventForm",
                    vm
                );
            }

            vm.ImageUrl = upload.imageUrl;
        }

        // ----------------------------------------------------
        // UPDATE EVENT
        // ----------------------------------------------------

        var result = await _api.SendAsync<ApiMessage>(
            HttpMethod.Put,
            $"events/{vm.Id}",
            vm
        );

        if (result.ok)
        {
            TempData["Success"] = result.message;

            return RedirectToAction(
                nameof(Dashboard)
            );
        }

        TempData["Error"] = result.message;

        return View(
            "EventForm",
            vm
        );
    }

    // ========================================================
    // PUBLISH / UNPUBLISH EVENT
    // ========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(
        int id,
        bool value)
    {
        var result = await _api.SendAsync<ApiMessage>(
            HttpMethod.Patch,
            $"events/{id}/publish?value={value.ToString().ToLowerInvariant()}"
        );

        if (result.ok)
            TempData["Success"] = result.message;
        else
            TempData["Error"] = result.message;

        return RedirectToAction(
            nameof(Dashboard)
        );
    }

    // ========================================================
    // DELETE EVENT
    // ========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _api.SendAsync<ApiMessage>(
            HttpMethod.Delete,
            $"events/{id}"
        );

        if (result.ok)
            TempData["Success"] = result.message;
        else
            TempData["Error"] = result.message;

        return RedirectToAction(
            nameof(Dashboard)
        );
    }

    // ========================================================
    // RESERVATION APPROVAL
    // ========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approval(
        int id,
        string status)
    {
        var result = await _api.SendAsync<ApiMessage>(
            HttpMethod.Patch,
            $"reservations/{id}/approval?status={status}"
        );

        if (result.ok)
            TempData["Success"] = result.message;
        else
            TempData["Error"] = result.message;

        return RedirectToAction(
            nameof(Dashboard)
        );
    }

    // ========================================================
    // NORMALIZE TICKET CATEGORIES
    // ========================================================

    private void NormalizeTickets(EventVm vm)
    {
        vm.TicketCategories =
            vm.TicketCategories
                .Where(
                    x =>
                        !string.IsNullOrWhiteSpace(x.Name) &&
                        x.SeatLimit > 0
                )
                .ToList();
    }

    // ========================================================
    // EVENT DATE VALIDATION
    // ========================================================

    private bool ValidDates(EventVm vm)
    {
        if (
            vm.EventDate.Date <
            DateTime.Today.AddDays(10)
        )
        {
            ModelState.AddModelError(
                nameof(vm.EventDate),
                "Event date must be at least 10 days from today."
            );
        }

        if (
            vm.RegistrationCloseDate.Date <
            DateTime.Today
        )
        {
            ModelState.AddModelError(
                nameof(vm.RegistrationCloseDate),
                "Registration close date cannot be in the past."
            );
        }

        if (
            vm.RegistrationCloseDate >=
            vm.EventDate
        )
        {
            ModelState.AddModelError(
                nameof(vm.RegistrationCloseDate),
                "Registration must close before the event starts."
            );
        }

        return ModelState.IsValid;
    }
}