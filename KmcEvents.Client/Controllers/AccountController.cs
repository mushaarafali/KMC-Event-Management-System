using KmcEvents.Client.Models;
using KmcEvents.Client.Services;

using Microsoft.AspNetCore.Mvc;

using System.Text.Json;

namespace KmcEvents.Client.Controllers;

// ============================================================
// ACCOUNT CONTROLLER
// Handles login, registration, logout and password recovery.
// ============================================================

public class AccountController : Controller
{
    private readonly ApiClient _api;

    public AccountController(ApiClient api)
    {
        _api = api;
    }

    // ========================================================
    // LOGIN
    // ========================================================

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;

        return View(new LoginVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        LoginVm vm,
        string? returnUrl = null)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var result = await _api.SendAsync<JsonElement>(
            HttpMethod.Post,
            "auth/login",
            vm
        );

        if (!result.ok)
        {
            ModelState.AddModelError("", result.message);
            return View(vm);
        }

        var data = result.data;

        HttpContext.Session.SetString(
            "Token",
            data.GetProperty("token").GetString()!
        );

        HttpContext.Session.SetString(
            "Role",
            data.GetProperty("role").GetString()!
        );

        HttpContext.Session.SetString(
            "Name",
            data.GetProperty("fullName").GetString()!
        );

        HttpContext.Session.SetString(
            "Email",
            data.GetProperty("email").GetString()!
        );

        if (!string.IsNullOrWhiteSpace(returnUrl) &&
            Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        var role = HttpContext.Session.GetString("Role");

        if (role == "KMC Admin")
            return RedirectToAction("Dashboard", "Admin");

        if (role == "Event Organizer")
            return RedirectToAction("Dashboard", "Organizer");

        return RedirectToAction("Index", "Home");
    }

    // ========================================================
    // PUBLIC USER REGISTRATION
    // ========================================================

    [HttpGet]
    public IActionResult RegisterPublic()
    {
        return View(new PublicRegisterVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterPublic(PublicRegisterVm vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var result = await _api.SendAsync<ApiMessage>(
            HttpMethod.Post,
            "auth/register-public",
            vm
        );

        if (result.ok)
        {
            TempData["Success"] = result.message;

            return RedirectToAction(nameof(Login));
        }

        ModelState.AddModelError("", result.message);

        return View(vm);
    }

    // ========================================================
    // EVENT ORGANIZER REGISTRATION
    // ========================================================

    [HttpGet]
    public IActionResult RegisterOrganizer()
    {
        return View(new OrganizerRegisterVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterOrganizer(
        OrganizerRegisterVm vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var result = await _api.SendAsync<ApiMessage>(
            HttpMethod.Post,
            "auth/register-organizer",
            vm
        );

        if (result.ok)
        {
            TempData["Success"] = result.message;

            return RedirectToAction(nameof(Login));
        }

        ModelState.AddModelError("", result.message);

        return View(vm);
    }

    // ========================================================
    // FORGOT PASSWORD
    // User enters registered email.
    // API sends 6-digit reset code by email.
    // ========================================================

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordVm vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var result = await _api.SendAsync<ApiMessage>(
            HttpMethod.Post,
            "auth/forgot-password",
            vm
        );

        if (!result.ok)
        {
            ModelState.AddModelError("", result.message);

            return View(vm);
        }

        TempData["Success"] = result.message;

        return RedirectToAction(
            nameof(ResetPassword),
            new
            {
                email = vm.Email
            }
        );
    }

    // ========================================================
    // RESET PASSWORD
    // User enters email code and new password.
    // ========================================================

    [HttpGet]
    public IActionResult ResetPassword(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return RedirectToAction(nameof(ForgotPassword));

        var vm = new ResetPasswordVm
        {
            Email = email
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordVm vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var result = await _api.SendAsync<ApiMessage>(
            HttpMethod.Post,
            "auth/reset-password",
            vm
        );

        if (!result.ok)
        {
            ModelState.AddModelError("", result.message);

            return View(vm);
        }

        TempData["Success"] = result.message;

        return RedirectToAction(nameof(Login));
    }

    // ========================================================
    // LOGOUT
    // Clears session and prevents cached authenticated pages.
    // ========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();

        Response.Headers.CacheControl =
            "no-store, no-cache, must-revalidate, max-age=0";

        Response.Headers.Pragma = "no-cache";

        Response.Headers.Expires = "0";

        return RedirectToAction("Index", "Home");
    }

    // ========================================================
    // ACCESS DENIED
    // ========================================================

    public IActionResult Denied()
    {
        return View();
    }
}