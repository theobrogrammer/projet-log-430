using Microsoft.EntityFrameworkCore;
using ProjetLog430.Domain.Model.Identite;
using ProjetLog430.Domain.Ports.Outbound;
using ProjetLog430.Infrastructure.Persistence;

namespace ProjetLog430.Infrastructure.Adapters.Repositories;

public sealed class InMemoryAccountRepository : IAccountRepository, ICompteRepository
{
    private readonly BrokerXDbContext _db;
    public InMemoryAccountRepository(BrokerXDbContext db) => _db = db;

    public Task<Compte?> GetByIdAsync(Guid accountId, CancellationToken ct = default)
        => _db.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.AccountId == accountId, ct);

    public Task<List<Compte>> GetByClientIdAsync(Guid clientId, CancellationToken ct = default)
        => _db.Accounts.AsNoTracking().Where(x => x.ClientId == clientId).ToListAsync(ct);

    public async Task AddAsync(Compte account, CancellationToken ct = default)
    {
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Compte account, CancellationToken ct = default)
    {
        _db.Accounts.Update(account);
        await _db.SaveChangesAsync(ct);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
