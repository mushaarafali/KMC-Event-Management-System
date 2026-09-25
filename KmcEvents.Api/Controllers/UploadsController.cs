using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KmcEvents.Api.Controllers;

[ApiController]
[Route("api/uploads")]
public class UploadsController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;

    public UploadsController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    // ========================================================
    // EVENT IMAGE UPLOAD
    // ========================================================

    [HttpPost("event-image")]
    [Authorize(Roles = "Event Organizer,KMC Admin")]
    public async Task<IActionResult> UploadEventImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new
            {
                message = "Please select an image."
            });
        }

        var allowedExtensions = new[]
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

        var extension =
            Path.GetExtension(file.FileName)
                .ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(new
            {
                message = "Only JPG, JPEG, PNG and WEBP images are allowed."
            });
        }

        const long maxFileSize =
            5 * 1024 * 1024;

        if (file.Length > maxFileSize)
        {
            return BadRequest(new
            {
                message = "Image size cannot exceed 5 MB."
            });
        }

        var webRoot =
            _environment.WebRootPath;

        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot =
                Path.Combine(
                    _environment.ContentRootPath,
                    "wwwroot"
                );
        }

        var uploadFolder =
            Path.Combine(
                webRoot,
                "uploads",
                "events"
            );

        Directory.CreateDirectory(
            uploadFolder
        );

        var fileName =
            $"{Guid.NewGuid():N}{extension}";

        var filePath =
            Path.Combine(
                uploadFolder,
                fileName
            );

        await using var stream =
            new FileStream(
                filePath,
                FileMode.Create
            );

        await file.CopyToAsync(
            stream
        );

        var imageUrl =
            $"{Request.Scheme}://{Request.Host}/uploads/events/{fileName}";

        return Ok(new
        {
            imageUrl,
            message = "Event image uploaded successfully."
        });
    }
}