namespace ProjetLog430.Domain.Ports.Outbound;

using ProjetLog430.Domain.Model.MarketData;

/// <summary>
/// Repository pour les cotations de marché (UC-04)
/// </summary>
public interface IQuoteRepository
{
    Task<Quote?> GetLatestQuoteAsync(string symbol, CancellationToken ct = default);
    Task<List<Quote>> GetLatestQuotesAsync(List<string> symbols, CancellationToken ct = default);
    Task<List<Quote>> GetQuoteHistoryAsync(string symbol, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
    Task AddAsync(Quote quote, CancellationToken ct = default);
    Task UpdateAsync(Quote quote, CancellationToken ct = default);
}

/// <summary>
/// Repository pour les abonnements clients aux données de marché (UC-04)
/// </summary>
public interface ISubscriptionRepository
{
    Task<Subscription?> GetByIdAsync(Guid subscriptionId, CancellationToken ct = default);
    Task<List<Subscription>> GetByClientIdAsync(Guid clientId, CancellationToken ct = default);
    Task<List<Subscription>> GetActiveSubscriptionsAsync(CancellationToken ct = default);
    Task AddAsync(Subscription subscription, CancellationToken ct = default);
    Task UpdateAsync(Subscription subscription, CancellationToken ct = default);
}
