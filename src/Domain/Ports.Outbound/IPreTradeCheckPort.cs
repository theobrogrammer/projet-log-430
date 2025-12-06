namespace ProjetLog430.Domain.Ports.Outbound;

/// <summary>
/// Port sortant pour les contrôles pré-trade (UC-05).
/// Architecture Hexagonale : Service métier externe (règles, risque, compliance).
/// </summary>
public interface IPreTradeCheckPort
{
    /// <summary>
    /// Vérifier le pouvoir d'achat pour un ordre d'achat
    /// </summary>
    Task<PreTradeCheckResult> CheckBuyingPowerAsync(
        Guid accountId,
        string symbol,
        decimal quantity,
        decimal? price,
        CancellationToken ct = default);

    /// <summary>
    /// Vérifier les bandes de prix (price bands)
    /// </summary>
    Task<PreTradeCheckResult> CheckPriceBandsAsync(
        string symbol,
        decimal? price,
        CancellationToken ct = default);

    /// <summary>
    /// Vérifier les limites de trading par utilisateur
    /// </summary>
    Task<PreTradeCheckResult> CheckTradingLimitsAsync(
        Guid accountId,
        decimal notional,
        CancellationToken ct = default);

    /// <summary>
    /// Vérifier si le short-sell est autorisé
    /// </summary>
    Task<PreTradeCheckResult> CheckShortSellAsync(
        Guid accountId,
        string symbol,
        CancellationToken ct = default);

    /// <summary>
    /// Vérifier si l'instrument est actif et tradable
    /// </summary>
    Task<PreTradeCheckResult> CheckInstrumentStatusAsync(
        string symbol,
        CancellationToken ct = default);
}

/// <summary>
/// Result des contrôles pré-trade
/// </summary>
public sealed class PreTradeCheckResult
{
    public bool Passed { get; init; }
    public string? RejectReason { get; init; }
    public string? ErrorCode { get; init; }

    public static PreTradeCheckResult Pass() => new() { Passed = true };
    
    public static PreTradeCheckResult Reject(string errorCode, string reason) => new()
    {
        Passed = false,
        ErrorCode = errorCode,
        RejectReason = reason
    };
}
