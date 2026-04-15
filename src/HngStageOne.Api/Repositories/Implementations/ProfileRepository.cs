using HngStageOne.Api.Data;
using HngStageOne.Api.Domain.Entities;
using HngStageOne.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HngStageOne.Api.Repositories.Implementations;

public class ProfileRepository : IProfileRepository
{
    private readonly AppDbContext _context;

    public ProfileRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Profile?> GetByIdAsync(Guid id)
    {
        return await _context.Profiles.FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Profile?> GetByNormalizedNameAsync(string normalizedName)
    {
        return await _context.Profiles.FirstOrDefaultAsync(p => p.NormalizedName == normalizedName);
    }

    public async Task<List<Profile>> GetAllAsync(string? gender = null, string? countryId = null, string? ageGroup = null)
    {
        var query = _context.Profiles.AsQueryable();

        if (!string.IsNullOrWhiteSpace(gender))
        {
            var genderLower = gender.ToLower();
            query = query.Where(p => p.Gender.ToLower() == genderLower);
        }

        if (!string.IsNullOrWhiteSpace(countryId))
        {
            var countryIdUpper = countryId.ToUpper();
            query = query.Where(p => p.CountryId.ToUpper() == countryIdUpper);
        }

        if (!string.IsNullOrWhiteSpace(ageGroup))
        {
            var ageGroupLower = ageGroup.ToLower();
            query = query.Where(p => p.AgeGroup.ToLower() == ageGroupLower);
        }

        return await query.ToListAsync();
    }

    public async Task AddAsync(Profile profile)
    {
        await _context.Profiles.AddAsync(profile);
    }

    public async Task DeleteAsync(Profile profile)
    {
        _context.Profiles.Remove(profile);
        await Task.CompletedTask;
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
