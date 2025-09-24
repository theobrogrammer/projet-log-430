namespace ProjetLog430.Domain.Model.Securite;

public enum StatutDefi { Pending, Passed, Failed, Expired }

public sealed class DefiMFA
{
    public Guid ChallengeId { get; }
    public Guid ClientId { get; }
    public TypeMfa Type { get; }
    public StatutDefi Statut { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset ExpiresAt { get; }
    public DateTimeOffset? CompletedAt { get; private set; }

    private DefiMFA(Guid id, Guid clientId, TypeMfa type, DateTimeOffset expiresAt)
    {
        ChallengeId = id;
        ClientId = clientId;
        Type = type;
        CreatedAt = DateTimeOffset.UtcNow;
        ExpiresAt = expiresAt;
        Statut = StatutDefi.Pending;
    }

    public static DefiMFA Demarrer(Guid clientId, TypeMfa type, TimeSpan ttl)
        => new(Guid.NewGuid(), clientId, type, DateTimeOffset.UtcNow.Add(ttl));

    public void Reussir()
    {
        if (Statut != StatutDefi.Pending) throw new InvalidOperationException("Défi non en attente.");
        if (DateTimeOffset.UtcNow > ExpiresAt) { Statut = StatutDefi.Expired; throw new InvalidOperationException("Défi expiré."); }
        Statut = StatutDefi.Passed;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void Echouer()
    {
        if (Statut != StatutDefi.Pending) throw new InvalidOperationException("Défi non en attente.");
        Statut = StatutDefi.Failed;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void Expirer()
    {
        if (Statut is StatutDefi.Passed or StatutDefi.Failed) return;
        Statut = StatutDefi.Expired;
    }
}
