namespace OfferPrice.Api.Dtos;

public class FareOffer
{
    public int OfferIndex { get; set; }
    public string FareFamilyName { get; set; }
    public string BaggageAllowance { get; set; }
    public string BaggageAllowanceDetailed { get; set; }
    public string CancellationPenalty { get; set; }
    public string DateChangePenalty { get; set; }
    public bool IsPrimaryOffer { get; set; }
    public decimal PriceDifference { get; set; }
    public List<FareFamilySection> FareFamilyDescription { get; set; }
}