using ProjetLog430.Domain.Model.MarketData;
using ProjetLog430.Domain.Ports.Outbound;
using Serilog;

namespace ProjetLog430.Infrastructure.Adapters.MarketData;

/// <summary>
/// Simulateur de flux de marché pour UC-04
/// Génère des cotations aléatoires basées sur des prix de base réalistes
/// </summary>
public sealed class MarketFeedSimulator : IMarketFeedPort, IDisposable
{
    private readonly Dictionary<string, decimal> _baseprices = new()
    {
        { "AAPL", 180.50m },
        { "GOOGL", 140.25m },
        { "MSFT", 380.75m },
        { "TSLA", 245.30m },
        { "AMZN", 155.60m },
        { "META", 350.20m },
        { "NVDA", 485.90m },
        { "AMD", 140.15m },
        { "NFLX", 445.80m },
        { "DIS", 95.40m }
    };

    private readonly Random _rng = new();
    private readonly Timer? _timer;
    private readonly IQuoteRepository _quoteRepo;
    private readonly object _lock = new();
    private bool _isRunning = false;

    public event EventHandler<Quote>? OnQuoteGenerated;

    public MarketFeedSimulator(IQuoteRepository quoteRepo)
    {
        _quoteRepo = quoteRepo;
        // Timer disabled by default - will be started explicitly
        _timer = null;
    }

    public async Task StartFeedAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_isRunning)
            {
                Log.Warning("[UC04_MARKET_FEED] Feed already running");
                return;
            }
            _isRunning = true;
        }

        Log.Information("[UC04_MARKET_FEED] Starting market data feed simulation");
        
        // Generate initial quotes for all symbols
        foreach (var symbol in _baseprices.Keys)
        {
            await GenerateQuoteForSymbol(symbol, ct);
        }

        Log.Information("[UC04_MARKET_FEED] Market feed started with {SymbolCount} symbols", _baseprices.Count);
    }

    public Task StopFeedAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (!_isRunning)
            {
                return Task.CompletedTask;
            }
            _isRunning = false;
        }

        _timer?.Dispose();
        Log.Information("[UC04_MARKET_FEED] Market feed stopped");
        return Task.CompletedTask;
    }

    public async Task<Quote?> GetLatestQuoteAsync(string symbol, CancellationToken ct = default)
    {
        if (!_baseprices.ContainsKey(symbol.ToUpper()))
        {
            return null;
        }

        var latest = await _quoteRepo.GetLatestQuoteAsync(symbol, ct);
        
        // If no quote exists, generate one
        if (latest == null)
        {
            latest = await GenerateQuoteForSymbol(symbol, ct);
        }

        return latest;
    }

    public async Task<List<Quote>> GetQuoteHistoryAsync(string symbol, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        return await _quoteRepo.GetQuoteHistoryAsync(symbol, from, to, ct);
    }

    public List<string> GetAvailableSymbols()
    {
        return _baseprices.Keys.ToList();
    }

    /// <summary>
    /// Generate a single quote manually (for testing or on-demand)
    /// </summary>
    public async Task<Quote> GenerateQuoteForSymbol(string symbol, CancellationToken ct = default)
    {
        var symbolUpper = symbol.ToUpper();
        if (!_baseprices.TryGetValue(symbolUpper, out var basePrice))
        {
            throw new ArgumentException($"Unknown symbol: {symbol}");
        }

        // Random walk: ±0.5% from base price
        var variation = 1.0m + ((decimal)_rng.NextDouble() - 0.5m) * 0.01m;
        var midPrice = basePrice * variation;
        
        // Spread: 0.05% to 0.15%
        var spreadPercent = 0.0005m + (decimal)_rng.NextDouble() * 0.001m;
        var spread = midPrice * spreadPercent;
        
        var bid = Math.Round(midPrice - spread / 2, 2);
        var ask = Math.Round(midPrice + spread / 2, 2);
        var last = Math.Round(midPrice, 2); // Last traded price is typically the mid-price
        var volume = (decimal)_rng.Next(1000, 100000); // Random volume between 1K and 100K

        var quote = Quote.Creer(symbolUpper, bid, ask, last, volume);
        
        // Persist to repository
        await _quoteRepo.AddAsync(quote, ct);
        
        // Notify subscribers
        OnQuoteGenerated?.Invoke(this, quote);
        
        Log.Debug("[UC04_MARKET_FEED] Generated quote: {Symbol} Bid={Bid} Ask={Ask}", 
            symbolUpper, bid, ask);

        return quote;
    }

    /// <summary>
    /// Background task to generate quotes periodically (call this in a hosted service)
    /// </summary>
    public async Task GenerateQuotesAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _isRunning)
        {
            try
            {
                // Pick a random symbol and generate a quote
                var symbols = _baseprices.Keys.ToList();
                var randomSymbol = symbols[_rng.Next(symbols.Count)];
                
                await GenerateQuoteForSymbol(randomSymbol, ct);
                
                // Wait 100-500ms before next quote (simulates realistic market activity)
                await Task.Delay(_rng.Next(100, 500), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[UC04_MARKET_FEED] Error generating quote");
                await Task.Delay(1000, ct); // Back off on error
            }
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
