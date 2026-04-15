using System.Text.Json.Serialization;

namespace HngStageOne.Api.DTOs.Responses;

public class ProfilesListResponse
{
    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("count")]
    public required int Count { get; set; }

    [JsonPropertyName("data")]
    public required List<ProfileListItemResponse> Data { get; set; }
}
