using Domain;
using Domain.Models.JsonModels;
using OfferPrice.Domain.Dtos;

namespace OfferPrice.Application.Interfaces;

public interface IFlightSearchService
{
    Task<Result<Root>> SearchFlightsAsync(FlightSearchRequest searchRequest);
}