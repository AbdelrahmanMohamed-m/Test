using Domain;
using Domain.Dtos;
using Domain.Models.JsonModels;
using OfferPrice.Api.Dtos;
using OfferPrice.Application.Interfaces;
using OfferPrice.Domain.Dtos;

namespace OfferPrice.Application.Services;

public class FlightOrchestrationService : IFlightOrchestrationService
{
    private readonly IFlightSearchService _flightSearchService;
    private readonly IOfferPriceService _offerPriceService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<FlightOrchestrationService> _logger;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(1);

    public FlightOrchestrationService(
        IFlightSearchService flightSearchService,
        IOfferPriceService offerPriceService,
        ICacheService cacheService,
        ILogger<FlightOrchestrationService> logger)
    {
        _flightSearchService = flightSearchService;
        _offerPriceService = offerPriceService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Result<List<OfferPriceResponse>>> GetFlightPricesAsync(FlightSearchRequest searchRequest)
    {
        var cacheKey = $"{searchRequest.OriginAirportCode}_{searchRequest.DestinationAirportCode}_{searchRequest.FlightDate:yyyy-MM-dd}_{searchRequest.ReturnDate?.ToString("yyyy-MM-dd") ?? "oneway"}_{searchRequest.AdultsCount}_{searchRequest.ChildrenCount}_{searchRequest.InfantsCount}";

        var cachedResult = _cacheService.Get<Root>(cacheKey);
        Root flightData;

        if (cachedResult is { IsSuccess: true, Value: not null })
        {
            flightData = cachedResult.Value;
            _logger.LogInformation("Using cached flight search results for key {CacheKey}", cacheKey);
        }
        else
        {
            var searchResult = await _flightSearchService.SearchFlightsAsync(searchRequest);

            if (!searchResult.IsSuccess)
            {
                return Result<List<OfferPriceResponse>>.Failure(searchResult.Error);
            }

            flightData = searchResult.Value;

            _cacheService.Set(cacheKey, flightData, CacheExpiration);
           
        }

        var ndcFlights = flightData?.RecommendedFlights?.Where(f => f.IsNdc).ToList();

        if (ndcFlights == null || !ndcFlights.Any())
        {
            return Result<List<OfferPriceResponse>>.Failure("No NDC-enabled flight found in search results");
        }

        var offerPriceResponses = new List<OfferPriceResponse>();

        foreach (var ndcFlight in ndcFlights)
        {
            var offerPriceResult = await _offerPriceService.GetOfferPriceAsync(
                flightData!.FlightRequestSessionId,
                ndcFlight.Id
            );

            if (!offerPriceResult.IsSuccess)
            {
                return Result<List<OfferPriceResponse>>.Failure(
                    $"Unable to retrieve offer price for flight {ndcFlight.Id}. {offerPriceResult.Error}");
            }

            offerPriceResponses.Add(offerPriceResult.Value);
        }

        return Result<List<OfferPriceResponse>>.Success(offerPriceResponses);
    }
}