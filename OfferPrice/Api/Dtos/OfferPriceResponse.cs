using OfferPrice.Api.Dtos;

namespace Domain.Dtos;

public class OfferPriceResponse
{
    public decimal TotalCost { get; set; }
    public decimal AdultPrice { get; set; }
    public string ValidatingCarrier { get; set; }
    public bool IsInvalid { get; set; }
    public bool IsVerified { get; set; }
    public required string CheckRules { get; set; }
    public required decimal TotalApiCostSar { get; set; }
    public required List<FareOffer> Offers { get; set; }
}