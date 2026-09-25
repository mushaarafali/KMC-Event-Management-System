using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
namespace KmcEvents.Client.Services;

// ============================================================
// API CLIENT
// Handles authenticated communication between
// KmcEvents.Client and KmcEvents.Api.
// ============================================================

public class ApiClient
{
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _context;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // ========================================================
    // CONSTRUCTOR
    // ========================================================

    public ApiClient(
        HttpClient http,
        IHttpContextAccessor context)
    {
        _http = http;
        _context = context;
    }

    // ========================================================
    // ADD JWT TOKEN
    // Adds the logged-in user's JWT token to API requests.
    // ========================================================

    private void AddAuthorizationHeader()
    {
        var token = _context
            .HttpContext?
            .Session
            .GetString("Token");

        if (string.IsNullOrWhiteSpace(token))
        {
            _http.DefaultRequestHeaders.Authorization = null;
            return;
        }

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token
            );
    }

    // ========================================================
    // GET JSON DATA
    // ========================================================

    public async Task<T?> GetAsync<T>(string url)
    {
        AddAuthorizationHeader();

        var response = await _http.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            return default;

        return await response.Content.ReadFromJsonAsync<T>(
            _jsonOptions
        );
    }

    // ========================================================
    // SEND POST / PUT / PATCH / DELETE REQUEST
    // ========================================================

    public async Task<(bool ok, T? data, string message)> SendAsync<T>(
        HttpMethod method,
        string url,
        object? body = null)
    {
        AddAuthorizationHeader();

        using var request = new HttpRequestMessage(
            method,
            url
        );

        if (body != null)
        {
            request.Content = JsonContent.Create(body);
        }

        var response = await _http.SendAsync(request);

        var responseText =
            await response.Content.ReadAsStringAsync();

        T? data = default;

        string message = "";

        // ----------------------------------------------------
        // Deserialize response data.
        // ----------------------------------------------------

        if (!string.IsNullOrWhiteSpace(responseText))
        {
            try
            {
                data = JsonSerializer.Deserialize<T>(
                    responseText,
                    _jsonOptions
                );
            }
            catch
            {
                data = default;
            }
        }

        // ----------------------------------------------------
        // Read API message property.
        // ----------------------------------------------------

        if (!string.IsNullOrWhiteSpace(responseText))
        {
            try
            {
                using var document =
                    JsonDocument.Parse(responseText);

                if (
                    document.RootElement.TryGetProperty(
                        "message",
                        out var messageElement
                    )
                )
                {
                    message =
                        messageElement.GetString()
                        ?? "";
                }
            }
            catch
            {
                message = "";
            }
        }

        // ----------------------------------------------------
        // Fallback response message.
        // ----------------------------------------------------

        if (string.IsNullOrWhiteSpace(message))
        {
            message = response.IsSuccessStatusCode
                ? "Operation completed."
                : $"Request failed ({(int)response.StatusCode}).";
        }

        return (
            response.IsSuccessStatusCode,
            data,
            message
        );
    }

    // ========================================================
    // DOWNLOAD BINARY FILE
    // Used for PDF report downloads.
    // ========================================================

    public async Task<byte[]?> GetBytesAsync(string url)
    {
        AddAuthorizationHeader();

        var response = await _http.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task<(bool ok, string? imageUrl, string message)> UploadImageAsync(
    string url,
    IFormFile file)
    {
        AddAuthorizationHeader();

        using var content =
            new MultipartFormDataContent();

        await using var stream =
            file.OpenReadStream();

        using var fileContent =
            new StreamContent(stream);

        fileContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(
                file.ContentType
            );

        content.Add(
            fileContent,
            "file",
            file.FileName
        );

        var response =
            await _http.PostAsync(
                url,
                content
            );

        var responseText =
            await response.Content.ReadAsStringAsync();

        string? imageUrl = null;
        string message = "";

        try
        {
            using var json =
                JsonDocument.Parse(
                    responseText
                );

            if (
                json.RootElement.TryGetProperty(
                    "imageUrl",
                    out var imageElement
                )
            )
            {
                imageUrl =
                    imageElement.GetString();
            }

            if (
                json.RootElement.TryGetProperty(
                    "message",
                    out var messageElement
                )
            )
            {
                message =
                    messageElement.GetString()
                    ?? "";
            }
        }
        catch
        {
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            message =
                response.IsSuccessStatusCode
                    ? "Image uploaded successfully."
                    : "Image upload failed.";
        }

        return (
            response.IsSuccessStatusCode,
            imageUrl,
            message
        );
    }
}