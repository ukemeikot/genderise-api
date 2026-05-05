using System.Text;
using HngStageOne.Api.Constants;
using HngStageOne.Api.DTOs.Requests;
using HngStageOne.Api.DTOs.Responses;
using HngStageOne.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HngStageOne.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthConstants.AnalystOrAdminPolicy)]
[Route("api/v1/profiles")]
public class V1ProfilesController : ControllerBase
{
    private readonly IProfileService _profileService;
    private readonly ICsvIngestionService _csvIngestionService;

    public V1ProfilesController(IProfileService profileService, ICsvIngestionService csvIngestionService)
    {
        _profileService = profileService;
        _csvIngestionService = csvIngestionService;
    }

    [Authorize(Policy = AuthConstants.AdminOnlyPolicy)]
    [HttpPost]
    public async Task<IActionResult> CreateProfile([FromBody] CreateProfileRequest request, CancellationToken cancellationToken)
    {
        var result = await _profileService.CreateProfileAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetProfile), new { id = result.Data.Id }, result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProfile(Guid id, CancellationToken cancellationToken)
    {
        var result = await _profileService.GetProfileByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllProfiles([FromQuery] ProfileQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await _profileService.GetProfilesAsync(request, cancellationToken);
        return Ok(ToV1(result));
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchProfiles([FromQuery] ProfileSearchRequest request, CancellationToken cancellationToken)
    {
        var result = await _profileService.SearchProfilesAsync(request, cancellationToken);
        return Ok(ToV1(result));
    }

    [HttpGet("export.csv")]
    public async Task<IActionResult> ExportProfiles([FromQuery] ProfileQueryRequest request, [FromQuery(Name = "q")] string? q, CancellationToken cancellationToken)
    {
        var profiles = await _profileService.ExportProfilesAsync(request, q, cancellationToken);
        var csv = BuildCsv(profiles);
        var fileName = $"profiles-export-{DateTime.UtcNow:yyyy-MM-dd}.csv";
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }

    [Authorize(Policy = AuthConstants.AdminOnlyPolicy)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProfile(Guid id, CancellationToken cancellationToken)
    {
        await _profileService.DeleteProfileAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Streams a CSV file (up to 500,000 rows) and bulk-upserts the valid rows.
    /// Bad rows are skipped; the response summarizes counts and reasons.
    /// Admin-only; the upload runs off the request's DbContext via a context factory
    /// so query traffic on other connections is not blocked.
    /// </summary>
    [Authorize(Policy = AuthConstants.AdminOnlyPolicy)]
    [HttpPost("upload")]
    [RequestFormLimits(MultipartBodyLengthLimit = 500_000_000)]
    [RequestSizeLimit(500_000_000)]
    public async Task<IActionResult> UploadCsv(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { status = "error", message = "file is required" });
        }

        await using var stream = file.OpenReadStream();
        var summary = await _csvIngestionService.IngestAsync(stream, cancellationToken);
        return Ok(summary);
    }

    private static V1ProfilesListResponse ToV1(ProfilesListResponse response)
    {
        return new V1ProfilesListResponse
        {
            Status = response.Status,
            Data = response.Data,
            Pagination = new PaginationMetadata
            {
                Page = response.Page,
                Limit = response.Limit,
                Total = response.Total,
                TotalPages = response.TotalPages,
                HasNext = response.Page < response.TotalPages,
                HasPrevious = response.Page > 1
            }
        };
    }

    private static string BuildCsv(IEnumerable<ProfileDetailResponse> profiles)
    {
        var builder = new StringBuilder();
        builder.AppendLine("id,name,gender,gender_probability,age,age_group,country_id,country_name,country_probability,created_at");
        foreach (var profile in profiles)
        {
            builder.AppendLine(string.Join(',', new[]
            {
                Escape(profile.Id.ToString()),
                Escape(profile.Name),
                Escape(profile.Gender),
                Escape(profile.GenderProbability.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                Escape(profile.Age.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                Escape(profile.AgeGroup),
                Escape(profile.CountryId),
                Escape(profile.CountryName),
                Escape(profile.CountryProbability.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                Escape(profile.CreatedAt)
            }));
        }

        return builder.ToString();
    }

    private static string Escape(string? value)
    {
        var text = value ?? "";
        return text.Contains(',') || text.Contains('"') || text.Contains('\n')
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }
}
