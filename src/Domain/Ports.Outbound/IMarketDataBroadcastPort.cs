using ProjetLog430.Domain.Model.MarketData;

namespace ProjetLog430.Domain.Ports.Outbound;

/// <summary>
/// Port de sortie pour diffuser les cotations en temps réel aux clients connectés.
/// Architecture Hexagonale : permet de découpler la logique métier de la technologie de broadcast (SignalR, WebSockets, etc.)
/// </summary>
public interface IMarketDataBroadcastPort
{
    /// <summary>
    /// Diffuse une cotation à tous les clients abonnés au symbole
    /// </summary>
    Task BroadcastQuoteAsync(Quote quote, CancellationToken ct = default);

    /// <summary>
    /// Obtient le nombre d'abonnés actifs pour un symbole donné
    /// </summary>
    int GetSubscriberCount(string symbol);

    /// <summary>
    /// Obtient la liste de tous les symboles ayant des abonnés actifs
    /// </summary>
    List<string> GetActiveSymbols();
}
