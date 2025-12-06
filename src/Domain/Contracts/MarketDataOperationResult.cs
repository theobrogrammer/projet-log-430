namespace ProjetLog430.Domain.Contracts;

/// <summary>
/// Result générique pour les opérations de données de marché
/// Pattern hexagonal : pas d'exceptions dans les use cases
/// </summary>
public sealed record MarketDataOperationResult
{
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public MarketDataResult? Data { get; init; }

    private MarketDataOperationResult() { }

    public static MarketDataOperationResult Ok(MarketDataResult data) => new()
    {
        Success = true,
        Data = data
    };

    public static MarketDataOperationResult Fail(string errorCode, string errorMessage) => new()
    {
        Success = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };
}

/// <summary>
/// Result pour la récupération de cotations
/// </summary>
public sealed record QuotesResult
{
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public List<ProjetLog430.Domain.Model.MarketData.Quote>? Quotes { get; init; }

    private QuotesResult() { }

    public static QuotesResult Ok(List<ProjetLog430.Domain.Model.MarketData.Quote> quotes) => new()
    {
        Success = true,
        Quotes = quotes
    };

    public static QuotesResult Fail(string errorCode, string errorMessage) => new()
    {
        Success = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
        Quotes = new List<ProjetLog430.Domain.Model.MarketData.Quote>()
    };
}

/// <summary>
/// Result pour l'annulation d'abonnement
/// </summary>
public sealed record UnsubscribeResult
{
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    private UnsubscribeResult() { }

    public static UnsubscribeResult Ok() => new()
    {
        Success = true
    };

    public static UnsubscribeResult Fail(string errorCode, string errorMessage) => new()
    {
        Success = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };
}
