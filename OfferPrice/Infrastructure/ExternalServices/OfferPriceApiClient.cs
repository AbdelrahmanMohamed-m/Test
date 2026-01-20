using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Domain;
using Domain.Dtos;
using OfferPrice.Api.Dtos;
using OfferPrice.Application.Interfaces;
using OfferPrice.Domain.Dtos;

namespace OfferPrice.Application.ExternalServices;

public class OfferPriceApiClient(HttpClient httpClient, ILogger<OfferPriceApiClient> logger) : IOfferPriceService
{
    public async Task<Result<OfferPriceResponse>> GetOfferPriceAsync(
        string flightRequestSessionId,
        string selectedFlightId)
    {
        try
        {
            logger.LogInformation("Processing offer price for session {SessionId} and flight {FlightId}", flightRequestSessionId, selectedFlightId);
            var requestBody = new
            {
                FlightRequestSessionId = flightRequestSessionId,
                SelectedFlightId = selectedFlightId
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync("", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return Result<OfferPriceResponse>.Failure($"Offer price failed: {response.StatusCode} - {error}");
            }

            var xmlContent = await response.Content.ReadAsStringAsync();

            var errorMessage = ParseErrorsFromXml(xmlContent);
            if (!string.IsNullOrEmpty(errorMessage))
                return Result<OfferPriceResponse>.Failure(errorMessage);

            var offerPriceData = ParseSoapResponse(xmlContent);

            if (offerPriceData != null) return Result<OfferPriceResponse>.Success(offerPriceData);

            return Result<OfferPriceResponse>.Failure("Failed to parse offer price response from XML");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calling offer price API");
            return Result<OfferPriceResponse>.Failure($"Offer price API error: {ex.Message}");
        }
    }

    private static OfferPriceResponse? ParseSoapResponse(string xmlContent)
    {
        XNamespace ns = "http://xml.amadeus.com/2010/06/Travel_OfferPriceRS_v1";

        var doc = XDocument.Parse(xmlContent);
        var response = doc.Descendants(ns + "Response").FirstOrDefault();


        var pricedOffer = response?.Descendants(ns + "PricedOffer").FirstOrDefault()?.Element(ns + "Offer");
        if (pricedOffer == null)
            return null;

        var totalPriceElement = pricedOffer.Descendants(ns + "TotalPrice").FirstOrDefault()?.Element(ns + "TotalAmount");
        decimal basePrice = decimal.Parse(totalPriceElement?.Value ?? "0", CultureInfo.InvariantCulture) / 100; // Assuming amounts are in cents

        
        string validatingCarrier = pricedOffer.Element(ns + "OwnerCode")?.Value ?? string.Empty;

        var fareDetail = pricedOffer.Descendants(ns + "FareDetail").FirstOrDefault();
        var baseAmountElement = fareDetail?.Descendants(ns + "BaseAmount").FirstOrDefault();
        decimal apiCost = decimal.Parse(baseAmountElement?.Value ?? "0", CultureInfo.InvariantCulture) / 100;

        
        var otherOffers = response?.Descendants(ns + "OtherOffers").SelectMany(o => o.Elements(ns + "Offer"));

        var offers = new List<FareOffer>();

        var i = 0;
        if (otherOffers == null)
            return new OfferPriceResponse
            {
                TotalCost = basePrice,
                AdultPrice = basePrice,
                ValidatingCarrier = validatingCarrier,
                IsInvalid = false,
                IsVerified = true,
                CheckRules = $"Fare Family: {offers.FirstOrDefault()?.FareFamilyName ?? "Unknown"}",
                TotalApiCostSar = apiCost,
                Offers = offers
            };
        foreach (var offer in otherOffers)
        {
            var offerTotalPrice =
                offer.Descendants(ns + "TotalPrice").FirstOrDefault()?.Element(ns + "TotalAmount");
            var offerPrice = decimal.Parse(offerTotalPrice?.Value ?? "0", CultureInfo.InvariantCulture) / 100;
            var priceDifference = offerPrice - basePrice;

            var journeyOverview = offer.Element(ns + "JourneyOverview");
            var priceClassRef = journeyOverview?.Elements(ns + "JourneyPriceClass").FirstOrDefault()
                ?.Element(ns + "PriceClassRef")?.Value ?? "";

            var priceClassList = response?.Element(ns + "DataLists")?.Element(ns + "PriceClassList");
            var priceClass = priceClassList?.Elements(ns + "PriceClass")
                .FirstOrDefault(pc => pc.Element(ns + "Code")?.Value == priceClassRef);
            var fareFamilyName = priceClass?.Element(ns + "Name")?.Value ?? priceClassRef;

            var baggageAllowance = offer.Elements(ns + "BaggageAllowance").FirstOrDefault();
            var baggageRef = baggageAllowance?.Element(ns + "BaggageAllowanceRefID")?.Value;
            var baggageList = response?.Element(ns + "DataLists")?.Element(ns + "BaggageAllowanceList");
            var baggage = baggageList?.Elements(ns + "BaggageAllowance")
                .FirstOrDefault(ba => ba.Element(ns + "BaggageAllowanceID")?.Value == baggageRef);
            var weightAllowance = baggage?.Element(ns + "WeightAllowance")?.Element(ns + "MaximumWeightMeasure");
            int checkedBags =
                weightAllowance != null
                    ? (int)(decimal.Parse(weightAllowance.Value) / 25)
                    : 0; // Assuming 25kg per bag

            var offerItem = offer.Element(ns + "OfferItem");
            var fareComp = offerItem?.Element(ns + "FareDetail")?.Elements(ns + "FareComponent").FirstOrDefault();
            var fareRules = fareComp?.Element(ns + "FareRules");
            var penalty = fareRules?.Element(ns + "Penalty");
            bool refundable = penalty?.Attribute("CancelFeeInd")?.Value == "true";
            bool changeable = penalty?.Attribute("ChangeFeeInd")?.Value == "true";

            offers.Add(new FareOffer
            {
                OfferIndex = i++,
                FareFamilyName = fareFamilyName,
                BaggageAllowance = $"{Math.Max(1, checkedBags)} piece(s)",
                BaggageAllowanceDetailed = checkedBags > 0
                    ? $"Cabin: 1 piece(s) | Checked: {checkedBags} piece(s)"
                    : "Cabin: 1 piece(s)",
                CancellationPenalty = refundable ? "Refundable" : "Non-refundable",
                DateChangePenalty = changeable ? "Changeable" : "Not changeable",
                IsPrimaryOffer = priceDifference == 0,
                PriceDifference = priceDifference,
                FareFamilyDescription = BuildFareFamilyDescription(
                    checkedBags,
                    refundable,
                    changeable,
                    fareFamilyName
                )
            });
        }

        return new OfferPriceResponse
        {
            TotalCost = basePrice,
            AdultPrice = basePrice,
            ValidatingCarrier = validatingCarrier,
            IsInvalid = false,
            IsVerified = true,
            CheckRules = $"Fare Family: {offers.FirstOrDefault()?.FareFamilyName ?? "Unknown"}",
            TotalApiCostSar = apiCost,
            Offers = offers
        };
    }

    private string ParseErrorsFromXml(string xmlContent)
    {
        try
        {
            var doc = XDocument.Parse(xmlContent);
            var errors = doc.Descendants().Where(e => e.Name.LocalName == "Error");

            var xElements = errors as XElement[] ?? errors.ToArray();
            var userFriendlyMessages = xElements.Select(e =>
            {
                var code = e.Element(e.Name.Namespace + "Code")?.Value ?? "Unknown";
                return code switch
                {
                    "39457" => "The selected flight offer is invalid or has expired. Please search for flights again.",
                    "490" => "Unable to retrieve the flight offer. Please try a different flight option.",
                    _ => "An error occurred while processing the flight offer. Please try again later."
                };
            }).Distinct(); // Remove duplicates

            return string.Join(" ", userFriendlyMessages);
        }
        catch
        {
            return "An unexpected error occurred while processing the flight offer.";
        }
    }

    private static List<FareFamilySection> BuildFareFamilyDescription(
        int checkedBags,
        bool refundable,
        bool changeable,
        string fareFamily)
    {
        return
        [
            new FareFamilySection
            {
                Title = "Baggage Allowance",
                Items =
                [
                    new FareFamilyItem { Text = "Cabin bag: 1 piece(s)", Icon = "CabinBaggage" },
                    checkedBags > 0
                        ? new() { Text = $"Checked bag: {checkedBags} piece(s)", Icon = "CheckedBaggage" }
                        : new() { Text = "No checked baggage", Icon = "Cancel" }
                ]
            },

            new FareFamilySection
            {
                Title = "Refund & Date Change",
                Items =
                [
                    new FareFamilyItem
                    {
                        Text = refundable ? "Refundable" : "Non-refundable", Icon = refundable ? "Refund" : "NoRefund"
                    },

                    new FareFamilyItem
                    {
                        Text = changeable ? "Changeable" : "Not changeable", Icon = changeable ? "Change" : "Cancel"
                    }
                ]
            },

            new FareFamilySection
            {
                Title = "Seat Selection",
                Items =
                [
                    new FareFamilyItem
                    {
                        Text = fareFamily.Contains("PRO") || fareFamily.Contains("FULL")
                            ? "Free seat selection"
                            : "Seat selection with fees",
                        Icon = "SeatSelection"
                    }
                ]
            }
        ];
    }
}