using System.Text.Json.Serialization;

namespace HngStageOne.Api.DTOs.Responses;

public class ProfilesListResponse
{
    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("page")]
    public required int Page { get; set; }

    [JsonPropertyName("limit")]
    public required int Limit { get; set; }

    [JsonPropertyName("total")]
    public required int Total { get; set; }

    [JsonPropertyName("data")]
    public required List<ProfileDetailResponse> Data { get; set; }
}
