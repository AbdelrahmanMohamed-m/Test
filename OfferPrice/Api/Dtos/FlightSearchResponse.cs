namespace Domain.Models.JsonModels; 
    public class RecommendedFlight
    {
        public string Id { get; set; }
        public bool IsNdc { get; set; }
    
    }
    public class Root
    {
        public string FlightRequestSessionId { get; set; }
        public string FlightSearchSessionId { get; set; }
        public List<RecommendedFlight> RecommendedFlights { get; set; }
        public List<string> AllAirlines { get; set; }
        public List<string> AllWaitingAirports { get; set; }
        public bool IsMultiCity { get; set; }
    }

