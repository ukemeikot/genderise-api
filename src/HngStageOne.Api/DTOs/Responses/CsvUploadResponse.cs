using System.Text.Json.Serialization;

namespace HngStageOne.Api.DTOs.Responses;

public class CsvUploadResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "success";

    [JsonPropertyName("total_rows")]
    public int TotalRows { get; set; }

    [JsonPropertyName("inserted")]
    public int Inserted { get; set; }

    [JsonPropertyName("skipped")]
    public int Skipped { get; set; }

    [JsonPropertyName("reasons")]
    public Dictionary<string, int> Reasons { get; set; } = new();
}
