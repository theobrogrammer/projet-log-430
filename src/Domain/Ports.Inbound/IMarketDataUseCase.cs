namespace ProjetLog430.Domain.Ports.Inbound;

using ProjetLog430.Domain.Contracts;
using ProjetLog430.Domain.Model.MarketData;

/// <summary>
/// UC-04 : Abonnement aux données de marché — streaming temps réel via WebSocket/SSE
/// Architecture hexagonale : les méthodes retournent des Result, pas d'exceptions
/// </summary>
public interface IMarketDataUseCase
{
    /// <summary>
    /// Créer un abonnement aux données de marché pour des symboles spécifiques
    /// </summary>
    Task<MarketDataOperationResult> SubscribeAsync(
        Guid clientId, 
        List<string> symbols, 
        CanalStreaming canal = CanalStreaming.WebSocket,
        CancellationToken ct = default);

    /// <summary>
    /// Ajouter des symboles à un abonnement existant
    /// </summary>
    Task<MarketDataOperationResult> AddSymbolsAsync(
        Guid subscriptionId, 
        List<string> symbols, 
        CancellationToken ct = default);

    /// <summary>
    /// Retirer des symboles d'un abonnement
    /// </summary>
    Task<MarketDataOperationResult> RemoveSymbolsAsync(
        Guid subscriptionId, 
        List<string> symbols, 
        CancellationToken ct = default);

    /// <summary>
    /// Annuler un abonnement
    /// </summary>
    Task<UnsubscribeResult> UnsubscribeAsync(
        Guid subscriptionId, 
        CancellationToken ct = default);

    /// <summary>
    /// Obtenir les dernières cotations pour des symboles
    /// </summary>
    Task<QuotesResult> GetLatestQuotesAsync(
        List<string> symbols, 
        CancellationToken ct = default);
}
