namespace ProjetLog430.Domain.Model.MarketData;

/// <summary>
/// Abonnement client aux données de marché (UC-04)
/// Selon MDD: subscriptionId, symbols[], canal, statut, createdAt
/// </summary>
public sealed class Subscription
{
    public Guid SubscriptionId { get; private set; }
    public Guid ClientId { get; private set; }
    public List<string> Symbols { get; private set; } = new();
    public CanalStreaming Canal { get; private set; }
    public StatutSubscription Statut { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // Constructeur privé pour EF Core
    private Subscription()
    {
        SubscriptionId = Guid.NewGuid();
        Canal = CanalStreaming.WebSocket;
        Statut = StatutSubscription.Active;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    private Subscription(Guid subscriptionId, Guid clientId, List<string> symbols, CanalStreaming canal)
    {
        SubscriptionId = subscriptionId;
        ClientId = clientId;
        Symbols = symbols?.Select(s => s.ToUpper()).Distinct().ToList() ?? new List<string>();
        Canal = canal;
        Statut = StatutSubscription.Active;
        CreatedAt = DateTimeOffset.UtcNow;

        if (Symbols.Count == 0)
            throw new InvalidOperationException("Au moins un symbole est requis pour l'abonnement");
    }

    public static Subscription Creer(Guid clientId, List<string> symbols, CanalStreaming canal = CanalStreaming.WebSocket)
        => new(Guid.NewGuid(), clientId, symbols, canal);

    public void AjouterSymbole(string symbol)
    {
        var symbolUpper = symbol?.ToUpper() ?? throw new ArgumentNullException(nameof(symbol));
        if (!Symbols.Contains(symbolUpper))
        {
            Symbols.Add(symbolUpper);
        }
    }

    public void RetirerSymbole(string symbol)
    {
        var symbolUpper = symbol?.ToUpper();
        Symbols.RemoveAll(s => s.Equals(symbolUpper, StringComparison.OrdinalIgnoreCase));
        
        if (Symbols.Count == 0)
            throw new InvalidOperationException("Impossible de retirer le dernier symbole");
    }

    public bool EstAbonneA(string symbol)
    {
        var symbolUpper = symbol?.ToUpper() ?? string.Empty;
        return Symbols.Any(s => s.Equals(symbolUpper, StringComparison.OrdinalIgnoreCase));
    }

    public void Suspendre()
    {
        if (Statut == StatutSubscription.Cancelled)
            throw new InvalidOperationException("Cannot suspend a cancelled subscription");
        Statut = StatutSubscription.Suspended;
    }

    public void Reactiver()
    {
        if (Statut == StatutSubscription.Cancelled)
            throw new InvalidOperationException("Cannot reactivate a cancelled subscription");
        Statut = StatutSubscription.Active;
    }

    public void Annuler()
    {
        Statut = StatutSubscription.Cancelled;
    }
}

public enum CanalStreaming
{
    WebSocket,
    SSE
}

public enum StatutSubscription
{
    Active,
    Suspended,
    Cancelled
}
