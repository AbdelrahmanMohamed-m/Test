using Domain;
using Domain.Dtos;
using OfferPrice.Domain.Dtos;

namespace OfferPrice.Application.Interfaces;

public interface IFlightOrchestrationService
{
    Task<Result<List<OfferPriceResponse>>> GetFlightPricesAsync(FlightSearchRequest searchRequest);
}