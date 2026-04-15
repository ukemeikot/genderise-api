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
    public async Task<IActionResult> CreateProfile([FromBody] CreateProfileRequest request)
    {
        if (request == null)
        {
            return BadRequest(new { status = "error", message = "Missing or invalid name provided" });
        }

        var result = await _profileService.CreateProfileAsync(request);
        return CreatedAtAction(nameof(GetProfile), new { id = result.Data.Id }, result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProfile(Guid id)
    {
        var result = await _profileService.GetProfileByIdAsync(id);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllProfiles(
        [FromQuery] string? gender,
        [FromQuery] string? country_id,
        [FromQuery] string? age_group)
    {
        var result = await _profileService.GetAllProfilesAsync(gender, country_id, age_group);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProfile(Guid id)
    {
        await _profileService.DeleteProfileAsync(id);
        return NoContent();
    }
}
