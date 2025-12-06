using Microsoft.EntityFrameworkCore;
using ProjetLog430.Domain.Model.MarketData;
using ProjetLog430.Domain.Ports.Outbound;

namespace ProjetLog430.Infrastructure.Persistence.Repositories;

public sealed class InMemoryQuoteRepository : IQuoteRepository
{
    private readonly BrokerXDbContext _ctx;

    public InMemoryQuoteRepository(BrokerXDbContext ctx) => _ctx = ctx;

    public async Task<Quote?> GetLatestQuoteAsync(string symbol, CancellationToken ct = default)
    {
        return await _ctx.Quotes
            .Where(q => q.Symbol == symbol.ToUpper())
            .OrderByDescending(q => q.Timestamp)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<Quote>> GetQuoteHistoryAsync(string symbol, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        return await _ctx.Quotes
            .Where(q => q.Symbol == symbol.ToUpper() && q.Timestamp >= from && q.Timestamp <= to)
            .OrderBy(q => q.Timestamp)
            .ToListAsync(ct);
    }

    public async Task<List<Quote>> GetLatestQuotesAsync(List<string> symbols, CancellationToken ct = default)
    {
        var quotes = new List<Quote>();
        foreach (var symbol in symbols)
        {
            var quote = await GetLatestQuoteAsync(symbol, ct);
            if (quote != null)
            {
                quotes.Add(quote);
            }
        }
        return quotes;
    }

    public async Task AddAsync(Quote quote, CancellationToken ct = default)
    {
        _ctx.Quotes.Add(quote);
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Quote quote, CancellationToken ct = default)
    {
        _ctx.Quotes.Update(quote);
        await _ctx.SaveChangesAsync(ct);
    }
}
