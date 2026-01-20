using System.Text.Json.Serialization;

namespace OfferPrice.Domain.Dtos;


using System.Text.Json.Serialization;

public class FlightSearchRequest
{
    [JsonPropertyName("ClassType")]
    public string ClassType { get; set; } = "M,Y";
    
    [JsonPropertyName("FlightDate")]
    public DateTime FlightDate { get; set; }
    
    [JsonPropertyName("ReturnDate")]
    public DateTime? ReturnDate { get; set; }
    
    [JsonPropertyName("OriginAirportCode")]
    public string OriginAirportCode { get; set; }
    
    [JsonPropertyName("DestinationAirportCode")]
    public string DestinationAirportCode { get; set; }
    
    [JsonPropertyName("AdultsCount")]
    public int AdultsCount { get; set; } = 1;
    
    [JsonPropertyName("ChildrenCount")]
    public int ChildrenCount { get; set; } = 0;
    
    [JsonPropertyName("InfantsCount")]
    public int InfantsCount { get; set; } = 0;
    
    [JsonPropertyName("IsRoundTrip")]
    public bool IsRoundTrip { get; set; } = true;
    
    [JsonPropertyName("IsMultiCity")]
    public bool IsMultiCity { get; set; } = false;
    
    [JsonPropertyName("IsOriginCity")]
    public bool IsOriginCity { get; set; } = false;
    
    [JsonPropertyName("IsDestinationCity")]
    public bool IsDestinationCity { get; set; } = false;
    
    [JsonPropertyName("IncludedBaggage")]
    public bool IncludedBaggage { get; set; } = true;
}