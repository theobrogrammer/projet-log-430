using System.Collections.Concurrent;
using ProjetLog430.Domain.Model.Identite;
using ProjetLog430.Domain.Ports.Outbound;

namespace ProjetLog430.Infrastructure.Adapters.Repositories;

public sealed class InMemoryAccountRepository : IAccountRepository
{
    private readonly ConcurrentDictionary<Guid, Compte> _byId = new();

    public Task<Compte?> GetByIdAsync(Guid accountId, CancellationToken ct = default)
        => Task.FromResult(_byId.TryGetValue(accountId, out var a) ? a : null);

    public Task AddAsync(Compte account, CancellationToken ct = default)
    { _byId[account.AccountId] = account; return Task.CompletedTask; }

    public Task UpdateAsync(Compte account, CancellationToken ct = default)
    { _byId[account.AccountId] = account; return Task.CompletedTask; }
}
