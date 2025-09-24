namespace ProjetLog430.Domain.Model.PortefeuilleReglement;

public sealed class Portefeuille
{
    public Guid Id { get; }
    public Guid AccountId { get; }
    public string Devise { get; }
    public decimal SoldeMonnaie { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Portefeuille(Guid id, Guid accountId, string devise, decimal solde)
    {
        Id = id;
        AccountId = accountId;
        Devise = ExigerDevise(devise);
        SoldeMonnaie = solde;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static Portefeuille Ouvrir(Guid accountId, string devise, decimal soldeInitial = 0m)
        => new(Guid.NewGuid(), accountId, devise, soldeInitial);

    public void Crediter(decimal montant, string devise)
    {
        VerifierDevise(devise);
        if (montant <= 0) throw new ArgumentOutOfRangeException(nameof(montant));
        SoldeMonnaie += montant;
        Touch();
    }

    public void Debiter(decimal montant, string devise)
    {
        VerifierDevise(devise);
        if (montant <= 0) throw new ArgumentOutOfRangeException(nameof(montant));
        if (SoldeMonnaie < montant) throw new InvalidOperationException("Solde insuffisant.");
        SoldeMonnaie -= montant;
        Touch();
    }

    private void VerifierDevise(string devise)
    {
        if (!string.Equals(Devise, devise, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Devise inattendue. Portefeuille={Devise}, demande={devise}");
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    private static string ExigerDevise(string ccy)
    {
        if (string.IsNullOrWhiteSpace(ccy) || ccy.Length != 3) throw new ArgumentException("Devise ISO-3 requise.", nameof(ccy));
        return ccy.ToUpperInvariant();
    }
}
