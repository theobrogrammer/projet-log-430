using ProjetLog430.Domain.Model.Trading;

namespace ProjetLog430.Domain.Ports.Outbound;

/// <summary>
/// Port sortant vers le moteur d'appariement (Order Matching Engine).
/// Architecture Hexagonale : Service externe qui gère le carnet d'ordres et l'exécution.
/// </summary>
public interface IOrderMatchingPort
{
    /// <summary>
    /// Soumettre un ordre au moteur d'appariement
    /// </summary>
    Task<bool> SubmitOrderAsync(Ordre ordre, CancellationToken ct = default);

    /// <summary>
    /// Annuler un ordre dans le moteur d'appariement
    /// </summary>
    Task<bool> CancelOrderAsync(Guid orderId, CancellationToken ct = default);
}
