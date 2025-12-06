using ProjetLog430.Domain.Ports.Outbound;
using Serilog;

namespace ProjetLog430.Infrastructure.Adapters.PreTrade;

/// <summary>
/// Adapter pour les contrôles pré-trade (UC-05).
/// Architecture Hexagonale : Implémente IPreTradeCheckPort.
/// Simule les règles de risque, compliance, et limites de trading.
/// </summary>
public sealed class PreTradeCheckAdapter : IPreTradeCheckPort
{
    private readonly IPortfolioRepository _portefeuilles;
    private readonly IMarketFeedPort _marketFeed;

    // Configuration des règles (en production, viendrait d'une DB ou service de configuration)
    private readonly decimal _maxPositionSize = 10000m; // Max 10,000 shares per position
    private readonly decimal _maxNotional = 1_000_000m; // Max $1M notional per order
    private readonly decimal _tickSize = 0.01m; // Minimum price increment
    private readonly HashSet<string> _activeSymbols = new() { "AAPL", "TSLA", "GOOGL", "MSFT", "AMZN", "META", "NVDA", "NFLX", "AMD", "INTC" };

    public PreTradeCheckAdapter(IPortfolioRepository portefeuilles, IMarketFeedPort marketFeed)
    {
        _portefeuilles = portefeuilles;
        _marketFeed = marketFeed;
    }

    public async Task<PreTradeCheckResult> CheckBuyingPowerAsync(
        Guid accountId,
        string symbol,
        decimal quantity,
        decimal? price,
        CancellationToken ct = default)
    {
        Log.Debug("[PRE_TRADE_CHECK] Checking buying power for Account={AccountId}, Symbol={Symbol}, Qty={Quantity}, Price={Price}",
            accountId, symbol, quantity, price);

        // Récupérer le portefeuille
        var portefeuille = await _portefeuilles.GetByAccountIdAsync(accountId, ct);
        if (portefeuille == null)
        {
            return PreTradeCheckResult.Reject("PORTFOLIO_NOT_FOUND", "Portefeuille introuvable");
        }

        // Calculer le coût estimé de l'ordre
        decimal estimatedCost;
        if (price.HasValue)
        {
            // Ordre limite : utiliser le prix spécifié
            estimatedCost = quantity * price.Value;
        }
        else
        {
            // Ordre marché : utiliser le dernier prix connu ou ask
            var quote = await _marketFeed.GetLatestQuoteAsync(symbol, ct);
            if (quote == null)
            {
                return PreTradeCheckResult.Reject("QUOTE_NOT_AVAILABLE", "Cotation non disponible pour estimer le coût");
            }
            estimatedCost = quantity * quote.Ask; // Pour un achat, on utilise le Ask
        }

        // Ajouter une marge de sécurité de 5% pour les ordres marché
        if (!price.HasValue)
        {
            estimatedCost *= 1.05m;
        }

        Log.Debug("[PRE_TRADE_CHECK] EstimatedCost={EstimatedCost}, AvailableBalance={Balance}",
            estimatedCost, portefeuille.SoldeMonnaie);

        // Vérifier le solde disponible
        if (portefeuille.SoldeMonnaie < estimatedCost)
        {
            return PreTradeCheckResult.Reject(
                "INSUFFICIENT_FUNDS",
                $"Fonds insuffisants. Requis: {estimatedCost:F2} {portefeuille.Devise}, Disponible: {portefeuille.SoldeMonnaie:F2} {portefeuille.Devise}");
        }

        Log.Information("[PRE_TRADE_CHECK] Buying power check PASSED for Account={AccountId}", accountId);
        return PreTradeCheckResult.Pass();
    }

    public async Task<PreTradeCheckResult> CheckPriceBandsAsync(
        string symbol,
        decimal? price,
        CancellationToken ct = default)
    {
        if (!price.HasValue)
            return PreTradeCheckResult.Pass(); // Pas de vérification pour les ordres marché

        Log.Debug("[PRE_TRADE_CHECK] Checking price bands for Symbol={Symbol}, Price={Price}", symbol, price);

        // Récupérer la dernière cotation
        var quote = await _marketFeed.GetLatestQuoteAsync(symbol, ct);
        if (quote == null)
        {
            // Si pas de cotation disponible, on accepte (mode dégradé)
            Log.Warning("[PRE_TRADE_CHECK] No quote available for {Symbol}, skipping price band check", symbol);
            return PreTradeCheckResult.Pass();
        }

        // Vérifier le tick size (incrément de prix minimum)
        var remainder = price.Value % _tickSize;
        if (remainder != 0)
        {
            return PreTradeCheckResult.Reject(
                "INVALID_TICK_SIZE",
                $"Le prix doit être un multiple de {_tickSize:F2}");
        }

        // Bandes de prix : +/- 10% du dernier prix
        var lastPrice = quote.Last;
        var lowerBand = lastPrice * 0.90m;
        var upperBand = lastPrice * 1.10m;

        if (price.Value < lowerBand || price.Value > upperBand)
        {
            return PreTradeCheckResult.Reject(
                "PRICE_OUT_OF_BANDS",
                $"Prix hors des bandes autorisées [{lowerBand:F2} - {upperBand:F2}]. Dernier prix: {lastPrice:F2}");
        }

        Log.Information("[PRE_TRADE_CHECK] Price bands check PASSED for Symbol={Symbol}, Price={Price}", symbol, price);
        return PreTradeCheckResult.Pass();
    }

    public Task<PreTradeCheckResult> CheckTradingLimitsAsync(
        Guid accountId,
        decimal notional,
        CancellationToken ct = default)
    {
        Log.Debug("[PRE_TRADE_CHECK] Checking trading limits for Account={AccountId}, Notional={Notional}",
            accountId, notional);

        // Vérifier le notional maximum par ordre
        if (notional > _maxNotional)
        {
            return Task.FromResult(PreTradeCheckResult.Reject(
                "MAX_NOTIONAL_EXCEEDED",
                $"Notional de l'ordre dépasse la limite maximum: {_maxNotional:N0} $"));
        }

        // TODO: Vérifier d'autres limites (nombre d'ordres par jour, position maximale, etc.)

        Log.Information("[PRE_TRADE_CHECK] Trading limits check PASSED for Account={AccountId}", accountId);
        return Task.FromResult(PreTradeCheckResult.Pass());
    }

    public async Task<PreTradeCheckResult> CheckShortSellAsync(
        Guid accountId,
        string symbol,
        CancellationToken ct = default)
    {
        Log.Debug("[PRE_TRADE_CHECK] Checking short-sell for Account={AccountId}, Symbol={Symbol}",
            accountId, symbol);

        // Récupérer le portefeuille pour vérifier qu'il existe
        var portefeuille = await _portefeuilles.GetByAccountIdAsync(accountId, ct);
        if (portefeuille == null)
        {
            return PreTradeCheckResult.Reject("PORTFOLIO_NOT_FOUND", "Portefeuille introuvable");
        }

        // NOTE: Dans une implémentation complète, on vérifierait les positions via un PositionRepository.
        // Pour cette version simplifiée, on accepte les ventes (assume le client a les actions).
        // En production, il faudrait:
        // 1. Avoir un IPositionRepository pour vérifier les positions réelles
        // 2. Interdire la vente si la position est insuffisante
        Log.Warning("[PRE_TRADE_CHECK] Short-sell check SKIPPED (simplified implementation) for Account={AccountId}, Symbol={Symbol}", accountId, symbol);
        return PreTradeCheckResult.Pass();
    }

    public Task<PreTradeCheckResult> CheckInstrumentStatusAsync(
        string symbol,
        CancellationToken ct = default)
    {
        Log.Debug("[PRE_TRADE_CHECK] Checking instrument status for Symbol={Symbol}", symbol);

        // Vérifier si le symbole est dans la liste des instruments actifs
        if (!_activeSymbols.Contains(symbol.ToUpperInvariant()))
        {
            return Task.FromResult(PreTradeCheckResult.Reject(
                "INSTRUMENT_NOT_ACTIVE",
                $"L'instrument {symbol} n'est pas disponible pour le trading"));
        }

        // TODO: Vérifier d'autres critères (halted, suspended, etc.)

        Log.Information("[PRE_TRADE_CHECK] Instrument status check PASSED for Symbol={Symbol}", symbol);
        return Task.FromResult(PreTradeCheckResult.Pass());
    }
}
