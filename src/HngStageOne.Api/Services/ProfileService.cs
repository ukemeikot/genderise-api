using HngStageOne.Api.Clients.Interfaces;
using HngStageOne.Api.DTOs.Requests;
using HngStageOne.Api.DTOs.Responses;
using HngStageOne.Api.Domain.Entities;
using HngStageOne.Api.Helpers;
using HngStageOne.Api.Helpers.Exceptions;
using HngStageOne.Api.Repositories.Interfaces;
using HngStageOne.Api.Services.Interfaces;

namespace HngStageOne.Api.Services;

public class ProfileService : IProfileService
{
    private readonly IProfileRepository _repository;
    private readonly IGenderizeClient _genderizeClient;
    private readonly IAgifyClient _agifyClient;
    private readonly INationalizeClient _nationalizeClient;

    public ProfileService(
        IProfileRepository repository,
        IGenderizeClient genderizeClient,
        IAgifyClient agifyClient,
        INationalizeClient nationalizeClient)
    {
        _repository = repository;
        _genderizeClient = genderizeClient;
        _agifyClient = agifyClient;
        _nationalizeClient = nationalizeClient;
    }

    public async Task<SingleProfileResponse> CreateProfileAsync(CreateProfileRequest request)
    {
        string name = request.Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be empty or whitespace");
        }

        string normalizedName = NameNormalizer.Normalize(name);

        // Check if profile already exists
        var existingProfile = await _repository.GetByNormalizedNameAsync(normalizedName);
        if (existingProfile != null)
        {
            return new SingleProfileResponse
            {
                Status = "success",
                Message = "Profile already exists",
                Data = MapToProfileDetailResponse(existingProfile)
            };
        }

        // Call external APIs
        var genderResponse = await _genderizeClient.GetGenderAsync(name);
        var ageResponse = await _agifyClient.GetAgeAsync(name);
        var nationalityResponse = await _nationalizeClient.GetNationalityAsync(name);

        // Validate responses
        if (genderResponse == null || string.IsNullOrWhiteSpace(genderResponse.Gender) || genderResponse.Count == 0)
        {
            throw new InvalidUpstreamResponseException("Genderize");
        }

        if (ageResponse == null || ageResponse.Age == null)
        {
            throw new InvalidUpstreamResponseException("Agify");
        }

        if (nationalityResponse == null || nationalityResponse.Country == null || nationalityResponse.Country.Count == 0)
        {
            throw new InvalidUpstreamResponseException("Nationalize");
        }

        // Get the country with highest probability
        var topCountry = nationalityResponse.Country
            .OrderByDescending(c => c.Probability)
            .FirstOrDefault();

        if (topCountry == null || string.IsNullOrWhiteSpace(topCountry.CountryId))
        {
            throw new InvalidUpstreamResponseException("Nationalize");
        }

        // Classify age group
        string ageGroup = AgeGroupClassifier.Classify(ageResponse.Age.Value);

        // Create profile
        var profile = new Profile
        {
            Id = Guid.NewGuid(),
            Name = name,
            NormalizedName = normalizedName,
            Gender = genderResponse.Gender.ToLower(),
            GenderProbability = genderResponse.Probability ?? 0,
            SampleSize = genderResponse.Count ?? 0,
            Age = ageResponse.Age.Value,
            AgeGroup = ageGroup,
            CountryId = topCountry.CountryId.ToUpper(),
            CountryProbability = topCountry.Probability ?? 0,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _repository.AddAsync(profile);
        await _repository.SaveChangesAsync();

        return new SingleProfileResponse
        {
            Status = "success",
            Data = MapToProfileDetailResponse(profile)
        };
    }

    public async Task<SingleProfileResponse> GetProfileByIdAsync(Guid id)
    {
        var profile = await _repository.GetByIdAsync(id);
        if (profile == null)
        {
            throw new ProfileNotFoundException();
        }

        return new SingleProfileResponse
        {
            Status = "success",
            Data = MapToProfileDetailResponse(profile)
        };
    }

    public async Task<ProfilesListResponse> GetAllProfilesAsync(string? gender = null, string? countryId = null, string? ageGroup = null)
    {
        var profiles = await _repository.GetAllAsync(gender, countryId, ageGroup);

        var data = profiles.Select(p => new ProfileListItemResponse
        {
            Id = p.Id,
            Name = p.Name,
            Gender = p.Gender,
            Age = p.Age,
            AgeGroup = p.AgeGroup,
            CountryId = p.CountryId
        }).ToList();

        return new ProfilesListResponse
        {
            Status = "success",
            Count = data.Count,
            Data = data
        };
    }

    public async Task DeleteProfileAsync(Guid id)
    {
        var profile = await _repository.GetByIdAsync(id);
        if (profile == null)
        {
            throw new ProfileNotFoundException();
        }

        await _repository.DeleteAsync(profile);
        await _repository.SaveChangesAsync();
    }

    private static ProfileDetailResponse MapToProfileDetailResponse(Profile profile)
    {
        return new ProfileDetailResponse
        {
            Id = profile.Id,
            Name = profile.Name,
            Gender = profile.Gender,
            GenderProbability = profile.GenderProbability,
            SampleSize = profile.SampleSize,
            Age = profile.Age,
            AgeGroup = profile.AgeGroup,
            CountryId = profile.CountryId,
            CountryProbability = profile.CountryProbability,
            CreatedAt = profile.CreatedAt.ToString("O")
        };
    }
}
