namespace ProjetLog430.Domain.Contracts;

/// <summary>
/// Result of UC-04: market data subscription (WebSocket/SSE)
/// </summary>
public sealed record MarketDataResult(
    Guid SubscriptionId,
    List<string> Symbols,
    string Canal, // "WebSocket" | "SSE"
    string Statut // "Active" | "Suspended" | "Cancelled"
);
