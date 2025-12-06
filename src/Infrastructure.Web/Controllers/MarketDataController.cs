using Microsoft.AspNetCore.Mvc;
using ProjetLog430.Domain.Ports.Inbound;
using ProjetLog430.Domain.Model.MarketData;
using ProjetLog430.Domain.Ports.Outbound;
using Serilog;

namespace ProjetLog430.Infrastructure.Web.Controllers;

[ApiController]
[Route("api/v1/market")]
public sealed class MarketDataController : ControllerBase
{
    private readonly IMarketDataUseCase _marketDataUseCase;
    private readonly IMarketFeedPort _marketFeed;

    public MarketDataController(IMarketDataUseCase marketDataUseCase, IMarketFeedPort marketFeed)
    {
        _marketDataUseCase = marketDataUseCase;
        _marketFeed = marketFeed;
    }

    /// <summary>
    /// UC-04: Subscribe to market data for specific symbols
    /// POST /api/v1/market/subscribe
    /// Architecture hexagonale : gère les Result, pas d'exceptions du use case
    /// </summary>
    [HttpPost("subscribe")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest req, CancellationToken ct)
    {
        if (req.ClientId == Guid.Empty || req.Symbols == null || !req.Symbols.Any())
        {
            return BadRequest(new { error = "ClientId and Symbols are required" });
        }

        var canal = req.Canal?.ToLower() switch
        {
            "sse" => CanalStreaming.SSE,
            _ => CanalStreaming.WebSocket
        };

        Log.Information("[UC04_SUBSCRIBE] ClientId={ClientId}, Symbols={Symbols}, Canal={Canal}", 
            req.ClientId, string.Join(",", req.Symbols), canal);

        var result = await _marketDataUseCase.SubscribeAsync(req.ClientId, req.Symbols, canal, ct);

        if (!result.Success)
        {
            Log.Warning("[UC04_SUBSCRIBE] Échec: {ErrorCode} - {ErrorMessage}", 
                result.ErrorCode, result.ErrorMessage);
            return BadRequest(new 
            { 
                error = result.ErrorCode, 
                message = result.ErrorMessage 
            });
        }

        return Ok(new
        {
            subscriptionId = result.Data!.SubscriptionId,
            symbols = result.Data.Symbols,
            canal = result.Data.Canal,
            statut = result.Data.Statut
        });
    }

    /// <summary>
    /// UC-04: Unsubscribe from market data
    /// POST /api/v1/market/unsubscribe
    /// Architecture hexagonale : gère les Result
    /// </summary>
    [HttpPost("unsubscribe")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeRequest req, CancellationToken ct)
    {
        if (req.SubscriptionId == Guid.Empty)
        {
            return BadRequest(new { error = "SubscriptionId is required" });
        }

        Log.Information("[UC04_UNSUBSCRIBE] SubscriptionId={SubscriptionId}", req.SubscriptionId);

        var result = await _marketDataUseCase.UnsubscribeAsync(req.SubscriptionId, ct);

        if (!result.Success)
        {
            Log.Warning("[UC04_UNSUBSCRIBE] Échec: {ErrorCode} - {ErrorMessage}", 
                result.ErrorCode, result.ErrorMessage);
            return BadRequest(new 
            { 
                error = result.ErrorCode, 
                message = result.ErrorMessage 
            });
        }

        return Ok(new { message = "Subscription cancelled successfully" });
    }

    /// <summary>
    /// Get latest quote for a symbol
    /// GET /api/v1/market/quote/{symbol}
    /// </summary>
    [HttpGet("quote/{symbol}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetQuote(string symbol, CancellationToken ct)
    {
        Log.Information("[UC04_GET_QUOTE] Symbol={Symbol}", symbol);

        var quote = await _marketFeed.GetLatestQuoteAsync(symbol, ct);

        if (quote == null)
        {
            return NotFound(new { error = $"No quote found for symbol {symbol}" });
        }

        return Ok(new
        {
            symbol = quote.Symbol,
            bid = quote.Bid,
            ask = quote.Ask,
            spread = quote.ObtenirSpread(),
            spreadBps = quote.ObtenirSpreadBps(),
            timestamp = quote.Timestamp
        });
    }

    /// <summary>
    /// Get quote history for a symbol
    /// GET /api/v1/market/history/{symbol}?from=2024-01-01T00:00:00Z&to=2024-01-02T00:00:00Z
    /// </summary>
    [HttpGet("history/{symbol}")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetHistory(
        string symbol, 
        [FromQuery] DateTimeOffset? from, 
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        var fromDate = from ?? DateTimeOffset.UtcNow.AddHours(-24);
        var toDate = to ?? DateTimeOffset.UtcNow;

        Log.Information("[UC04_GET_HISTORY] Symbol={Symbol}, From={From}, To={To}", 
            symbol, fromDate, toDate);

        var quotes = await _marketFeed.GetQuoteHistoryAsync(symbol, fromDate, toDate, ct);

        return Ok(new
        {
            symbol,
            from = fromDate,
            to = toDate,
            count = quotes.Count,
            quotes = quotes.Select(q => new
            {
                bid = q.Bid,
                ask = q.Ask,
                spread = q.ObtenirSpread(),
                timestamp = q.Timestamp
            })
        });
    }

    /// <summary>
    /// Get available symbols
    /// GET /api/v1/market/symbols
    /// </summary>
    [HttpGet("symbols")]
    [ProducesResponseType(200)]
    public IActionResult GetSymbols()
    {
        var symbols = _marketFeed.GetAvailableSymbols();
        return Ok(new { symbols, count = symbols.Count });
    }

    /// <summary>
    /// Get latest quotes for multiple symbols
    /// POST /api/v1/market/quotes
    /// Architecture hexagonale : gère les Result
    /// </summary>
    [HttpPost("quotes")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GetQuotes([FromBody] QuotesRequest req, CancellationToken ct)
    {
        Log.Information("[UC04_GET_QUOTES] Symbols={Symbols}", string.Join(",", req.Symbols));

        var result = await _marketDataUseCase.GetLatestQuotesAsync(req.Symbols, ct);

        if (!result.Success)
        {
            Log.Warning("[UC04_GET_QUOTES] Échec: {ErrorCode} - {ErrorMessage}", 
                result.ErrorCode, result.ErrorMessage);
            return BadRequest(new 
            { 
                error = result.ErrorCode, 
                message = result.ErrorMessage 
            });
        }

        return Ok(new
        {
            count = result.Quotes!.Count,
            quotes = result.Quotes.Select(q => new
            {
                symbol = q.Symbol,
                bid = q.Bid,
                ask = q.Ask,
                spread = q.ObtenirSpread(),
                spreadBps = q.ObtenirSpreadBps(),
                timestamp = q.Timestamp
            })
        });
    }

    /// <summary>
    /// Add symbols to existing subscription
    /// POST /api/v1/market/subscription/{subscriptionId}/add-symbols
    /// Architecture hexagonale : gère les Result
    /// </summary>
    [HttpPost("subscription/{subscriptionId}/add-symbols")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> AddSymbols(Guid subscriptionId, [FromBody] SymbolsRequest req, CancellationToken ct)
    {
        if (req.Symbols == null || !req.Symbols.Any())
        {
            return BadRequest(new { error = "Symbols are required" });
        }

        Log.Information("[UC04_ADD_SYMBOLS] SubscriptionId={SubscriptionId}, Symbols={Symbols}", 
            subscriptionId, string.Join(",", req.Symbols));

        var result = await _marketDataUseCase.AddSymbolsAsync(subscriptionId, req.Symbols, ct);

        if (!result.Success)
        {
            Log.Warning("[UC04_ADD_SYMBOLS] Échec: {ErrorCode} - {ErrorMessage}", 
                result.ErrorCode, result.ErrorMessage);
            return BadRequest(new 
            { 
                error = result.ErrorCode, 
                message = result.ErrorMessage 
            });
        }

        return Ok(new
        {
            subscriptionId = result.Data!.SubscriptionId,
            symbols = result.Data.Symbols,
            canal = result.Data.Canal,
            statut = result.Data.Statut
        });
    }

    /// <summary>
    /// Remove symbols from existing subscription
    /// POST /api/v1/market/subscription/{subscriptionId}/remove-symbols
    /// Architecture hexagonale : gère les Result
    /// </summary>
    [HttpPost("subscription/{subscriptionId}/remove-symbols")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> RemoveSymbols(Guid subscriptionId, [FromBody] SymbolsRequest req, CancellationToken ct)
    {
        if (req.Symbols == null || !req.Symbols.Any())
        {
            return BadRequest(new { error = "Symbols are required" });
        }

        Log.Information("[UC04_REMOVE_SYMBOLS] SubscriptionId={SubscriptionId}, Symbols={Symbols}", 
            subscriptionId, string.Join(",", req.Symbols));

        var result = await _marketDataUseCase.RemoveSymbolsAsync(subscriptionId, req.Symbols, ct);

        if (!result.Success)
        {
            Log.Warning("[UC04_REMOVE_SYMBOLS] Échec: {ErrorCode} - {ErrorMessage}", 
                result.ErrorCode, result.ErrorMessage);
            return BadRequest(new 
            { 
                error = result.ErrorCode, 
                message = result.ErrorMessage 
            });
        }

        return Ok(new
        {
            subscriptionId = result.Data!.SubscriptionId,
            symbols = result.Data.Symbols,
            canal = result.Data.Canal,
            statut = result.Data.Statut
        });
    }
}

// Request DTOs
public record SubscribeRequest(Guid ClientId, List<string> Symbols, string? Canal);
public record UnsubscribeRequest(Guid SubscriptionId);
public record SymbolsRequest(List<string> Symbols);
public record QuotesRequest(List<string> Symbols);
