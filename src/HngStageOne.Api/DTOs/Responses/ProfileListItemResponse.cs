using System.Text.Json.Serialization;

namespace HngStageOne.Api.DTOs.Responses;

public class ProfileListItemResponse
{
    [JsonPropertyName("id")]
    public required Guid Id { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("gender")]
    public required string Gender { get; set; }

    [JsonPropertyName("age")]
    public required int Age { get; set; }

    [JsonPropertyName("age_group")]
    public required string AgeGroup { get; set; }

    [JsonPropertyName("country_id")]
    public required string CountryId { get; set; }
}
