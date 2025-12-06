namespace ProjetLog430.Domain.Model.Trading;

/// <summary>
/// Agrégat Ordre - UC-05 : Placement d'un ordre (marché/limite) avec contrôles pré-trade.
/// Représente un ordre d'achat ou de vente soumis par un client.
/// </summary>
public sealed class Ordre
{
    public Guid OrderId { get; private set; }
    public Guid AccountId { get; private set; }
    public string ClientOrderId { get; private set; } = string.Empty; // Pour idempotence
    public string Symbol { get; private set; } = string.Empty;
    public SensOrdre Side { get; private set; }
    public TypeOrdre Type { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal? Price { get; private set; } // Null pour ordre marché
    public DureeValidite TimeInForce { get; private set; }
    public StatutOrdre Statut { get; private set; }
    public decimal FilledQuantity { get; private set; }
    public decimal? AvgPrice { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public string? RejectReason { get; private set; }

    // Navigation properties
    public List<Execution> Executions { get; private set; } = new();

    private Ordre() { } // EF Core

    /// <summary>
    /// Factory Method : Créer un nouvel ordre après validation pré-trade
    /// </summary>
    public static Ordre Creer(
        Guid accountId,
        string clientOrderId,
        string symbol,
        SensOrdre side,
        TypeOrdre type,
        decimal quantity,
        decimal? price,
        DureeValidite timeInForce)
    {
        // Validations métier
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Le symbole est requis", nameof(symbol));
        
        if (string.IsNullOrWhiteSpace(clientOrderId))
            throw new ArgumentException("ClientOrderId est requis pour l'idempotence", nameof(clientOrderId));

        if (quantity <= 0)
            throw new ArgumentException("La quantité doit être positive", nameof(quantity));

        if (type == TypeOrdre.Limit && (!price.HasValue || price.Value <= 0))
            throw new ArgumentException("Le prix est requis pour un ordre limite", nameof(price));

        var ordre = new Ordre
        {
            OrderId = Guid.NewGuid(),
            AccountId = accountId,
            ClientOrderId = clientOrderId,
            Symbol = symbol.ToUpperInvariant(),
            Side = side,
            Type = type,
            Quantity = quantity,
            Price = price,
            TimeInForce = timeInForce,
            Statut = StatutOrdre.New,
            FilledQuantity = 0,
            CreatedAt = DateTime.UtcNow
        };

        return ordre;
    }

    /// <summary>
    /// Accepter l'ordre après validation pré-trade
    /// </summary>
    public void Accepter()
    {
        if (Statut != StatutOrdre.New && Statut != StatutOrdre.Validating)
            throw new InvalidOperationException($"Cannot accept order in status {Statut}");

        Statut = StatutOrdre.Accepted;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Placer l'ordre dans le carnet (order book)
    /// </summary>
    public void PlacerDansCarnet()
    {
        if (Statut != StatutOrdre.Accepted)
            throw new InvalidOperationException($"Cannot place order in status {Statut}");

        Statut = StatutOrdre.Working;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Rejeter l'ordre avec une raison
    /// </summary>
    public void Rejeter(string reason)
    {
        if (Statut == StatutOrdre.Filled || Statut == StatutOrdre.Cancelled)
            throw new InvalidOperationException($"Cannot reject order in status {Statut}");

        Statut = StatutOrdre.Rejected;
        RejectReason = reason;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Exécuter partiellement l'ordre
    /// </summary>
    public Execution ExecuterPartiellement(decimal execQuantity, decimal execPrice, decimal commission)
    {
        if (Statut != StatutOrdre.Working && Statut != StatutOrdre.PartiallyFilled)
            throw new InvalidOperationException($"Cannot execute order in status {Statut}");

        if (execQuantity <= 0 || execQuantity > (Quantity - FilledQuantity))
            throw new ArgumentException("Invalid execution quantity", nameof(execQuantity));

        var execution = Execution.Creer(OrderId, execQuantity, execPrice, commission);
        Executions.Add(execution);

        FilledQuantity += execQuantity;
        
        // Calcul du prix moyen pondéré
        var totalValue = Executions.Sum(e => e.ExecQuantity * e.ExecPrice);
        AvgPrice = totalValue / FilledQuantity;

        Statut = FilledQuantity >= Quantity ? StatutOrdre.Filled : StatutOrdre.PartiallyFilled;
        UpdatedAt = DateTime.UtcNow;

        return execution;
    }

    /// <summary>
    /// Annuler l'ordre
    /// </summary>
    public void Annuler()
    {
        if (Statut == StatutOrdre.Filled)
            throw new InvalidOperationException("Cannot cancel filled order");

        if (Statut == StatutOrdre.Rejected)
            throw new InvalidOperationException("Cannot cancel rejected order");

        Statut = StatutOrdre.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Vérifier si l'ordre est terminal (ne peut plus être modifié)
    /// </summary>
    public bool EstTerminal() => Statut is StatutOrdre.Filled or StatutOrdre.Rejected or StatutOrdre.Cancelled or StatutOrdre.Expired;

    /// <summary>
    /// Obtenir la quantité restante à exécuter
    /// </summary>
    public decimal ObtenirQuantiteRestante() => Quantity - FilledQuantity;
}

/// <summary>
/// Value Object : Exécution d'un ordre (fill)
/// </summary>
public sealed class Execution
{
    public Guid ExecutionId { get; private set; }
    public Guid OrderId { get; private set; }
    public decimal ExecQuantity { get; private set; }
    public decimal ExecPrice { get; private set; }
    public decimal Commission { get; private set; }
    public DateTime Timestamp { get; private set; }

    private Execution() { } // EF Core

    internal static Execution Creer(Guid orderId, decimal execQuantity, decimal execPrice, decimal commission)
    {
        return new Execution
        {
            ExecutionId = Guid.NewGuid(),
            OrderId = orderId,
            ExecQuantity = execQuantity,
            ExecPrice = execPrice,
            Commission = commission,
            Timestamp = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Enum : Sens de l'ordre (Achat/Vente)
/// </summary>
public enum SensOrdre
{
    Buy,  // Achat
    Sell  // Vente
}

/// <summary>
/// Enum : Type d'ordre
/// </summary>
public enum TypeOrdre
{
    Market, // Au marché (exécution immédiate au meilleur prix)
    Limit   // Limite (exécution au prix spécifié ou meilleur)
}

/// <summary>
/// Enum : Durée de validité de l'ordre
/// </summary>
public enum DureeValidite
{
    Day,  // Valide jusqu'à la fin de la journée
    IOC,  // Immediate or Cancel (exécution immédiate, annulation du reste)
    FOK,  // Fill or Kill (exécution complète ou annulation totale)
    GTC   // Good Till Cancelled (valide jusqu'à annulation)
}

/// <summary>
/// Enum : Statut de l'ordre
/// </summary>
public enum StatutOrdre
{
    New,              // Nouvel ordre créé
    Validating,       // En cours de validation pré-trade
    Accepted,         // Accepté par les contrôles pré-trade
    Working,          // Placé dans le carnet d'ordres
    PartiallyFilled,  // Partiellement exécuté
    Filled,           // Complètement exécuté
    Cancelled,        // Annulé par le client
    Rejected,         // Rejeté par les contrôles
    Expired           // Expiré (durée de validité dépassée)
}
