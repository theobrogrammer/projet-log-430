using ProjetLog430.Domain.Ports.Outbound;
using ProjetLog430.Infrastructure.Web.Hubs;

namespace ProjetLog430.Infrastructure.Web.SignalR;

/// <summary>
/// Adapter pour les statistiques du Hub SignalR.
/// Architecture Hexagonale : 
/// - Implémente ISubscriptionStatsPort (Port Outbound)
/// - Accède aux statistiques de MarketDataHub
/// </summary>
public sealed class MarketDataHubStatsAdapter : ISubscriptionStatsPort
{
    public int GetSubscriberCount(string symbol)
    {
        return MarketDataHub.GetSubscriptionCount(symbol);
    }

    public List<string> GetActiveSymbols()
    {
        return MarketDataHub.GetActiveSymbols();
    }
}
