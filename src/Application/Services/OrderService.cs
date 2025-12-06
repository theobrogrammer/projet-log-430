using ProjetLog430.Domain.Contracts;
using ProjetLog430.Domain.Model.Observabilite;
using ProjetLog430.Domain.Model.Trading;
using ProjetLog430.Domain.Ports.Inbound;
using ProjetLog430.Domain.Ports.Outbound;
using Serilog;

namespace ProjetLog430.Application.Services;

/// <summary>
/// Service d'application pour UC-05 : Placement d'ordres.
/// Architecture Hexagonale : Couche Application, orchestre la logique métier sans dépendance technique.
/// Implémente IOrderUseCase (Port Inbound), utilise les Ports Outbound.
/// </summary>
public sealed class OrderService : IOrderUseCase
{
    private readonly IOrderRepository _orders;
    private readonly ICompteRepository _comptes;
    private readonly IPortfolioRepository _portefeuilles;
    private readonly IPreTradeCheckPort _preTradeCheck;
    private readonly IOrderMatchingPort _orderMatching;
    private readonly IAuditPort _audit;
    private readonly ICachePort _cache;

    public OrderService(
        IOrderRepository orders,
        ICompteRepository comptes,
        IPortfolioRepository portefeuilles,
        IPreTradeCheckPort preTradeCheck,
        IOrderMatchingPort orderMatching,
        IAuditPort audit,
        ICachePort cache)
    {
        _orders = orders;
        _comptes = comptes;
        _portefeuilles = portefeuilles;
        _preTradeCheck = preTradeCheck;
        _orderMatching = orderMatching;
        _audit = audit;
        _cache = cache;
    }

    public async Task<OrderOperationResult> PlaceOrderAsync(
        Guid accountId,
        string clientOrderId,
        string symbol,
        string side,
        string type,
        decimal quantity,
        decimal? price,
        string timeInForce,
        CancellationToken ct = default)
    {
        Log.Information("[UC05_ORDER_PLACE_START] AccountId={AccountId}, ClientOrderId={ClientOrderId}, Symbol={Symbol}, Side={Side}, Type={Type}, Qty={Quantity}",
            accountId, clientOrderId, symbol, side, type, quantity);

        // 1. Validation de base
        if (string.IsNullOrWhiteSpace(symbol))
            return OrderOperationResult.Fail("INVALID_SYMBOL", "Le symbole est requis");

        if (string.IsNullOrWhiteSpace(clientOrderId))
            return OrderOperationResult.Fail("INVALID_CLIENT_ORDER_ID", "ClientOrderId requis pour idempotence");

        if (quantity <= 0)
            return OrderOperationResult.Fail("INVALID_QUANTITY", "La quantité doit être positive");

        // 2. Vérifier idempotence (même clientOrderId)
        var cacheKey = $"order:{accountId}:{clientOrderId}";
        var cachedOrder = await _cache.GetAsync<OrderDto>(cacheKey, ct);
        if (cachedOrder != null)
        {
            Log.Information("[UC05_ORDER_IDEMPOTENT] Returning cached order {OrderId}", cachedOrder.OrderId);
            return OrderOperationResult.Ok(cachedOrder.OrderId, cachedOrder.ClientOrderId, cachedOrder.Status);
        }

        var existingOrder = await _orders.GetByClientOrderIdAsync(accountId, clientOrderId, ct);
        if (existingOrder != null)
        {
            Log.Information("[UC05_ORDER_IDEMPOTENT] Order already exists {OrderId}", existingOrder.OrderId);
            return OrderOperationResult.Ok(existingOrder.OrderId, existingOrder.ClientOrderId, existingOrder.Statut.ToString());
        }

        // 3. Vérifier que le compte existe
        var compte = await _comptes.GetByIdAsync(accountId, ct);
        if (compte == null)
            return OrderOperationResult.Fail("ACCOUNT_NOT_FOUND", "Compte introuvable");

        // 4. Parser les enums
        if (!Enum.TryParse<SensOrdre>(side, true, out var sensOrdre))
            return OrderOperationResult.Fail("INVALID_SIDE", "Sens invalide (Buy/Sell)");

        if (!Enum.TryParse<TypeOrdre>(type, true, out var typeOrdre))
            return OrderOperationResult.Fail("INVALID_TYPE", "Type invalide (Market/Limit)");

        if (!Enum.TryParse<DureeValidite>(timeInForce, true, out var dureeValidite))
            return OrderOperationResult.Fail("INVALID_TIME_IN_FORCE", "Durée de validité invalide (DAY/IOC/FOK/GTC)");

        // 5. Validation type d'ordre
        if (typeOrdre == TypeOrdre.Market && price.HasValue)
        {
            Log.Warning("[UC05_ORDER_MARKET_WITH_PRICE] Market order should not have price, ignoring");
            price = null;
        }

        if (typeOrdre == TypeOrdre.Limit && !price.HasValue)
            return OrderOperationResult.Fail("PRICE_REQUIRED", "Le prix est requis pour un ordre limite");

        // 6. Normaliser et horodater
        var symbolNormalized = symbol.ToUpperInvariant();
        var orderTimestamp = DateTime.UtcNow;

        // 7. **CONTRÔLES PRÉ-TRADE**
        Log.Information("[UC05_PRE_TRADE_CHECKS_START] Symbol={Symbol}, AccountId={AccountId}", symbolNormalized, accountId);

        // 7.1 Vérifier que l'instrument est actif
        var instrumentCheck = await _preTradeCheck.CheckInstrumentStatusAsync(symbolNormalized, ct);
        if (!instrumentCheck.Passed)
        {
            await _audit.WriteAsync(AuditLog.Ecrire("UC05_ORDER_REJECTED_INSTRUMENT", $"account:{accountId}",
                payload: new { accountId, symbol = symbolNormalized, reason = instrumentCheck.RejectReason }), ct);
            return OrderOperationResult.Fail(instrumentCheck.ErrorCode ?? "INSTRUMENT_INACTIVE", instrumentCheck.RejectReason ?? "Instrument non actif");
        }

        // 7.2 Vérifier les bandes de prix (price bands)
        if (price.HasValue)
        {
            var priceBandCheck = await _preTradeCheck.CheckPriceBandsAsync(symbolNormalized, price.Value, ct);
            if (!priceBandCheck.Passed)
            {
                await _audit.WriteAsync(AuditLog.Ecrire("UC05_ORDER_REJECTED_PRICE_BAND", $"account:{accountId}",
                    payload: new { accountId, symbol = symbolNormalized, price, reason = priceBandCheck.RejectReason }), ct);
                return OrderOperationResult.Fail(priceBandCheck.ErrorCode ?? "PRICE_BAND_VIOLATION", priceBandCheck.RejectReason ?? "Prix hors bandes autorisées");
            }
        }

        // 7.3 Vérifier le pouvoir d'achat (pour ordres d'achat)
        if (sensOrdre == SensOrdre.Buy)
        {
            var buyingPowerCheck = await _preTradeCheck.CheckBuyingPowerAsync(accountId, symbolNormalized, quantity, price, ct);
            if (!buyingPowerCheck.Passed)
            {
                await _audit.WriteAsync(AuditLog.Ecrire("UC05_ORDER_REJECTED_BUYING_POWER", $"account:{accountId}",
                    payload: new { accountId, symbol = symbolNormalized, quantity, price, reason = buyingPowerCheck.RejectReason }), ct);
                return OrderOperationResult.Fail(buyingPowerCheck.ErrorCode ?? "INSUFFICIENT_BUYING_POWER", buyingPowerCheck.RejectReason ?? "Pouvoir d'achat insuffisant");
            }
        }

        // 7.4 Vérifier short-sell (pour ordres de vente)
        if (sensOrdre == SensOrdre.Sell)
        {
            var shortSellCheck = await _preTradeCheck.CheckShortSellAsync(accountId, symbolNormalized, ct);
            if (!shortSellCheck.Passed)
            {
                await _audit.WriteAsync(AuditLog.Ecrire("UC05_ORDER_REJECTED_SHORT_SELL", $"account:{accountId}",
                    payload: new { accountId, symbol = symbolNormalized, reason = shortSellCheck.RejectReason }), ct);
                return OrderOperationResult.Fail(shortSellCheck.ErrorCode ?? "SHORT_SELL_NOT_ALLOWED", shortSellCheck.RejectReason ?? "Vente à découvert non autorisée");
            }
        }

        // 7.5 Vérifier les limites de trading
        var notional = price.HasValue ? price.Value * quantity : 0; // Pour ordre marché, on estime à 0 (sera calculé à l'exécution)
        var tradingLimitsCheck = await _preTradeCheck.CheckTradingLimitsAsync(accountId, notional, ct);
        if (!tradingLimitsCheck.Passed)
        {
            await _audit.WriteAsync(AuditLog.Ecrire("UC05_ORDER_REJECTED_TRADING_LIMITS", $"account:{accountId}",
                payload: new { accountId, notional, reason = tradingLimitsCheck.RejectReason }), ct);
            return OrderOperationResult.Fail(tradingLimitsCheck.ErrorCode ?? "TRADING_LIMIT_EXCEEDED", tradingLimitsCheck.RejectReason ?? "Limite de trading dépassée");
        }

        Log.Information("[UC05_PRE_TRADE_CHECKS_PASSED] All checks passed for Symbol={Symbol}, AccountId={AccountId}", symbolNormalized, accountId);

        // 8. Créer l'ordre (Domain Model)
        Ordre ordre;
        try
        {
            ordre = Ordre.Creer(accountId, clientOrderId, symbolNormalized, sensOrdre, typeOrdre, quantity, price, dureeValidite);
            ordre.Accepter(); // Accepté après contrôles pré-trade
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[UC05_ORDER_CREATION_FAILED] Failed to create order");
            return OrderOperationResult.Fail("ORDER_CREATION_FAILED", ex.Message);
        }

        // 9. Persister l'ordre
        await _orders.AddAsync(ordre, ct);
        await _orders.SaveChangesAsync(ct);

        Log.Information("[UC05_ORDER_ACCEPTED] OrderId={OrderId}, ClientOrderId={ClientOrderId}, Status={Status}",
            ordre.OrderId, ordre.ClientOrderId, ordre.Statut);

        // 10. Soumettre au moteur d'appariement
        var submitted = await _orderMatching.SubmitOrderAsync(ordre, ct);
        if (submitted)
        {
            ordre.PlacerDansCarnet();
            await _orders.UpdateAsync(ordre, ct);
            await _orders.SaveChangesAsync(ct);
            Log.Information("[UC05_ORDER_WORKING] OrderId={OrderId} placed in order book", ordre.OrderId);
        }

        // 11. Mettre en cache pour idempotence
        var orderDto = MapToDto(ordre);
        await _cache.SetAsync(cacheKey, orderDto, TimeSpan.FromMinutes(30), ct);

        // 12. Audit
        await _audit.WriteAsync(AuditLog.Ecrire("UC05_ORDER_PLACED", $"account:{accountId}",
            payload: new
            {
                orderId = ordre.OrderId,
                accountId,
                clientOrderId,
                symbol = symbolNormalized,
                side = sensOrdre.ToString(),
                type = typeOrdre.ToString(),
                quantity,
                price,
                timeInForce = dureeValidite.ToString(),
                status = ordre.Statut.ToString()
            }), ct);

        return OrderOperationResult.Ok(ordre.OrderId, ordre.ClientOrderId, ordre.Statut.ToString());
    }

    public async Task<OrderQueryResult> GetOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        var ordre = await _orders.GetByIdAsync(orderId, ct);
        if (ordre == null)
            return OrderQueryResult.Fail("ORDER_NOT_FOUND", "Ordre introuvable");

        var orderDto = MapToDto(ordre);
        return OrderQueryResult.Ok(orderDto);
    }

    public async Task<List<OrderDto>> GetOrdersByAccountAsync(Guid accountId, CancellationToken ct = default)
    {
        var ordres = await _orders.GetByAccountIdAsync(accountId, ct);
        return ordres.Select(MapToDto).ToList();
    }

    public async Task<OrderOperationResult> CancelOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        var ordre = await _orders.GetByIdAsync(orderId, ct);
        if (ordre == null)
            return OrderOperationResult.Fail("ORDER_NOT_FOUND", "Ordre introuvable");

        if (ordre.EstTerminal())
            return OrderOperationResult.Fail("ORDER_TERMINAL", "L'ordre ne peut plus être annulé");

        try
        {
            // Annuler dans le moteur d'appariement
            await _orderMatching.CancelOrderAsync(orderId, ct);

            // Annuler dans le domain
            ordre.Annuler();
            await _orders.UpdateAsync(ordre, ct);
            await _orders.SaveChangesAsync(ct);

            await _audit.WriteAsync(AuditLog.Ecrire("UC05_ORDER_CANCELLED", $"order:{orderId}",
                payload: new { orderId, accountId = ordre.AccountId }), ct);

            return OrderOperationResult.Ok(ordre.OrderId, ordre.ClientOrderId, ordre.Statut.ToString());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[UC05_ORDER_CANCEL_FAILED] OrderId={OrderId}", orderId);
            return OrderOperationResult.Fail("CANCEL_FAILED", ex.Message);
        }
    }

    private static OrderDto MapToDto(Ordre ordre)
    {
        return new OrderDto
        {
            OrderId = ordre.OrderId,
            AccountId = ordre.AccountId,
            ClientOrderId = ordre.ClientOrderId,
            Symbol = ordre.Symbol,
            Side = ordre.Side.ToString(),
            Type = ordre.Type.ToString(),
            Quantity = ordre.Quantity,
            Price = ordre.Price,
            TimeInForce = ordre.TimeInForce.ToString(),
            Status = ordre.Statut.ToString(),
            FilledQuantity = ordre.FilledQuantity,
            AvgPrice = ordre.AvgPrice,
            CreatedAt = ordre.CreatedAt,
            UpdatedAt = ordre.UpdatedAt,
            RejectReason = ordre.RejectReason,
            Executions = ordre.Executions.Select(e => new ExecutionDto
            {
                ExecutionId = e.ExecutionId,
                ExecQuantity = e.ExecQuantity,
                ExecPrice = e.ExecPrice,
                Commission = e.Commission,
                Timestamp = e.Timestamp
            }).ToList()
        };
    }
}
