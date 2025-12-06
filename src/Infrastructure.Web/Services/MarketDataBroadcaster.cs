using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ProjetLog430.Domain.Model.MarketData;
using ProjetLog430.Domain.Ports.Outbound;
using ProjetLog430.Infrastructure.Adapters.MarketData;
using ProjetLog430.Infrastructure.Web.Hubs;
using Serilog;

namespace ProjetLog430.Infrastructure.Web.Services;

/// <summary>
/// Background service that broadcasts market data quotes to SignalR clients
/// Uses IServiceProvider to create scopes for accessing scoped services
/// </summary>
public sealed class MarketDataBroadcaster : BackgroundService
{
    private readonly IHubContext<MarketDataHub> _hubContext;
    private readonly IServiceProvider _serviceProvider;

    public MarketDataBroadcaster(IHubContext<MarketDataHub> hubContext, IServiceProvider serviceProvider)
    {
        _hubContext = hubContext;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("[UC04_BROADCASTER] Starting market data broadcaster...");

        // Create a scope to access scoped services
        using var scope = _serviceProvider.CreateScope();
        var marketFeed = scope.ServiceProvider.GetRequiredService<IMarketFeedPort>();

        // Subscribe to quote generation events
        if (marketFeed is MarketFeedSimulator simulator)
        {
            simulator.OnQuoteGenerated += async (sender, quote) =>
            {
                await BroadcastQuote(quote);
            };

            // Start the feed
            await marketFeed.StartFeedAsync(stoppingToken);

            // Generate quotes continuously
            await simulator.GenerateQuotesAsync(stoppingToken);
        }
        else
        {
            Log.Warning("[UC04_BROADCASTER] MarketFeed is not a simulator, broadcaster will not generate quotes");
        }
    }

    private async Task BroadcastQuote(Quote quote)
    {
        try
        {
            // Broadcast to all clients subscribed to this symbol's group
            await _hubContext.Clients
                .Group(quote.Symbol)
                .SendAsync("ReceiveQuote", new
                {
                    symbol = quote.Symbol,
                    bid = quote.Bid,
                    ask = quote.Ask,
                    last = quote.Last,
                    volume = quote.Volume,
                    spread = quote.ObtenirSpread(),
                    spreadBps = quote.ObtenirSpreadBps(),
                    timestamp = quote.Timestamp
                });

            var subCount = MarketDataHub.GetSubscriptionCount(quote.Symbol);
            if (subCount > 0)
            {
                Log.Debug("[UC04_BROADCASTER] Broadcasted {Symbol} to {Count} subscribers", 
                    quote.Symbol, subCount);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[UC04_BROADCASTER] Error broadcasting quote for {Symbol}", quote.Symbol);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        Log.Information("[UC04_BROADCASTER] Stopping market data broadcaster...");
        
        using var scope = _serviceProvider.CreateScope();
        var marketFeed = scope.ServiceProvider.GetRequiredService<IMarketFeedPort>();
        await marketFeed.StopFeedAsync(cancellationToken);
        
        await base.StopAsync(cancellationToken);
    }
}
