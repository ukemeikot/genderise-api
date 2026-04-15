using HngStageOne.Api.DTOs.Requests;
using HngStageOne.Api.DTOs.Responses;

namespace HngStageOne.Api.Services.Interfaces;

public interface IProfileService
{
    Task<SingleProfileResponse> CreateProfileAsync(CreateProfileRequest request);
    Task<SingleProfileResponse> GetProfileByIdAsync(Guid id);
    Task<ProfilesListResponse> GetAllProfilesAsync(string? gender = null, string? countryId = null, string? ageGroup = null);
    Task DeleteProfileAsync(Guid id);
}
