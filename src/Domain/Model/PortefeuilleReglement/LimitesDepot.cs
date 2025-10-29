namespace ProjetLog430.Domain.Model.PortefeuilleReglement;

public sealed record LimitesDepot(
    decimal MontantMinimum = 0.01m,
    decimal MontantMaximum = 100_000.00m,
    decimal LimiteQuotidienne = 10_000.00m,
    decimal LimiteMensuelle = 100_000.00m,
    string[] DevisesAcceptees = null!
)
{
    public static LimitesDepot ParDefaut() => new(
        DevisesAcceptees: new[] { "CAD", "USD", "EUR" }
    );

    public void ValiderMontant(decimal montant, string devise)
    {
        if (montant < MontantMinimum)
            throw new InvalidOperationException($"Montant minimum: {MontantMinimum:C}");
        
        if (montant > MontantMaximum)
            throw new InvalidOperationException($"Montant maximum: {MontantMaximum:C}");
        
        if (!DevisesAcceptees.Contains(devise.ToUpperInvariant()))
            throw new InvalidOperationException($"Devise non supportée: {devise}. Acceptées: {string.Join(", ", DevisesAcceptees)}");
    }
}