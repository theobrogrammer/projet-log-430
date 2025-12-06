using ProjetLog430.Domain.Model.Trading;
using ProjetLog430.Domain.Ports.Outbound;
using Serilog;

namespace ProjetLog430.Infrastructure.Adapters.OrderMatching;

/// <summary>
/// Simulateur de moteur d'appariement d'ordres (UC-05).
/// Architecture Hexagonale : Implémente IOrderMatchingPort.
/// En production, ce serait un vrai moteur d'appariement (FIX protocol, etc.)
/// </summary>
public sealed class OrderMatchingSimulator : IOrderMatchingPort
{
    // Carnet d'ordres en mémoire (par symbole)
    private readonly Dictionary<string, OrderBook> _orderBooks = new();
    private readonly object _lock = new();

    public Task<bool> SubmitOrderAsync(Ordre ordre, CancellationToken ct = default)
    {
        lock (_lock)
        {
            Log.Information("[ORDER_MATCHING] Submitting order to matching engine: OrderId={OrderId}, Symbol={Symbol}, Side={Side}, Type={Type}, Qty={Quantity}, Price={Price}",
                ordre.OrderId, ordre.Symbol, ordre.Side, ordre.Type, ordre.Quantity, ordre.Price);

            // Créer le carnet d'ordres pour ce symbole s'il n'existe pas
            if (!_orderBooks.ContainsKey(ordre.Symbol))
            {
                _orderBooks[ordre.Symbol] = new OrderBook(ordre.Symbol);
            }

            var orderBook = _orderBooks[ordre.Symbol];

            // Ajouter l'ordre au carnet
            orderBook.AddOrder(ordre);

            Log.Information("[ORDER_MATCHING] Order submitted successfully: OrderId={OrderId}, Status=Working", ordre.OrderId);
            
            // TODO: Implémenter la logique d'appariement (matching)
            // Pour UC-05, on se contente de placer l'ordre dans le carnet
            // UC-07 (appariement) gérera l'exécution

            return Task.FromResult(true);
        }
    }

    public Task<bool> CancelOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            Log.Information("[ORDER_MATCHING] Cancelling order: OrderId={OrderId}", orderId);

            // Chercher l'ordre dans tous les carnets
            foreach (var orderBook in _orderBooks.Values)
            {
                if (orderBook.RemoveOrder(orderId))
                {
                    Log.Information("[ORDER_MATCHING] Order cancelled successfully: OrderId={OrderId}", orderId);
                    return Task.FromResult(true);
                }
            }

            Log.Warning("[ORDER_MATCHING] Order not found in order books: OrderId={OrderId}", orderId);
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Obtenir le carnet d'ordres pour un symbole (pour monitoring/debugging)
    /// </summary>
    public OrderBook? GetOrderBook(string symbol)
    {
        lock (_lock)
        {
            return _orderBooks.TryGetValue(symbol.ToUpperInvariant(), out var orderBook) ? orderBook : null;
        }
    }
}

/// <summary>
/// Représente un carnet d'ordres (Order Book) pour un symbole
/// </summary>
public sealed class OrderBook
{
    public string Symbol { get; }
    private readonly Dictionary<Guid, Ordre> _orders = new();
    private readonly SortedDictionary<decimal, List<Guid>> _bids = new(); // Prix décroissant
    private readonly SortedDictionary<decimal, List<Guid>> _asks = new(); // Prix croissant

    public OrderBook(string symbol)
    {
        Symbol = symbol;
    }

    public void AddOrder(Ordre ordre)
    {
        _orders[ordre.OrderId] = ordre;

        // Pour les ordres limites, ajouter au bon niveau de prix
        if (ordre.Type == TypeOrdre.Limit && ordre.Price.HasValue)
        {
            var priceLevel = ordre.Side == SensOrdre.Buy ? _bids : _asks;
            
            if (!priceLevel.ContainsKey(ordre.Price.Value))
            {
                priceLevel[ordre.Price.Value] = new List<Guid>();
            }
            
            priceLevel[ordre.Price.Value].Add(ordre.OrderId);
        }

        Log.Debug("[ORDER_BOOK] Order added to {Symbol} book: OrderId={OrderId}, Side={Side}, Price={Price}",
            Symbol, ordre.OrderId, ordre.Side, ordre.Price);
    }

    public bool RemoveOrder(Guid orderId)
    {
        if (!_orders.TryGetValue(orderId, out var ordre))
            return false;

        _orders.Remove(orderId);

        // Retirer des niveaux de prix
        if (ordre.Type == TypeOrdre.Limit && ordre.Price.HasValue)
        {
            var priceLevel = ordre.Side == SensOrdre.Buy ? _bids : _asks;
            
            if (priceLevel.TryGetValue(ordre.Price.Value, out var orders))
            {
                orders.Remove(orderId);
                if (orders.Count == 0)
                {
                    priceLevel.Remove(ordre.Price.Value);
                }
            }
        }

        Log.Debug("[ORDER_BOOK] Order removed from {Symbol} book: OrderId={OrderId}", Symbol, orderId);
        return true;
    }

    public int TotalOrders => _orders.Count;
    public decimal? BestBid => _bids.Keys.LastOrDefault();
    public decimal? BestAsk => _asks.Keys.FirstOrDefault();
}
