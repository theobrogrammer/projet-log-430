namespace ProjetLog430.Domain.Model.PortefeuilleReglement;

public enum TypeEcriture { DEPOSIT, TRADE_FILL, FEE, ADJUSTMENT }
public enum TypeReference { PAYMENT_TX, EXECUTION, ORDER, OTHER }

public sealed class EcritureLedger
{
    public Guid LedgerEntryId { get; private set; }
    public Guid AccountId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }
    public TypeEcriture Kind { get; private set; }
    public TypeReference RefType { get; private set; }
    public Guid RefId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // Constructeur privé pour Entity Framework
    private EcritureLedger()
    {
        LedgerEntryId = Guid.NewGuid();
        AccountId = Guid.Empty;
        Amount = 0m;
        Currency = "CAD";
        Kind = TypeEcriture.DEPOSIT;
        RefType = TypeReference.OTHER;
        RefId = Guid.Empty;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    private EcritureLedger(Guid id, Guid accountId, decimal amount, string currency,
                           TypeEcriture kind, TypeReference refType, Guid refId)
    {
        if (amount == 0m) throw new ArgumentOutOfRangeException(nameof(amount), "Montant non nul requis.");
        LedgerEntryId = id;
        AccountId = accountId;
        Amount = amount;
        Currency = ExigerDevise(currency);
        Kind = kind;
        RefType = refType;
        RefId = refId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public static EcritureLedger PourDepot(Guid accountId, decimal amount, string currency, Guid paymentTxId)
        => new(Guid.NewGuid(), accountId, amount, currency, TypeEcriture.DEPOSIT, TypeReference.PAYMENT_TX, paymentTxId);

    public static EcritureLedger PourAjustement(Guid accountId, decimal amount, string currency, string reason)
        => new(Guid.NewGuid(), accountId, amount, currency, TypeEcriture.ADJUSTMENT, TypeReference.OTHER, Guid.NewGuid());

    private static string ExigerDevise(string ccy)
    {
        if (string.IsNullOrWhiteSpace(ccy) || ccy.Length != 3) throw new ArgumentException("Devise ISO-3 requise.", nameof(ccy));
        return ccy.ToUpperInvariant();
    }
}
