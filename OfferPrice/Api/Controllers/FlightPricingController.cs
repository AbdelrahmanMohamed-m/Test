using Domain;
using Microsoft.AspNetCore.Mvc;
using OfferPrice.Application.Interfaces;
using OfferPrice.Domain.Dtos;

namespace OfferPrice.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FlightPricingController : ControllerBase
{
    private readonly IFlightOrchestrationService _orchestrationService;
    private readonly ILogger<FlightPricingController> _logger;

    public FlightPricingController(
        IFlightOrchestrationService orchestrationService,
        ILogger<FlightPricingController> logger)
    {
        _orchestrationService = orchestrationService;
        _logger = logger;
    }

    [HttpPost("prices")]
    public async Task<IActionResult> GetFlightPrices([FromBody] FlightSearchRequest searchRequest)
    {
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Invalid model state: {ModelState}", ModelState);
            return BadRequest(ModelState);
        }
        
        _logger.LogInformation("Received flight prices request for {Origin} to {Destination} on {Date}", 
            searchRequest.OriginAirportCode, 
            searchRequest.DestinationAirportCode, 
            searchRequest.FlightDate);
        
        var result = await _orchestrationService.GetFlightPricesAsync(searchRequest);

        if (!result.IsSuccess)
        {
            _logger.LogError("Flight prices request failed: {Error}", result.Error);
            return BadRequest(new { error = result.Error });
        }

        _logger.LogInformation("Successfully retrieved {Count} flight prices", result.Value);
        return Ok(result.Value);
    }
}