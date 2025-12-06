using ProjetLog430.Domain.Contracts;

namespace ProjetLog430.Domain.Ports.Inbound;

/// <summary>
/// Port entrant pour UC-05 : Placement d'ordres.
/// Architecture Hexagonale : Interface exposée par le Domain, implémentée par Application.
/// Définit les opérations métier disponibles pour placer et gérer des ordres.
/// </summary>
public interface IOrderUseCase
{
    /// <summary>
    /// UC-05 : Placer un ordre d'achat ou de vente (marché/limite).
    /// Effectue les contrôles pré-trade et place l'ordre dans le carnet si validé.
    /// </summary>
    /// <param name="accountId">ID du compte</param>
    /// <param name="clientOrderId">ID client pour idempotence</param>
    /// <param name="symbol">Symbole de l'instrument (ex: AAPL, TSLA)</param>
    /// <param name="side">Sens (Buy/Sell)</param>
    /// <param name="type">Type (Market/Limit)</param>
    /// <param name="quantity">Quantité</param>
    /// <param name="price">Prix (requis si Limit, null si Market)</param>
    /// <param name="timeInForce">Durée de validité (DAY/IOC/FOK/GTC)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result avec OrderId si succès, ErrorCode si échec</returns>
    Task<OrderOperationResult> PlaceOrderAsync(
        Guid accountId,
        string clientOrderId,
        string symbol,
        string side,
        string type,
        decimal quantity,
        decimal? price,
        string timeInForce,
        CancellationToken ct = default);

    /// <summary>
    /// Récupérer les informations d'un ordre
    /// </summary>
    Task<OrderQueryResult> GetOrderAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// Récupérer tous les ordres d'un compte
    /// </summary>
    Task<List<OrderDto>> GetOrdersByAccountAsync(Guid accountId, CancellationToken ct = default);

    /// <summary>
    /// Annuler un ordre
    /// </summary>
    Task<OrderOperationResult> CancelOrderAsync(Guid orderId, CancellationToken ct = default);
}
