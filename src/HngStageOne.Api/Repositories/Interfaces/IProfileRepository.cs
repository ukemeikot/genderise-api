using HngStageOne.Api.Domain.Entities;

namespace HngStageOne.Api.Repositories.Interfaces;

public interface IProfileRepository
{
    Task<Profile?> GetByIdAsync(Guid id);
    Task<Profile?> GetByNormalizedNameAsync(string normalizedName);
    Task<List<Profile>> GetAllAsync(string? gender = null, string? countryId = null, string? ageGroup = null);
    Task AddAsync(Profile profile);
    Task DeleteAsync(Profile profile);
    Task SaveChangesAsync();
}
