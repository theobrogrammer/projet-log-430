using System.Collections.Concurrent;
using ProjetLog430.Domain.Model.PortefeuilleReglement;
using ProjetLog430.Domain.Ports.Outbound;

namespace ProjetLog430.Infrastructure.Adapters.Repositories;

public sealed class InMemoryPortfolioRepository : IPortfolioRepository
{
    private readonly ConcurrentDictionary<Guid, Portefeuille> _byAccount = new();

    public Task<Portefeuille?> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default)
        => Task.FromResult(_byAccount.TryGetValue(accountId, out var p) ? p : null);

    public Task AddAsync(Portefeuille portfolio, CancellationToken ct = default)
    { _byAccount[portfolio.AccountId] = portfolio; return Task.CompletedTask; }

    public Task UpdateAsync(Portefeuille portfolio, CancellationToken ct = default)
    { _byAccount[portfolio.AccountId] = portfolio; return Task.CompletedTask; }
}
