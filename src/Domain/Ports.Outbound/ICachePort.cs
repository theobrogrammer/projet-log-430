namespace ProjetLog430.Domain.Ports.Outbound;

/// <summary>
/// Port de sortie pour le cache distribué (Redis).
/// Suit l'architecture hexagonale - abstraction du cache.
/// Phase 2 Étape 2a - Performance optimization.
/// </summary>
public interface ICachePort
{
    /// <summary>
    /// Récupère une valeur du cache.
    /// </summary>
    /// <typeparam name="T">Type de la valeur à récupérer</typeparam>
    /// <param name="key">Clé du cache</param>
    /// <param name="ct">Token d'annulation</param>
    /// <returns>La valeur si trouvée, null sinon</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Stocke une valeur dans le cache avec TTL optionnel.
    /// </summary>
    /// <typeparam name="T">Type de la valeur à stocker</typeparam>
    /// <param name="key">Clé du cache</param>
    /// <param name="value">Valeur à stocker</param>
    /// <param name="ttl">Durée de vie (Time To Live). Si null, pas d'expiration.</param>
    /// <param name="ct">Token d'annulation</param>
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Supprime une clé du cache (invalidation).
    /// </summary>
    /// <param name="key">Clé à supprimer</param>
    /// <param name="ct">Token d'annulation</param>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Vérifie si une clé existe dans le cache.
    /// </summary>
    /// <param name="key">Clé à vérifier</param>
    /// <param name="ct">Token d'annulation</param>
    /// <returns>True si la clé existe, false sinon</returns>
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Supprime plusieurs clés correspondant à un pattern.
    /// Utile pour invalidation en masse (ex: wallet:balance:* après deposit).
    /// </summary>
    /// <param name="pattern">Pattern de clés (ex: "wallet:*")</param>
    /// <param name="ct">Token d'annulation</param>
    Task RemoveByPatternAsync(string pattern, CancellationToken ct = default);
}
