namespace ProjetLog430.Domain.Model.PortefeuilleReglement;

public enum StatutTransaction { Pending, Settled, Failed }

public sealed class TransactionPaiement
{
    public Guid PaymentTxId { get; private set; }
    public Guid AccountId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }
    public string IdempotencyKey { get; private set; }
    public StatutTransaction Statut { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SettledAt { get; private set; }
    public string? FailureReason { get; private set; }

    // Constructeur privé pour Entity Framework
    private TransactionPaiement()
    {
        PaymentTxId = Guid.NewGuid();
        AccountId = Guid.Empty;
        Amount = 0m;
        Currency = "CAD";
        IdempotencyKey = string.Empty;
        Statut = StatutTransaction.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    private TransactionPaiement(Guid id, Guid accountId, decimal amount, string currency, string idempotencyKey)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        PaymentTxId   = id;
        AccountId     = accountId;
        Amount        = amount;
        Currency      = ExigerDevise(currency);
        IdempotencyKey= ExigerNonVide(idempotencyKey, nameof(idempotencyKey));
        Statut        = StatutTransaction.Pending;
        CreatedAt     = DateTimeOffset.UtcNow;
    }

    public static TransactionPaiement Creer(Guid accountId, decimal amount, string currency, string idempotencyKey)
        => new(Guid.NewGuid(), accountId, amount, currency, idempotencyKey);

    public void MarquerReglee()
    {
        if (Statut == StatutTransaction.Settled) return; // idempotent
        if (Statut == StatutTransaction.Failed) throw new InvalidOperationException("Transaction déjà échouée.");
        Statut = StatutTransaction.Settled;
        SettledAt = DateTimeOffset.UtcNow;
    }

    public void MarquerEchoue(string raison)
    {
        if (Statut == StatutTransaction.Settled) throw new InvalidOperationException("Transaction déjà réglée.");
        Statut = StatutTransaction.Failed;
        FailureReason = string.IsNullOrWhiteSpace(raison) ? "unknown" : raison;
    }

    private static string ExigerDevise(string ccy)
    {
        if (string.IsNullOrWhiteSpace(ccy) || ccy.Length != 3) throw new ArgumentException("Devise ISO-3 requise.", nameof(ccy));
        return ccy.ToUpperInvariant();
    }

    private static string ExigerNonVide(string v, string nom)
    {
        if (string.IsNullOrWhiteSpace(v)) throw new ArgumentException($"{nom} requis.", nom);
        return v.Trim();
    }
}
