using ProjetLog430.Domain.Model.MarketData;
using ProjetLog430.Domain.Ports.Outbound;
using Serilog;

namespace ProjetLog430.Infrastructure.Adapters.SignalR;

/// <summary>
/// Adapter SignalR pour diffuser les cotations en temps réel.
/// Architecture Hexagonale : 
/// - Implémente IMarketDataBroadcastPort (Port Outbound)
/// - Encapsule la technologie SignalR via des abstractions
/// - Peut être remplacé par WebSockets, gRPC, etc. sans impact sur le Domain/Application
/// </summary>
public sealed class SignalRBroadcastAdapter : IMarketDataBroadcastPort
{
    private readonly ISignalRHubContextWrapper _hubContext;
    private readonly ISubscriptionStatsPort _stats;

    public SignalRBroadcastAdapter(ISignalRHubContextWrapper hubContext, ISubscriptionStatsPort stats)
    {
        _hubContext = hubContext;
        _stats = stats;
    }

    public async Task BroadcastQuoteAsync(Quote quote, CancellationToken ct = default)
    {
        try
        {
            // Diffuser à tous les clients abonnés au groupe du symbole
            await _hubContext.SendToGroupAsync(quote.Symbol, "ReceiveQuote", new
            {
                symbol = quote.Symbol,
                bid = quote.Bid,
                ask = quote.Ask,
                last = quote.Last,
                volume = quote.Volume,
                spread = quote.ObtenirSpread(),
                spreadBps = quote.ObtenirSpreadBps(),
                timestamp = quote.Timestamp
            }, ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[UC04_SIGNALR_ADAPTER] Error broadcasting quote for {Symbol}", quote.Symbol);
            throw;
        }
    }

    public int GetSubscriberCount(string symbol)
    {
        return _stats.GetSubscriberCount(symbol);
    }

    public List<string> GetActiveSymbols()
    {
        return _stats.GetActiveSymbols();
    }
}
