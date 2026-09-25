using KmcEvents.Client.Models;

using KmcEvents.Client.Services;

using Microsoft.AspNetCore.Mvc;

namespace KmcEvents.Client.Controllers;

public class EventsController : Controller
{
    private readonly ApiClient _api;

    public EventsController(
        ApiClient api
    )
    {
        _api = api;
    }

    public async Task<IActionResult> Index(
        string? q,
        string? category,
        string? location,
        DateTime? date
    )
    {
        var queryParameters =
            new List<string>();

        if (!string.IsNullOrWhiteSpace(q))
        {
            queryParameters.Add(
                "q=" +
                Uri.EscapeDataString(q)
            );
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            queryParameters.Add(
                "category=" +
                Uri.EscapeDataString(category)
            );
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            queryParameters.Add(
                "location=" +
                Uri.EscapeDataString(location)
            );
        }

        if (date.HasValue)
        {
            queryParameters.Add(
                "date=" +
                date.Value.ToString("yyyy-MM-dd")
            );
        }

        ViewBag.Q =
            q;

        ViewBag.Category =
            category;

        ViewBag.Location =
            location;

        ViewBag.Date =
            date?.ToString("yyyy-MM-dd");

        var endpoint =
            "events";

        if (queryParameters.Count > 0)
        {
            endpoint +=
                "?" +
                string.Join(
                    "&",
                    queryParameters
                );
        }

        var events =
            await _api.GetAsync<List<EventVm>>(
                endpoint
            );

        return View(
            events ??
            new List<EventVm>()
        );
    }

    public async Task<IActionResult> Details(
        int id
    )
    {
        var eventItem =
            await _api.GetAsync<EventVm>(
                $"events/{id}"
            );

        if (eventItem == null)
        {
            return NotFound();
        }

        return View(
            eventItem
        );
    }
}