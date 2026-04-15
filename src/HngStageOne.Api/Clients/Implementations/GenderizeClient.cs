using HngStageOne.Api.Clients.Interfaces;
using HngStageOne.Api.DTOs.ExternalApis;
using System.Text.Json;

namespace HngStageOne.Api.Clients.Implementations;

public class GenderizeClient : IGenderizeClient
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://api.genderize.io";

    public GenderizeClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(BaseUrl);
    }

    public async Task<GenderizeResponse?> GetGenderAsync(string name)
    {
        try
        {
            var response = await _httpClient.GetAsync($"?name={Uri.EscapeDataString(name)}");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<GenderizeResponse>(content);
            return result;
        }
        catch
        {
            return null;
        }
    }
}
