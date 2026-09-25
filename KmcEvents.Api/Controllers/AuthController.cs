using KmcEvents.Api.Data;
using KmcEvents.Api.DTOs;
using KmcEvents.Api.Models;
using KmcEvents.Api.Services;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace KmcEvents.Api.Controllers;

// ============================================================
// AUTH CONTROLLER
// Registration, Login, Email Notifications and Password Reset
// ============================================================

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtTokenService _jwt;
    private readonly IPasswordHasher<AppUser> _hasher;
    private readonly IEmailService _email;

    public AuthController(
        AppDbContext db,
        JwtTokenService jwt,
        IPasswordHasher<AppUser> hasher,
        IEmailService email)
    {
        _db = db;
        _jwt = jwt;
        _hasher = hasher;
        _email = email;
    }

    // ========================================================
    // PUBLIC USER REGISTRATION
    // ========================================================

    [HttpPost("register-public")]
    public async Task<IActionResult> RegisterPublic(PublicRegisterDto dto)
    {
        return await Register(
            dto.FullName,
            null,
            dto.Nic,
            dto.Email,
            dto.Password,
            Roles.Public
        );
    }

    // ========================================================
    // EVENT ORGANIZER REGISTRATION
    // ========================================================

    [HttpPost("register-organizer")]
    public async Task<IActionResult> RegisterOrganizer(OrganizerRegisterDto dto)
    {
        return await Register(
            dto.FullName,
            dto.CompanyName,
            dto.Nic,
            dto.Email,
            dto.Password,
            Roles.Organizer
        );
    }

    // ========================================================
    // COMMON REGISTRATION METHOD
    // ========================================================

    private async Task<IActionResult> Register(
        string name,
        string? company,
        string nic,
        string email,
        string password,
        string role)
    {
        email = email.Trim().ToLowerInvariant();
        nic = nic.Trim().ToUpperInvariant();

        // Duplicate email validation.
        var emailExists = await _db.Users.AnyAsync(x => x.Email == email);

        if (emailExists)
            return Conflict(new { message = "Email already registered." });

        // Duplicate NIC validation.
        var nicExists = await _db.Users.AnyAsync(x => x.Nic == nic);

        if (nicExists)
            return Conflict(new { message = "NIC already registered." });

        // Create account.
        var user = new AppUser
        {
            FullName = name.Trim(),
            CompanyName = company?.Trim(),
            Nic = nic,
            Email = email,
            Role = role,
            IsActive = true
        };

        user.PasswordHash = _hasher.HashPassword(user, password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Organizer company details are displayed only when available.
        var companyHtml = string.IsNullOrWhiteSpace(user.CompanyName)
            ? ""
            : $@"
                <p style='margin:7px 0;font-size:14px;color:#333333;'>
                    <strong>Organization:</strong> {WebUtility.HtmlEncode(user.CompanyName)}
                </p>";

        // Welcome email.
        var welcomeBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
</head>

<body style='margin:0;padding:0;background:#f6f3f1;font-family:Arial,Helvetica,sans-serif;'>

<table width='100%' cellpadding='0' cellspacing='0'
       style='background:#f6f3f1;padding:35px 15px;'>
<tr>
<td align='center'>

<table width='600' cellpadding='0' cellspacing='0'
       style='width:100%;max-width:600px;background:#ffffff;border-radius:16px;
              overflow:hidden;box-shadow:0 10px 30px rgba(0,0,0,0.12);'>

    <!-- HEADER -->
    <tr>
        <td style='background:#8d0034;padding:35px 30px;text-align:center;color:#ffffff;'>
            <div style='font-size:12px;font-weight:bold;letter-spacing:2px;color:#f4b41a;'>
                KANDY MUNICIPAL COUNCIL
            </div>

            <h1 style='margin:12px 0 8px;font-size:31px;color:#ffffff;'>
                Welcome to KMC Events
            </h1>

            <p style='margin:0;font-size:14px;color:#f3dce4;'>
                Official Event Management Platform
            </p>
        </td>
    </tr>

    <!-- SRI LANKAN THEME STRIP -->
    <tr>
        <td>
            <table width='100%' cellpadding='0' cellspacing='0'>
                <tr>
                    <td style='height:5px;background:#005b50;'></td>
                    <td style='height:5px;background:#f28c28;'></td>
                    <td style='height:5px;background:#8d0034;'></td>
                    <td style='height:5px;background:#f4b41a;'></td>
                </tr>
            </table>
        </td>
    </tr>

    <!-- CONTENT -->
    <tr>
        <td style='padding:38px 35px;color:#30282a;'>

            <h2 style='margin:0 0 18px;color:#8d0034;font-size:24px;'>
                Hello {WebUtility.HtmlEncode(user.FullName)},
            </h2>

            <p style='font-size:15px;line-height:1.8;color:#555555;'>
                Welcome to the Kandy Municipal Council Event Management Platform.
                Your account has been successfully created.
            </p>

            <table width='100%' cellpadding='0' cellspacing='0'
                   style='background:#fff8ed;border-left:5px solid #f4b41a;
                          border-radius:9px;margin:25px 0;'>
                <tr>
                    <td style='padding:22px;'>

                        <div style='margin-bottom:12px;color:#8d0034;
                                    font-weight:bold;font-size:14px;'>
                            ACCOUNT DETAILS
                        </div>

                        <p style='margin:7px 0;font-size:14px;color:#333333;'>
                            <strong>Name:</strong> {WebUtility.HtmlEncode(user.FullName)}
                        </p>

                        <p style='margin:7px 0;font-size:14px;color:#333333;'>
                            <strong>Email:</strong> {WebUtility.HtmlEncode(user.Email)}
                        </p>

                        <p style='margin:7px 0;font-size:14px;color:#333333;'>
                            <strong>Account Type:</strong> {WebUtility.HtmlEncode(user.Role)}
                        </p>

                        {companyHtml}

                    </td>
                </tr>
            </table>

            <p style='font-size:15px;line-height:1.8;color:#555555;'>
                You can now login using your registered email address and password.
                Please keep your login credentials secure.
            </p>

            <div style='text-align:center;margin:30px 0 10px;'>
                <span style='display:inline-block;padding:14px 28px;background:#005b50;
                             color:#ffffff;border-radius:8px;font-size:14px;font-weight:bold;'>
                    Account Successfully Activated
                </span>
            </div>

        </td>
    </tr>

    <!-- FOOTER -->
    <tr>
        <td style='background:#2c1015;padding:27px;text-align:center;color:#ffffff;'>
            <strong>Kandy Municipal Council</strong>

            <div style='margin-top:7px;font-size:12px;color:#d9c8cc;'>
                KMC Event Management Platform
            </div>

            <div style='margin-top:13px;font-size:11px;color:#f4b41a;'>
                Developed by Mushaaraf Ali
            </div>
        </td>
    </tr>

</table>

</td>
</tr>
</table>

</body>
</html>";

        await _email.SendAsync(
            user.Email,
            "Welcome to KMC Event Management",
            welcomeBody
        );

        return Ok(new
        {
            message = "Registration successful. Welcome email sent. You can now login."
        });
    }

    // ========================================================
    // LOGIN
    // ========================================================

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Email == email);

        if (user == null)
            return Unauthorized(new { message = "Invalid email or password." });

        if (!user.IsActive)
        {
            return Unauthorized(new
            {
                message = "Your account has been disabled. Please contact KMC Administrator."
            });
        }

        var passwordResult = _hasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            dto.Password
        );

        // Wrong password security notification.
        if (passwordResult == PasswordVerificationResult.Failed)
        {
            var securityBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
</head>

<body style='margin:0;padding:0;background:#f6f3f1;font-family:Arial,Helvetica,sans-serif;'>

<table width='100%' cellpadding='0' cellspacing='0'
       style='background:#f6f3f1;padding:35px 15px;'>
<tr>
<td align='center'>

<table width='600' cellpadding='0' cellspacing='0'
       style='width:100%;max-width:600px;background:#ffffff;border-radius:16px;
              overflow:hidden;box-shadow:0 10px 30px rgba(0,0,0,0.12);'>

    <tr>
        <td style='background:#8d0034;padding:35px;text-align:center;color:#ffffff;'>
            <div style='font-size:12px;font-weight:bold;letter-spacing:2px;color:#f4b41a;'>
                KMC SECURITY NOTICE
            </div>

            <h1 style='margin:12px 0 8px;font-size:30px;'>
                Failed Login Attempt
            </h1>

            <p style='margin:0;color:#f3dce4;'>
                Account Security Notification
            </p>
        </td>
    </tr>

    <tr>
        <td style='padding:38px 35px;'>

            <h2 style='color:#8d0034;'>
                Hello {WebUtility.HtmlEncode(user.FullName)},
            </h2>

            <p style='font-size:15px;line-height:1.8;color:#555555;'>
                A failed login attempt was detected on your KMC Event Management account.
            </p>

            <div style='background:#fff3f4;border-left:5px solid #b00020;
                        border-radius:9px;padding:22px;margin:25px 0;'>

                <strong style='color:#b00020;'>LOGIN ATTEMPT DETAILS</strong>

                <p><strong>Email:</strong> {WebUtility.HtmlEncode(user.Email)}</p>
                <p><strong>Date:</strong> {DateTime.Now:dd MMMM yyyy}</p>
                <p><strong>Time:</strong> {DateTime.Now:hh:mm tt}</p>

            </div>

            <p style='font-size:15px;line-height:1.8;color:#555555;'>
                If you entered the wrong password, no action is required.
                If this was not you, please reset your password immediately.
            </p>

        </td>
    </tr>

    <tr>
        <td style='background:#2c1015;padding:25px;text-align:center;color:#ffffff;'>
            <strong>Kandy Municipal Council</strong>

            <div style='margin-top:10px;font-size:11px;color:#f4b41a;'>
                Developed by Mushaaraf Ali
            </div>
        </td>
    </tr>

</table>

</td>
</tr>
</table>

</body>
</html>";

            await _email.SendAsync(
                user.Email,
                "KMC Security Alert - Failed Login Attempt",
                securityBody
            );

            return Unauthorized(new { message = "Invalid email or password." });
        }

        var token = _jwt.Create(user);

        return Ok(new
        {
            token,
            userId = user.Id,
            fullName = user.FullName,
            email = user.Email,
            role = user.Role,
            companyName = user.CompanyName
        });
    }

    // ========================================================
    // FORGOT PASSWORD
    // Generates a 6-digit code valid for 10 minutes.
    // ========================================================

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Email == email);

        // Same response prevents revealing whether an account exists.
        if (user == null)
        {
            return Ok(new
            {
                message = "If the email is registered, a password reset code has been sent."
            });
        }

        if (!user.IsActive)
        {
            return BadRequest(new
            {
                message = "This account is currently disabled. Please contact KMC Administrator."
            });
        }

        // Generate secure 6-digit reset code.
        var resetCode = System.Security.Cryptography.RandomNumberGenerator
            .GetInt32(100000, 1000000)
            .ToString();

        user.PasswordResetCode = resetCode;
        user.PasswordResetCodeExpiry = DateTime.UtcNow.AddMinutes(10);

        await _db.SaveChangesAsync();

        var forgotPasswordBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
</head>

<body style='margin:0;padding:0;background:#f6f3f1;font-family:Arial,Helvetica,sans-serif;'>

<table width='100%' cellpadding='0' cellspacing='0'
       style='background:#f6f3f1;padding:35px 15px;'>
<tr>
<td align='center'>

<table width='600' cellpadding='0' cellspacing='0'
       style='width:100%;max-width:600px;background:#ffffff;border-radius:16px;
              overflow:hidden;box-shadow:0 10px 30px rgba(0,0,0,0.12);'>

    <!-- HEADER -->
    <tr>
        <td style='background:#8d0034;padding:35px;text-align:center;color:#ffffff;'>
            <div style='font-size:12px;font-weight:bold;letter-spacing:2px;color:#f4b41a;'>
                KANDY MUNICIPAL COUNCIL
            </div>

            <h1 style='margin:12px 0 8px;font-size:30px;'>
                Password Reset
            </h1>

            <p style='margin:0;color:#f3dce4;'>
                KMC Event Management Platform
            </p>
        </td>
    </tr>

    <!-- FLAG COLOR STRIP -->
    <tr>
        <td>
            <table width='100%' cellpadding='0' cellspacing='0'>
                <tr>
                    <td style='height:5px;background:#005b50;'></td>
                    <td style='height:5px;background:#f28c28;'></td>
                    <td style='height:5px;background:#8d0034;'></td>
                    <td style='height:5px;background:#f4b41a;'></td>
                </tr>
            </table>
        </td>
    </tr>

    <!-- CONTENT -->
    <tr>
        <td style='padding:38px 35px;text-align:center;'>

            <h2 style='color:#8d0034;margin-top:0;'>
                Hello {WebUtility.HtmlEncode(user.FullName)},
            </h2>

            <p style='font-size:15px;line-height:1.8;color:#555555;'>
                We received a request to reset the password for your
                KMC Event Management account.
            </p>

            <p style='font-size:14px;color:#555555;'>
                Enter the following verification code on the password reset page:
            </p>

            <!-- RESET CODE -->
            <div style='margin:28px auto;padding:20px;background:#fff8ed;
                        border:2px dashed #f4b41a;border-radius:12px;
                        max-width:300px;'>

                <div style='font-size:12px;font-weight:bold;color:#8d0034;
                            letter-spacing:1px;margin-bottom:10px;'>
                    PASSWORD RESET CODE
                </div>

                <div style='font-size:36px;font-weight:bold;letter-spacing:9px;color:#2c1015;'>
                    {resetCode}
                </div>

            </div>

            <p style='font-size:14px;color:#555555;'>
                This code is valid for <strong>10 minutes</strong>.
            </p>

            <div style='background:#fff3f4;border-left:5px solid #b00020;
                        border-radius:8px;padding:16px;text-align:left;margin-top:25px;'>

                <strong style='color:#b00020;'>Security Notice</strong>

                <p style='font-size:13px;line-height:1.6;color:#555555;margin-bottom:0;'>
                    If you did not request a password reset, do not share this code
                    with anyone. You can safely ignore this email.
                </p>

            </div>

        </td>
    </tr>

    <!-- FOOTER -->
    <tr>
        <td style='background:#2c1015;padding:25px;text-align:center;color:#ffffff;'>
            <strong>Kandy Municipal Council</strong>

            <div style='margin-top:7px;font-size:12px;color:#d9c8cc;'>
                KMC Event Management Platform
            </div>

            <div style='margin-top:10px;font-size:11px;color:#f4b41a;'>
                Developed by Mushaaraf Ali
            </div>
        </td>
    </tr>

</table>

</td>
</tr>
</table>

</body>
</html>";

        await _email.SendAsync(
            user.Email,
            "KMC Password Reset Code",
            forgotPasswordBody
        );

        return Ok(new
        {
            message = "If the email is registered, a password reset code has been sent."
        });
    }

    // ========================================================
    // RESET PASSWORD
    // Verifies code and changes the user's password.
    // ========================================================

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var code = dto.Code.Trim();

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Email == email);

        if (user == null)
            return BadRequest(new { message = "Invalid or expired password reset request." });

        if (string.IsNullOrWhiteSpace(user.PasswordResetCode) ||
            user.PasswordResetCode != code)
        {
            return BadRequest(new
            {
                message = "Invalid password reset code."
            });
        }

        if (user.PasswordResetCodeExpiry == null ||
            user.PasswordResetCodeExpiry <= DateTime.UtcNow)
        {
            user.PasswordResetCode = null;
            user.PasswordResetCodeExpiry = null;

            await _db.SaveChangesAsync();

            return BadRequest(new
            {
                message = "Password reset code has expired. Please request a new code."
            });
        }

        if (dto.NewPassword != dto.ConfirmPassword)
        {
            return BadRequest(new
            {
                message = "New password and confirm password do not match."
            });
        }

        // Set new hashed password.
        user.PasswordHash = _hasher.HashPassword(user, dto.NewPassword);

        // Reset code can never be reused.
        user.PasswordResetCode = null;
        user.PasswordResetCodeExpiry = null;

        await _db.SaveChangesAsync();

        // Password changed confirmation email.
        var successBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
</head>

<body style='margin:0;padding:0;background:#f6f3f1;font-family:Arial,Helvetica,sans-serif;'>

<table width='100%' cellpadding='0' cellspacing='0'
       style='background:#f6f3f1;padding:35px 15px;'>
<tr>
<td align='center'>

<table width='600' cellpadding='0' cellspacing='0'
       style='width:100%;max-width:600px;background:#ffffff;border-radius:16px;
              overflow:hidden;box-shadow:0 10px 30px rgba(0,0,0,0.12);'>

    <tr>
        <td style='background:#005b50;padding:35px;text-align:center;color:#ffffff;'>
            <div style='font-size:12px;font-weight:bold;letter-spacing:2px;color:#f4b41a;'>
                KANDY MUNICIPAL COUNCIL
            </div>

            <h1 style='margin:12px 0 8px;font-size:29px;'>
                Password Changed Successfully
            </h1>
        </td>
    </tr>

    <tr>
        <td style='padding:38px 35px;'>

            <h2 style='color:#8d0034;'>
                Hello {WebUtility.HtmlEncode(user.FullName)},
            </h2>

            <p style='font-size:15px;line-height:1.8;color:#555555;'>
                Your KMC Event Management account password has been
                successfully changed.
            </p>

            <div style='background:#eef9f5;border-left:5px solid #005b50;
                        padding:18px;border-radius:8px;margin:25px 0;'>

                <strong style='color:#005b50;'>
                    Password Reset Successful
                </strong>

                <p style='margin-bottom:0;font-size:14px;color:#555555;'>
                    You can now login using your new password.
                </p>

            </div>

            <p style='font-size:14px;line-height:1.7;color:#555555;'>
                If you did not make this change, please contact
                KMC Administrator immediately.
            </p>

        </td>
    </tr>

    <tr>
        <td style='background:#2c1015;padding:25px;text-align:center;color:#ffffff;'>
            <strong>Kandy Municipal Council</strong>

            <div style='margin-top:10px;font-size:11px;color:#f4b41a;'>
                Developed by Mushaaraf Ali
            </div>
        </td>
    </tr>

</table>

</td>
</tr>
</table>

</body>
</html>";

        await _email.SendAsync(
            user.Email,
            "KMC Password Changed Successfully",
            successBody
        );

        return Ok(new
        {
            message = "Password reset successful. You can now login using your new password."
        });
    }
}