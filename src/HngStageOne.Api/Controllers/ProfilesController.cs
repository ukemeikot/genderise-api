using HngStageOne.Api.Constants;
using HngStageOne.Api.DTOs.Requests;
using HngStageOne.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HngStageOne.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Profiles.Base)]
public class ProfilesController : ControllerBase
{
    private readonly IProfileService _profileService;

    public ProfilesController(IProfileService profileService)
    {
        _profileService = profileService;
    }

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
        return Ok(result);
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchProfiles([FromQuery] ProfileSearchRequest request, CancellationToken cancellationToken)
    {
        var result = await _profileService.SearchProfilesAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProfile(Guid id, CancellationToken cancellationToken)
    {
        await _profileService.DeleteProfileAsync(id, cancellationToken);
        return NoContent();
    }
}
