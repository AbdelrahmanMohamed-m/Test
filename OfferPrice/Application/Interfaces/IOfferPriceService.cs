using Domain;
using Domain.Dtos;

namespace OfferPrice.Application.Interfaces;

public interface IOfferPriceService
{
    Task<Result<OfferPriceResponse>> GetOfferPriceAsync(string flightRequestSessionId, string selectedFlightId);
}