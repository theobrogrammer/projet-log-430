using Microsoft.AspNetCore.SignalR;
using Serilog;

namespace ProjetLog430.Infrastructure.Web.Hubs;

/// <summary>
/// SignalR Hub pour diffuser les cotations en temps réel via WebSocket (UC-04).
/// Architecture Hexagonale :
/// - Point d'entrée Infrastructure.Web (minimal)
/// - Gère uniquement les connexions et groupes SignalR
/// - La logique métier est dans Application/Services
/// </summary>
public sealed class MarketDataHub : Hub
{
    private static readonly Dictionary<string, HashSet<string>> _symbolSubscriptions = new();
    private static readonly object _lock = new();

    public override async Task OnConnectedAsync()
    {
        var connectionId = Context.ConnectionId;
        Log.Information("[UC04_SIGNALR_HUB] Client connected: {ConnectionId}", connectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        
        // Remove connection from all symbol groups
        lock (_lock)
        {
            foreach (var subscribers in _symbolSubscriptions.Values)
            {
                subscribers.Remove(connectionId);
            }
        }

        if (exception != null)
        {
            Log.Warning(exception, "[UC04_SIGNALR_HUB] Client disconnected with error: {ConnectionId}", connectionId);
        }
        else
        {
            Log.Information("[UC04_SIGNALR_HUB] Client disconnected: {ConnectionId}", connectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Subscribe to quotes for a specific symbol
    /// </summary>
    public async Task SubscribeToSymbol(string symbol)
    {
        var connectionId = Context.ConnectionId;
        var symbolUpper = symbol.ToUpper();
        
        await Groups.AddToGroupAsync(connectionId, symbolUpper);
        
        lock (_lock)
        {
            if (!_symbolSubscriptions.ContainsKey(symbolUpper))
            {
                _symbolSubscriptions[symbolUpper] = new HashSet<string>();
            }
            _symbolSubscriptions[symbolUpper].Add(connectionId);
        }

        Log.Information("[UC04_SIGNALR_HUB] Client {ConnectionId} subscribed to {Symbol}", connectionId, symbolUpper);
        
        // Acknowledge subscription
        await Clients.Caller.SendAsync("SubscriptionConfirmed", symbolUpper);
    }

    /// <summary>
    /// Unsubscribe from quotes for a specific symbol
    /// </summary>
    public async Task UnsubscribeFromSymbol(string symbol)
    {
        var connectionId = Context.ConnectionId;
        var symbolUpper = symbol.ToUpper();
        
        await Groups.RemoveFromGroupAsync(connectionId, symbolUpper);
        
        lock (_lock)
        {
            if (_symbolSubscriptions.ContainsKey(symbolUpper))
            {
                _symbolSubscriptions[symbolUpper].Remove(connectionId);
            }
        }

        Log.Information("[UC04_SIGNALR_HUB] Client {ConnectionId} unsubscribed from {Symbol}", connectionId, symbolUpper);
        
        // Acknowledge unsubscription
        await Clients.Caller.SendAsync("UnsubscriptionConfirmed", symbolUpper);
    }

    /// <summary>
    /// Subscribe to multiple symbols at once
    /// </summary>
    public async Task SubscribeToSymbols(List<string> symbols)
    {
        foreach (var symbol in symbols)
        {
            await SubscribeToSymbol(symbol);
        }
    }

    /// <summary>
    /// Unsubscribe from multiple symbols at once
    /// </summary>
    public async Task UnsubscribeFromSymbols(List<string> symbols)
    {
        foreach (var symbol in symbols)
        {
            await UnsubscribeFromSymbol(symbol);
        }
    }

    /// <summary>
    /// Get current subscription count for a symbol (for monitoring)
    /// </summary>
    public static int GetSubscriptionCount(string symbol)
    {
        lock (_lock)
        {
            return _symbolSubscriptions.TryGetValue(symbol.ToUpper(), out var subscribers) 
                ? subscribers.Count 
                : 0;
        }
    }

    /// <summary>
    /// Get all active symbols with subscribers
    /// </summary>
    public static List<string> GetActiveSymbols()
    {
        lock (_lock)
        {
            return _symbolSubscriptions
                .Where(kvp => kvp.Value.Count > 0)
                .Select(kvp => kvp.Key)
                .ToList();
        }
    }
}
