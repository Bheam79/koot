using Koot.Api.Dtos.Quizzes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Koot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/uploads")]
public class UploadsController : ControllerBase
{
    private const long MaxBytes = 5 * 1024 * 1024; // 5 MB

    // Accepted MIME types and the file-extension we will write.
    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/jpg"]  = ".jpg",
        ["image/png"]  = ".png",
        ["image/gif"]  = ".gif",
        ["image/webp"] = ".webp",
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp",
    };

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<UploadsController> _logger;

    public UploadsController(IWebHostEnvironment env, ILogger<UploadsController> logger)
    {
        _env = env;
        _logger = logger;
    }

    /// <summary>
    /// Accept a single multipart file upload (field name "file") and save it under
    /// wwwroot/uploads/. Returns a server-relative URL the client can stash in
    /// e.g. Quiz.CoverImageUrl or Question.ImageUrl.
    /// </summary>
    [HttpPost("image")]
    [RequestSizeLimit(MaxBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxBytes)]
    public async Task<ActionResult<UploadResponse>> UploadImage([FromForm] IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "No file provided." });
        }

        if (file.Length > MaxBytes)
        {
            return BadRequest(new { error = "File exceeds 5 MB limit." });
        }

        if (!AllowedTypes.TryGetValue(file.ContentType ?? string.Empty, out var canonicalExt))
        {
            return BadRequest(new { error = "Unsupported content type. Allowed: jpg, png, gif, webp." });
        }

        var providedExt = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(providedExt) || !AllowedExtensions.Contains(providedExt))
        {
            // Fall back to the extension implied by the content type.
            providedExt = canonicalExt;
        }

        // Resolve uploads dir under WebRootPath (wwwroot). Create if missing.
        var webRoot = _env.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");
        }
        var uploadsDir = Path.Combine(webRoot, "uploads");
        Directory.CreateDirectory(uploadsDir);

        var safeName = $"{Guid.NewGuid():N}{providedExt.ToLowerInvariant()}";
        var fullPath = Path.Combine(uploadsDir, safeName);

        await using (var stream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream);
        }

        var url = $"/uploads/{safeName}";

        _logger.LogInformation(
            "Saved upload {File} ({Bytes} bytes, {ContentType}) -> {Url}",
            file.FileName, file.Length, file.ContentType, url);

        return Ok(new UploadResponse
        {
            Url = url,
            FileName = safeName,
            Size = file.Length,
            ContentType = file.ContentType ?? string.Empty,
        });
    }
}
