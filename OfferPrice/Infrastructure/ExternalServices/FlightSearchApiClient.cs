using System.Text;
using System.Text.Json;
using Domain;
using Domain.Models.JsonModels;
using OfferPrice.Application.Interfaces;
using OfferPrice.Domain.Dtos;

namespace OfferPrice.Infrastructure.ExternalServices;

public class FlightSearchApiClient(HttpClient httpClient) : IFlightSearchService
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<Result<Root>> SearchFlightsAsync(FlightSearchRequest searchRequest)
    {
        try
        {
            var content = SerializeRequest(searchRequest);
            var response = await _httpClient.PostAsync("", content);

            return await HandleResponseAsync(response);
        }
        catch (HttpRequestException)
        {
            return Result<Root>.Failure("Flight search service is unreachable.");
        }
        catch (JsonException)
        {
            return Result<Root>.Failure("Invalid response format from flight search service.");
        }
    }
    
    private static StringContent SerializeRequest(FlightSearchRequest request)
    {
        var json = JsonSerializer.Serialize(request);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static async Task<Result<Root>> HandleResponseAsync(HttpResponseMessage response)
    {
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return Result<Root>.Failure(
                $"Flight search failed: {response.StatusCode} - {responseContent}");
        }

        var result = JsonSerializer.Deserialize<Root>(
            responseContent,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return result is null
            ? Result<Root>.Failure("Failed to deserialize flight search response")
            : Result<Root>.Success(result);
    }

}