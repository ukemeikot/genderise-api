namespace HngStageOne.Api.Domain.Entities;

public class Profile
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public required string Gender { get; set; }
    public required decimal GenderProbability { get; set; }
    public required int SampleSize { get; set; }
    public required int Age { get; set; }
    public required string AgeGroup { get; set; }
    public required string CountryId { get; set; }
    public required decimal CountryProbability { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
}
