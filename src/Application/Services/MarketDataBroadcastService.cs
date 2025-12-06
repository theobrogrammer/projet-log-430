using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ProjetLog430.Domain.Ports.Outbound;
using ProjetLog430.Infrastructure.Adapters.MarketData;
using Serilog;

namespace ProjetLog430.Application.Services;

/// <summary>
/// Service d'application orchestrant la diffusion des cotations en temps réel (UC-04).
/// Architecture Hexagonale : 
/// - Couche Application : orchestration sans dépendance technique
/// - Utilise IMarketFeedPort (entrant) et IMarketDataBroadcastPort (sortant)
/// </summary>
public sealed class MarketDataBroadcastService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public MarketDataBroadcastService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("[UC04_BROADCAST_SERVICE] Starting market data broadcast orchestration...");

        // Créer un scope pour accéder aux services scoped
        using var scope = _serviceProvider.CreateScope();
        var marketFeed = scope.ServiceProvider.GetRequiredService<IMarketFeedPort>();
        var broadcaster = scope.ServiceProvider.GetRequiredService<IMarketDataBroadcastPort>();

        // S'abonner aux événements de génération de cotations
        if (marketFeed is MarketFeedSimulator simulator)
        {
            simulator.OnQuoteGenerated += async (sender, quote) =>
            {
                await broadcaster.BroadcastQuoteAsync(quote, stoppingToken);
                
                // Log uniquement si des clients sont abonnés
                var subCount = broadcaster.GetSubscriberCount(quote.Symbol);
                if (subCount > 0)
                {
                    Log.Debug("[UC04_BROADCAST_SERVICE] Quote {Symbol} broadcasted to {Count} subscribers", 
                        quote.Symbol, subCount);
                }
            };

            // Démarrer le feed de marché
            await marketFeed.StartFeedAsync(stoppingToken);

            // Générer les cotations en continu
            await simulator.GenerateQuotesAsync(stoppingToken);
        }
        else
        {
            Log.Warning("[UC04_BROADCAST_SERVICE] MarketFeed is not a simulator, broadcast service will not generate quotes");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        Log.Information("[UC04_BROADCAST_SERVICE] Stopping market data broadcast orchestration...");
        
        using var scope = _serviceProvider.CreateScope();
        var marketFeed = scope.ServiceProvider.GetRequiredService<IMarketFeedPort>();
        await marketFeed.StopFeedAsync(cancellationToken);
        
        await base.StopAsync(cancellationToken);
    }
}
