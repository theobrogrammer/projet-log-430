using ProjetLog430.Domain.Contracts;
using ProjetLog430.Domain.Ports.Inbound;
using ProjetLog430.Domain.Model.MarketData;
using ProjetLog430.Domain.Ports.Outbound;
using ProjetLog430.Domain.Model.Observabilite;
using Microsoft.Extensions.Logging;
using Serilog;

namespace ProjetLog430.Application.Services;

/// <summary>
/// Service d'application pour UC-04 : Abonnement aux données de marché en temps réel
/// Architecture hexagonale : orchestration entre Domain et Ports, retourne des Result (pas d'exceptions)
/// </summary>
public sealed class MarketDataService : IMarketDataUseCase
{
    private readonly IMarketFeedPort _marketFeed;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IQuoteRepository _quotes;
    private readonly IAuditPort _audit;
    private readonly ILogger<MarketDataService> _logger;
    
    public MarketDataService(
        IMarketFeedPort marketFeed,
        ISubscriptionRepository subscriptions,
        IQuoteRepository quotes,
        IAuditPort audit,
        ILogger<MarketDataService> logger)
    {
        _marketFeed = marketFeed;
        _subscriptions = subscriptions;
        _quotes = quotes;
        _audit = audit;
        _logger = logger;
    }

    public async Task<MarketDataOperationResult> SubscribeAsync(
        Guid clientId, 
        List<string> symbols, 
        CanalStreaming canal = CanalStreaming.WebSocket, 
        CancellationToken ct = default)
    {
        Log.Information("[UC04_SUBSCRIBE] Début abonnement pour client {ClientId}, symboles: {Symbols}", 
            clientId, string.Join(", ", symbols));

        // 1) Validation métier : symboles disponibles
        var availableSymbols = _marketFeed.GetAvailableSymbols();
        var invalidSymbols = symbols.Where(s => !availableSymbols.Contains(s.ToUpper())).ToList();
        if (invalidSymbols.Any())
        {
            _logger.LogWarning("[UC04_SUBSCRIBE] Symboles invalides: {InvalidSymbols}", 
                string.Join(", ", invalidSymbols));
            return MarketDataOperationResult.Fail(
                "INVALID_SYMBOLS",
                $"Symboles invalides: {string.Join(", ", invalidSymbols)}. " +
                $"Symboles disponibles: {string.Join(", ", availableSymbols)}");
        }

        // 2) Créer l'abonnement (entité Domain)
        var subscription = Subscription.Creer(clientId, symbols, canal);

        // 3) Persister l'abonnement
        await _subscriptions.AddAsync(subscription, ct);

        // 4) Audit log
        await _audit.WriteAsync(
            AuditLog.Ecrire("MARKET_DATA_SUBSCRIBED", clientId.ToString(),
                payload: new { 
                    subscriptionId = subscription.SubscriptionId, 
                    symbols, 
                    canal = canal.ToString() 
                }), ct);

        _logger.LogInformation("[UC04_SUBSCRIBE] Abonnement créé: {SubscriptionId} pour {ClientId}", 
            subscription.SubscriptionId, clientId);

        // 5) Retourner le résultat
        return MarketDataOperationResult.Ok(MapToResult(subscription));
    }

    public async Task<MarketDataOperationResult> AddSymbolsAsync(
        Guid subscriptionId, 
        List<string> symbols, 
        CancellationToken ct = default)
    {
        Log.Information("[UC04_ADD_SYMBOLS] Ajout symboles {Symbols} à abonnement {SubscriptionId}", 
            string.Join(", ", symbols), subscriptionId);

        // 1) Récupérer l'abonnement
        var subscription = await _subscriptions.GetByIdAsync(subscriptionId, ct);
        if (subscription == null)
        {
            _logger.LogWarning("[UC04_ADD_SYMBOLS] Abonnement introuvable: {SubscriptionId}", subscriptionId);
            return MarketDataOperationResult.Fail(
                "SUBSCRIPTION_NOT_FOUND",
                $"Abonnement {subscriptionId} introuvable");
        }

        // 2) Vérifier statut
        if (subscription.Statut == StatutSubscription.Cancelled)
        {
            _logger.LogWarning("[UC04_ADD_SYMBOLS] Tentative ajout symboles sur abonnement annulé: {SubscriptionId}", subscriptionId);
            return MarketDataOperationResult.Fail(
                "SUBSCRIPTION_CANCELLED",
                "Impossible d'ajouter des symboles à un abonnement annulé");
        }

        // 3) Validation symboles
        var availableSymbols = _marketFeed.GetAvailableSymbols();
        var invalidSymbols = symbols.Where(s => !availableSymbols.Contains(s.ToUpper())).ToList();
        if (invalidSymbols.Any())
        {
            _logger.LogWarning("[UC04_ADD_SYMBOLS] Symboles invalides: {InvalidSymbols}", 
                string.Join(", ", invalidSymbols));
            return MarketDataOperationResult.Fail(
                "INVALID_SYMBOLS",
                $"Symboles invalides: {string.Join(", ", invalidSymbols)}");
        }

        // 4) Ajouter symboles (logique métier)
        foreach (var symbol in symbols)
        {
            subscription.AjouterSymbole(symbol);
        }

        // 5) Persister
        await _subscriptions.UpdateAsync(subscription, ct);

        // 6) Audit
        await _audit.WriteAsync(
            AuditLog.Ecrire("MARKET_DATA_SYMBOLS_ADDED", subscription.ClientId.ToString(),
                payload: new { subscriptionId, addedSymbols = symbols }), ct);

        _logger.LogInformation("[UC04_ADD_SYMBOLS] Symboles ajoutés à {SubscriptionId}: {Symbols}", 
            subscriptionId, string.Join(", ", symbols));

        return MarketDataOperationResult.Ok(MapToResult(subscription));
    }

    public async Task<MarketDataOperationResult> RemoveSymbolsAsync(
        Guid subscriptionId, 
        List<string> symbols, 
        CancellationToken ct = default)
    {
        Log.Information("[UC04_REMOVE_SYMBOLS] Retrait symboles {Symbols} de abonnement {SubscriptionId}", 
            string.Join(", ", symbols), subscriptionId);

        // 1) Récupérer l'abonnement
        var subscription = await _subscriptions.GetByIdAsync(subscriptionId, ct);
        if (subscription == null)
        {
            _logger.LogWarning("[UC04_REMOVE_SYMBOLS] Abonnement introuvable: {SubscriptionId}", subscriptionId);
            return MarketDataOperationResult.Fail(
                "SUBSCRIPTION_NOT_FOUND",
                $"Abonnement {subscriptionId} introuvable");
        }

        // 2) Vérifier statut
        if (subscription.Statut == StatutSubscription.Cancelled)
        {
            _logger.LogWarning("[UC04_REMOVE_SYMBOLS] Tentative retrait symboles sur abonnement annulé: {SubscriptionId}", subscriptionId);
            return MarketDataOperationResult.Fail(
                "SUBSCRIPTION_CANCELLED",
                "Impossible de retirer des symboles d'un abonnement annulé");
        }

        // 3) Retirer symboles (logique métier avec validation)
        foreach (var symbol in symbols)
        {
            try
            {
                subscription.RetirerSymbole(symbol);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("dernier symbole"))
            {
                _logger.LogWarning("[UC04_REMOVE_SYMBOLS] Tentative de retrait du dernier symbole");
                return MarketDataOperationResult.Fail(
                    "CANNOT_REMOVE_LAST_SYMBOL",
                    "Impossible de retirer tous les symboles. Annulez l'abonnement si vous ne voulez plus recevoir de données.");
            }
        }

        // 4) Persister
        await _subscriptions.UpdateAsync(subscription, ct);

        // 5) Audit
        await _audit.WriteAsync(
            AuditLog.Ecrire("MARKET_DATA_SYMBOLS_REMOVED", subscription.ClientId.ToString(),
                payload: new { subscriptionId, removedSymbols = symbols }), ct);

        _logger.LogInformation("[UC04_REMOVE_SYMBOLS] Symboles retirés de {SubscriptionId}: {Symbols}", 
            subscriptionId, string.Join(", ", symbols));

        return MarketDataOperationResult.Ok(MapToResult(subscription));
    }

    public async Task<UnsubscribeResult> UnsubscribeAsync(Guid subscriptionId, CancellationToken ct = default)
    {
        Log.Information("[UC04_UNSUBSCRIBE] Annulation abonnement {SubscriptionId}", subscriptionId);

        // 1) Récupérer l'abonnement
        var subscription = await _subscriptions.GetByIdAsync(subscriptionId, ct);
        if (subscription == null)
        {
            _logger.LogWarning("[UC04_UNSUBSCRIBE] Abonnement introuvable: {SubscriptionId}", subscriptionId);
            return UnsubscribeResult.Fail(
                "SUBSCRIPTION_NOT_FOUND",
                $"Abonnement {subscriptionId} introuvable");
        }

        // 2) Annuler (logique métier)
        subscription.Annuler();

        // 3) Persister
        await _subscriptions.UpdateAsync(subscription, ct);

        // 4) Audit
        await _audit.WriteAsync(
            AuditLog.Ecrire("MARKET_DATA_UNSUBSCRIBED", subscription.ClientId.ToString(),
                payload: new { subscriptionId }), ct);

        _logger.LogInformation("[UC04_UNSUBSCRIBE] Abonnement annulé: {SubscriptionId}", subscriptionId);

        return UnsubscribeResult.Ok();
    }

    public async Task<QuotesResult> GetLatestQuotesAsync(List<string> symbols, CancellationToken ct = default)
    {
        Log.Information("[UC04_GET_QUOTES] Récupération cotations pour {Symbols}", 
            string.Join(", ", symbols));

        // 1) Validation symboles
        var availableSymbols = _marketFeed.GetAvailableSymbols();
        var invalidSymbols = symbols.Where(s => !availableSymbols.Contains(s.ToUpper())).ToList();
        if (invalidSymbols.Any())
        {
            _logger.LogWarning("[UC04_GET_QUOTES] Symboles invalides: {InvalidSymbols}", 
                string.Join(", ", invalidSymbols));
            return QuotesResult.Fail(
                "INVALID_SYMBOLS",
                $"Symboles invalides: {string.Join(", ", invalidSymbols)}. " +
                $"Symboles disponibles: {string.Join(", ", availableSymbols)}");
        }

        // 2) Récupérer les dernières cotations depuis le repository ou le feed
        var quotes = new List<Quote>();
        foreach (var symbol in symbols)
        {
            // Essayer d'abord depuis le repository (cache)
            var quote = await _quotes.GetLatestQuoteAsync(symbol, ct);
            
            // Si pas en cache, obtenir depuis le feed
            if (quote == null)
            {
                quote = await _marketFeed.GetLatestQuoteAsync(symbol, ct);
            }

            if (quote != null)
            {
                quotes.Add(quote);
            }
        }

        _logger.LogInformation("[UC04_GET_QUOTES] {QuoteCount} cotations récupérées sur {RequestedCount} demandées", 
            quotes.Count, symbols.Count);

        return QuotesResult.Ok(quotes);
    }

    // Helper : mapper Domain → Contract (DTO)
    private static MarketDataResult MapToResult(Subscription subscription)
    {
        return new MarketDataResult(
            SubscriptionId: subscription.SubscriptionId,
            Symbols: subscription.Symbols,
            Canal: subscription.Canal.ToString(),
            Statut: subscription.Statut.ToString()
        );
    }
}
