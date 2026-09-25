using KmcEvents.Client.Filters;
using KmcEvents.Client.Models;
using KmcEvents.Client.Services;
using Microsoft.AspNetCore.Mvc;

namespace KmcEvents.Client.Controllers;

[SessionAuthorize("Public")]
public class ReservationsController : Controller
{
    private readonly ApiClient _api;

    public ReservationsController(ApiClient api)
    {
        _api = api;
    }

    [HttpGet]
    public async Task<IActionResult> Create(int eventId)
    {
        var eventVm = await _api.GetAsync<EventVm>(
            $"events/{eventId}"
        );

        if (eventVm == null)
        {
            return NotFound();
        }

        ViewBag.Event = eventVm;

        return View(
            new ReservationVm
            {
                EventId = eventId,
                Quantity = 1,
                PaymentMethod = "Pay Later"
            }
        );
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ReservationVm vm)
    {
        var eventVm = await _api.GetAsync<EventVm>(
            $"events/{vm.EventId}"
        );

        if (eventVm == null)
        {
            return NotFound();
        }

        ViewBag.Event = eventVm;

        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        var ticket = eventVm.TicketCategories?
            .FirstOrDefault(
                x => x.Id == vm.TicketCategoryId
            );

        if (ticket == null)
        {
            ModelState.AddModelError(
                nameof(vm.TicketCategoryId),
                "Please select a valid ticket category."
            );

            return View(vm);
        }

        if (vm.Quantity < 1 || vm.Quantity > 10)
        {
            ModelState.AddModelError(
                nameof(vm.Quantity),
                "Quantity must be between 1 and 10."
            );

            return View(vm);
        }

        if (vm.PaymentMethod == "Card")
        {
            return RedirectToAction(
                nameof(Pay),
                new
                {
                    eventId = vm.EventId,
                    ticketCategoryId = vm.TicketCategoryId,
                    quantity = vm.Quantity
                }
            );
        }

        if (vm.PaymentMethod != "Pay Later")
        {
            ModelState.AddModelError(
                nameof(vm.PaymentMethod),
                "Please select a valid payment method."
            );

            return View(vm);
        }

        var result = await _api.SendAsync<ReservationVm>(
            HttpMethod.Post,
            "reservations",
            new
            {
                vm.EventId,
                vm.TicketCategoryId,
                vm.Quantity,
                PaymentMethod = "Pay Later"
            }
        );

        if (!result.ok)
        {
            ModelState.AddModelError(
                "",
                result.message
            );

            return View(vm);
        }

        TempData["Success"] = result.message;

        return RedirectToAction(
            nameof(Mine)
        );
    }

    [HttpGet]
    public async Task<IActionResult> Pay(
        int eventId,
        int ticketCategoryId,
        int quantity)
    {
        var eventVm = await _api.GetAsync<EventVm>(
            $"events/{eventId}"
        );

        if (eventVm == null)
        {
            return NotFound();
        }

        var ticket = eventVm.TicketCategories?
            .FirstOrDefault(
                x => x.Id == ticketCategoryId
            );

        if (ticket == null)
        {
            TempData["Error"] =
                "The selected ticket category was not found.";

            return RedirectToAction(
                "Details",
                "Events",
                new
                {
                    id = eventId
                }
            );
        }

        if (quantity < 1 || quantity > 10)
        {
            TempData["Error"] =
                "Invalid ticket quantity.";

            return RedirectToAction(
                nameof(Create),
                new
                {
                    eventId
                }
            );
        }

        var amount =
            ticket.Price * quantity;

        var vm = new CardVm
        {
            EventId = eventId,
            TicketCategoryId = ticketCategoryId,
            Quantity = quantity,
            Amount = amount
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(CardVm vm)
    {
        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        var result = await _api.SendAsync<ReservationVm>(
            HttpMethod.Post,
            "reservations/card-checkout",
            new
            {
                vm.EventId,
                vm.TicketCategoryId,
                vm.Quantity,
                vm.CardHolder,
                vm.CardNumber,
                vm.Expiry,
                vm.Cvv
            }
        );

        if (!result.ok)
        {
            ModelState.AddModelError(
                "",
                result.message
            );

            return View(vm);
        }

        TempData["Success"] =
            result.message;

        return RedirectToAction(
            nameof(Mine)
        );
    }

    [HttpGet]
    public async Task<IActionResult> Mine()
    {
        var reservations =
            await _api.GetAsync<List<ReservationVm>>(
                "reservations/mine"
            )
            ?? [];

        return View(reservations);
    }
}