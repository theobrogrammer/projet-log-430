namespace ProjetLog430.Domain.Ports.Outbound;

/// <summary>
/// Port de sortie pour obtenir des statistiques sur les abonnements clients.
/// Architecture Hexagonale : permet de découpler la logique des statistiques
/// </summary>
public interface ISubscriptionStatsPort
{
    /// <summary>
    /// Obtient le nombre d'abonnés actifs pour un symbole donné
    /// </summary>
    int GetSubscriberCount(string symbol);

    /// <summary>
    /// Obtient la liste de tous les symboles ayant des abonnés actifs
    /// </summary>
    List<string> GetActiveSymbols();
}
