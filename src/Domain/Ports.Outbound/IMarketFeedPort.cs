namespace ProjetLog430.Domain.Ports.Outbound;

using ProjetLog430.Domain.Model.MarketData;

/// <summary>
/// Port pour simuler un flux de données de marché externe (UC-04)
/// Génère des cotations en temps réel
/// </summary>
public interface IMarketFeedPort
{
    /// <summary>
    /// Démarrer le flux de données global
    /// </summary>
    Task StartFeedAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Arrêter le flux de données
    /// </summary>
    Task StopFeedAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Obtenir la dernière cotation pour un symbole
    /// </summary>
    Task<Quote?> GetLatestQuoteAsync(string symbol, CancellationToken ct = default);
    
    /// <summary>
    /// Obtenir l'historique des cotations pour un symbole
    /// </summary>
    Task<List<Quote>> GetQuoteHistoryAsync(string symbol, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
    
    /// <summary>
    /// Obtenir la liste des symboles disponibles
    /// </summary>
    List<string> GetAvailableSymbols();
}
