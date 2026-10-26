namespace Backend.DTOs.Chatbot;

public record ChatbotAskRequest(string? Message);

/// <summary>Kind: "search" (vehicles), "ask" (need one more detail), "faq", "unknown".</summary>
public record ChatbotAskResponse(
    string Kind,
    string Reply,
    IReadOnlyList<ChatbotVehicle> Vehicles,
    IReadOnlyList<ChatbotLink> Links,
    ChatbotSearchEcho? Search = null);

public record ChatbotVehicle(
    int VehicleId,
    string Name,
    string TypeName,
    int Seats,
    string LicensePlate,
    int Days,
    decimal TotalAmount);

public record ChatbotLink(string Text, string Url);

/// <summary>What was searched — the web uses it to build the "Đặt xe" link.</summary>
public record ChatbotSearchEcho(DateTime Start, DateTime End, string RentalMode, IReadOnlyList<int> Seats, decimal? MaxPrice);
