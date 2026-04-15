using System.Text.Json.Serialization;

namespace HngStageOne.Api.DTOs.Responses;

public class ProfileDetailResponse
{
    [JsonPropertyName("id")]
    public required Guid Id { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("gender")]
    public required string Gender { get; set; }

    [JsonPropertyName("gender_probability")]
    public required decimal GenderProbability { get; set; }

    [JsonPropertyName("sample_size")]
    public required int SampleSize { get; set; }

    [JsonPropertyName("age")]
    public required int Age { get; set; }

    [JsonPropertyName("age_group")]
    public required string AgeGroup { get; set; }

    [JsonPropertyName("country_id")]
    public required string CountryId { get; set; }

    [JsonPropertyName("country_probability")]
    public required decimal CountryProbability { get; set; }

    [JsonPropertyName("created_at")]
    public required string CreatedAt { get; set; }
}
