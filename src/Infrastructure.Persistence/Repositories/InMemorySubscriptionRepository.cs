using Microsoft.EntityFrameworkCore;
using ProjetLog430.Domain.Model.MarketData;
using ProjetLog430.Domain.Ports.Outbound;

namespace ProjetLog430.Infrastructure.Persistence.Repositories;

public sealed class InMemorySubscriptionRepository : ISubscriptionRepository
{
    private readonly BrokerXDbContext _ctx;

    public InMemorySubscriptionRepository(BrokerXDbContext ctx) => _ctx = ctx;

    public async Task<Subscription?> GetByIdAsync(Guid subscriptionId, CancellationToken ct = default)
    {
        return await _ctx.Subscriptions.FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId, ct);
    }

    public async Task<List<Subscription>> GetByClientIdAsync(Guid clientId, CancellationToken ct = default)
    {
        return await _ctx.Subscriptions
            .Where(s => s.ClientId == clientId)
            .ToListAsync(ct);
    }

    public async Task<List<Subscription>> GetActiveSubscriptionsAsync(CancellationToken ct = default)
    {
        return await _ctx.Subscriptions
            .Where(s => s.Statut == StatutSubscription.Active)
            .ToListAsync(ct);
    }

    public async Task<List<Subscription>> GetSubscriptionsBySymbolAsync(string symbol, CancellationToken ct = default)
    {
        var symbolUpper = symbol.ToUpper();
        // This is a simplified filter - in production, you'd need better JSON querying or a separate table
        return await _ctx.Subscriptions
            .Where(s => s.Statut == StatutSubscription.Active)
            .ToListAsync(ct)
            .ContinueWith(task => task.Result.Where(s => s.EstAbonneA(symbolUpper)).ToList(), ct);
    }

    public async Task AddAsync(Subscription subscription, CancellationToken ct = default)
    {
        _ctx.Subscriptions.Add(subscription);
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Subscription subscription, CancellationToken ct = default)
    {
        _ctx.Subscriptions.Update(subscription);
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid subscriptionId, CancellationToken ct = default)
    {
        var subscription = await GetByIdAsync(subscriptionId, ct);
        if (subscription != null)
        {
            _ctx.Subscriptions.Remove(subscription);
            await _ctx.SaveChangesAsync(ct);
        }
    }
}
