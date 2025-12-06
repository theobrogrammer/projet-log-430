namespace ProjetLog430.Domain.Model.MarketData;

/// <summary>
/// Quote de marché en temps réel (UC-04)
/// Selon MDD: quoteId, symbol, bid, ask, last, volume, timestamp
/// </summary>
public sealed class Quote
{
    public Guid QuoteId { get; private set; }
    public string Symbol { get; private set; } = string.Empty;
    public decimal Bid { get; private set; }
    public decimal Ask { get; private set; }
    public decimal Last { get; private set; }
    public decimal Volume { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }

    // Constructeur privé pour EF Core
    private Quote()
    {
        QuoteId = Guid.NewGuid();
        Symbol = string.Empty;
        Timestamp = DateTimeOffset.UtcNow;
    }

    private Quote(Guid quoteId, string symbol, decimal bid, decimal ask, decimal last, decimal volume)
    {
        QuoteId = quoteId;
        Symbol = ExigerNonVide(symbol, nameof(symbol)).ToUpper();
        Bid = ExigerPositif(bid, nameof(bid));
        Ask = ExigerPositif(ask, nameof(ask));
        Last = ExigerPositif(last, nameof(last));
        Volume = volume >= 0 ? volume : 0;
        Timestamp = DateTimeOffset.UtcNow;
        
        if (Ask < Bid)
            throw new InvalidOperationException($"Ask ({Ask}) doit être >= Bid ({Bid})");
    }

    public static Quote Creer(string symbol, decimal bid, decimal ask, decimal last, decimal volume = 0)
        => new(Guid.NewGuid(), symbol, bid, ask, last, volume);

    public void MettreAJourPrix(decimal bid, decimal ask, decimal last, decimal volume = 0)
    {
        Bid = ExigerPositif(bid, nameof(bid));
        Ask = ExigerPositif(ask, nameof(ask));
        Last = ExigerPositif(last, nameof(last));
        Volume = volume >= 0 ? volume : Volume;
        Timestamp = DateTimeOffset.UtcNow;
        
        if (Ask < Bid)
            throw new InvalidOperationException($"Ask ({Ask}) doit être >= Bid ({Bid})");
    }

    public decimal ObtenirSpread() => Ask - Bid;
    public decimal ObtenirSpreadBps() => Bid > 0 ? (ObtenirSpread() / Bid) * 10000 : 0;

    private static string ExigerNonVide(string valeur, string nomParam)
    {
        if (string.IsNullOrWhiteSpace(valeur))
            throw new ArgumentException($"{nomParam} ne peut pas être vide.", nomParam);
        return valeur;
    }

    private static decimal ExigerPositif(decimal valeur, string nomParam)
    {
        if (valeur < 0)
            throw new ArgumentException($"{nomParam} doit être >= 0.", nomParam);
        return valeur;
    }
}
