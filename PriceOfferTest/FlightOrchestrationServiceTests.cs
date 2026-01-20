
using Domain;
using Domain.Dtos;
using Domain.Models.JsonModels;
using Microsoft.Extensions.Logging;
using Moq;
using OfferPrice.Api.Dtos;
using OfferPrice.Application.Interfaces;
using OfferPrice.Application.Services;
using OfferPrice.Domain.Dtos;
using Xunit;

namespace PriceOfferTest;

public class FlightOrchestrationServiceTests
{
    private readonly Mock<IFlightSearchService> _flightSearchServiceMock;
    private readonly Mock<IOfferPriceService> _offerPriceServiceMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly FlightOrchestrationService _service;

    public FlightOrchestrationServiceTests()
    {
        _flightSearchServiceMock = new Mock<IFlightSearchService>();
        _offerPriceServiceMock = new Mock<IOfferPriceService>();
        _cacheServiceMock = new Mock<ICacheService>();
        var loggerMock = new Mock<ILogger<FlightOrchestrationService>>();

        _service = new FlightOrchestrationService(
            _flightSearchServiceMock.Object,
            _offerPriceServiceMock.Object,
            _cacheServiceMock.Object,
            loggerMock.Object
        );
    }

    [Fact]
    public async Task GetFlightPricesAsync_CacheHit_ReturnsCachedData()
    {
        var searchRequest = new FlightSearchRequest
        {
            OriginAirportCode = "JFK",
            DestinationAirportCode = "LAX",
            FlightDate = DateTime.Now,
            AdultsCount = 1
        };

        var cacheKey =
            $"{searchRequest.OriginAirportCode}_{searchRequest.DestinationAirportCode}_{searchRequest.FlightDate:yyyy-MM-dd}_oneway_1_0_0";

        var cachedFlightData = new Root
        {
            FlightRequestSessionId = "session123",
            RecommendedFlights = [new RecommendedFlight { Id = "flight1", IsNdc = true }]
        };

        var offerPriceResponse = new OfferPriceResponse
        {
            ValidatingCarrier = "BA",
            CheckRules =
                "Fare Family: BASIC ECONOMY\nTrips.FlightsCore.Dtos.SectionViewModel\nTrips.FlightsCore.Dtos.SectionViewModel\nTrips.FlightsCore.Dtos.SectionViewModel",

            TotalApiCostSar = 2778,
            Offers =
            [
                new FareOffer()
                {
                    OfferIndex = 0,
                    FareFamilyName = "BASIC ECONOMY",
                    BaggageAllowance = "1 piece(s)",
                    BaggageAllowanceDetailed = "Cabin: 1 piece(s)",
                    CancellationPenalty = "Non-refundable",
                    DateChangePenalty = "Not changeable",
                    IsPrimaryOffer = true,
                    PriceDifference = 0,
                    FareFamilyDescription =
                    [
                        new FareFamilySection()
                        {
                            Title = "Baggage Allowance",
                            Items =
                            [
                                new FareFamilyItem
                                {
                                    Text = "Cabin bag: 1 piece(s)",
                                    Icon = "CabinBaggage"
                                },

                                new FareFamilyItem
                                {
                                    Text = "No checked baggage",
                                    Icon = "Cancel"
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        _cacheServiceMock.Setup(c => c.Get<Root>(cacheKey))
            .Returns(Result<Root>.Success(cachedFlightData));

        _offerPriceServiceMock.Setup(o =>
                o.GetOfferPriceAsync("session123", "flight1"))
            .ReturnsAsync(Result<OfferPriceResponse>.Success(offerPriceResponse));

        var result = await _service.GetFlightPricesAsync(searchRequest);

        Assert.True(result.IsSuccess);
        if (result.Value != null)
        {
            Assert.Single(result.Value);
            Assert.Equal(offerPriceResponse, result.Value[0]);
        }

        _flightSearchServiceMock.Verify(
            f => f.SearchFlightsAsync(It.IsAny<FlightSearchRequest>()),
            Times.Never);
    }

    [Fact]
    public async Task GetFlightPricesAsync_OfferPriceFails_ReturnsFailure()
    {
        var searchRequest = new FlightSearchRequest
        {
            OriginAirportCode = "JFK",
            DestinationAirportCode = "LAX",
            FlightDate = DateTime.Now,
            AdultsCount = 1
        };

        var cacheKey =
            $"{searchRequest.OriginAirportCode}_{searchRequest.DestinationAirportCode}_{searchRequest.FlightDate:yyyy-MM-dd}_oneway_1_0_0";

        var flightData = new Root
        {
            FlightRequestSessionId = "session123",
            RecommendedFlights = [new RecommendedFlight { Id = "flight1", IsNdc = true }]
        };

        _cacheServiceMock.Setup(c => c.Get<Root>(cacheKey))
            .Returns(Result<Root>.Failure("Cache miss"));

        _flightSearchServiceMock.Setup(f =>
                f.SearchFlightsAsync(searchRequest))
            .ReturnsAsync(Result<Root>.Success(flightData));

        _offerPriceServiceMock.Setup(o =>
                o.GetOfferPriceAsync("session123", "flight1"))
            .ReturnsAsync(Result<OfferPriceResponse>.Failure("Offer price error"));

        var result = await _service.GetFlightPricesAsync(searchRequest);

        Assert.False(result.IsSuccess);
        Assert.Contains("Unable to retrieve offer price for flight flight1", result.Error);
    }

    [Fact]
    public async Task GetFlightPricesAsync_NoNdcFlight_ReturnsFailure()
    {
        var searchRequest = new FlightSearchRequest
        {
            OriginAirportCode = "JFK",
            DestinationAirportCode = "LAX",
            FlightDate = DateTime.Now,
            AdultsCount = 1
        };

        var cacheKey =
            $"{searchRequest.OriginAirportCode}_{searchRequest.DestinationAirportCode}_{searchRequest.FlightDate:yyyy-MM-dd}_oneway_1_0_0";

        var flightData = new Root
        {
            FlightRequestSessionId = "session123",
            RecommendedFlights = [new RecommendedFlight { Id = "flight1", IsNdc = false }]
        };

        _cacheServiceMock.Setup(c => c.Get<Root>(cacheKey))
            .Returns(Result<Root>.Failure("Cache miss"));

        _flightSearchServiceMock.Setup(f =>
                f.SearchFlightsAsync(searchRequest))
            .ReturnsAsync(Result<Root>.Success(flightData));

        var result = await _service.GetFlightPricesAsync(searchRequest);

        Assert.False(result.IsSuccess);
        Assert.Equal("No NDC-enabled flight found in search results", result.Error);
    }

    [Fact]
    public async Task GetFlightPricesAsync_FlightSearchFails_ReturnsFailure()
    {
        var searchRequest = new FlightSearchRequest
        {
            OriginAirportCode = "JFK",
            DestinationAirportCode = "LAX",
            FlightDate = DateTime.Now,
            AdultsCount = 1
        };

        var cacheKey =
            $"{searchRequest.OriginAirportCode}_{searchRequest.DestinationAirportCode}_{searchRequest.FlightDate:yyyy-MM-dd}_oneway_1_0_0";

        _cacheServiceMock.Setup(c => c.Get<Root>(cacheKey))
            .Returns(Result<Root>.Failure("Cache miss"));

        _flightSearchServiceMock.Setup(f => f.SearchFlightsAsync(searchRequest))
            .ReturnsAsync(Result<Root>.Failure("Flight search error"));

        var result = await _service.GetFlightPricesAsync(searchRequest);

        Assert.False(result.IsSuccess);
        Assert.Equal("Flight search error", result.Error);

        _offerPriceServiceMock.Verify(o => o.GetOfferPriceAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetFlightPricesAsync_MultipleNdcFlights_OneOfferPriceFails_ReturnsFailure()
    {
        var searchRequest = new FlightSearchRequest
        {
            OriginAirportCode = "JFK",
            DestinationAirportCode = "LAX",
            FlightDate = DateTime.Now,
            AdultsCount = 1
        };

        var cacheKey =
            $"{searchRequest.OriginAirportCode}_{searchRequest.DestinationAirportCode}_{searchRequest.FlightDate:yyyy-MM-dd}_oneway_1_0_0";

        var flightData = new Root
        {
            FlightRequestSessionId = "session123",
            RecommendedFlights =
            [
                new RecommendedFlight { Id = "flight1", IsNdc = true },
                new RecommendedFlight { Id = "flight2", IsNdc = true }
            ]
        };

        var offerPriceResponse1 = new OfferPriceResponse
        {
            TotalCost = 2778,
            AdultPrice = 2778,
            ValidatingCarrier = "BA",
            CheckRules = "Fare Family: BASIC ECONOMY",
            IsInvalid = false,
            IsVerified = true,
            TotalApiCostSar = 2778,
            Offers =
            [
                new FareOffer
                {
                    OfferIndex = 0,
                    FareFamilyName = "BASIC ECONOMY",
                    BaggageAllowance = "1 piece(s)",
                    BaggageAllowanceDetailed = "Cabin: 1 piece(s)",
                    CancellationPenalty = "Non-refundable",
                    DateChangePenalty = "Not changeable",
                    IsPrimaryOffer = true,
                    PriceDifference = 0,
                    FareFamilyDescription =
                    [
                        new FareFamilySection
                        {
                            Title = "Baggage Allowance",
                            Items =
                            [
                                new FareFamilyItem { Text = "Cabin bag: 1 piece(s)", Icon = "CabinBaggage" },
                                new FareFamilyItem { Text = "No checked baggage", Icon = "Cancel" }
                            ]
                        }
                    ]
                }
            ]
        };

        _cacheServiceMock.Setup(c => c.Get<Root>(cacheKey))
            .Returns(Result<Root>.Failure("Cache miss"));

        _flightSearchServiceMock.Setup(f => f.SearchFlightsAsync(searchRequest))
            .ReturnsAsync(Result<Root>.Success(flightData));

        _offerPriceServiceMock.Setup(o => o.GetOfferPriceAsync("session123", "flight1"))
            .ReturnsAsync(Result<OfferPriceResponse>.Success(offerPriceResponse1));

        _offerPriceServiceMock.Setup(o => o.GetOfferPriceAsync("session123", "flight2"))
            .ReturnsAsync(Result<OfferPriceResponse>.Failure("Offer price error for flight2"));

        var result = await _service.GetFlightPricesAsync(searchRequest);

        Assert.False(result.IsSuccess);
        Assert.Contains("Unable to retrieve offer price for flight flight2. Offer price error for flight2",
            result.Error);
    }
}