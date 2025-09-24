namespace ProjetLog430.Domain.Model.Identite;

public enum StatutCompte { Active, Suspended, Closed }

public sealed class Compte
{
    public Guid AccountId { get; }
    public Guid ClientId { get; }
    public string AccountNo { get; }
    public StatutCompte Statut { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Compte(Guid id, Guid clientId, string accountNo)
    {
        AccountId = id;
        ClientId = clientId;
        AccountNo = accountNo;
        Statut = StatutCompte.Active;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    internal static Compte Ouvrir(Client proprietaire)
        => new(Guid.NewGuid(), proprietaire.ClientId, GenererAccountNo());

    public void Suspendre(string raison)
    {
        if (Statut != StatutCompte.Active) throw new InvalidOperationException("Seuls les comptes actifs peuvent être suspendus.");
        Statut = StatutCompte.Suspended;
        Touch();
    }

    public void Reprendre()
    {
        if (Statut != StatutCompte.Suspended) throw new InvalidOperationException("Seuls les comptes suspendus peuvent reprendre.");
        Statut = StatutCompte.Active;
        Touch();
    }

    public void Fermer()
    {
        if (Statut == StatutCompte.Closed) return;
        Statut = StatutCompte.Closed;
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    private static string GenererAccountNo()
        => $"BX-{DateTimeOffset.UtcNow:yyyyMMdd}-{Random.Shared.Next(100000, 999999)}";
}
