using Microsoft.EntityFrameworkCore;
using ProjetLog430.Domain.Model.PortefeuilleReglement;
using ProjetLog430.Domain.Ports.Outbound;
using ProjetLog430.Infrastructure.Persistence;

namespace ProjetLog430.Infrastructure.Adapters.Repositories;

public sealed class InMemoryPortfolioRepository : IPortfolioRepository
{
    private readonly BrokerXDbContext _db;
    public InMemoryPortfolioRepository(BrokerXDbContext db) => _db = db;

    public Task<Portefeuille?> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default)
        => _db.Portfolios.AsNoTracking().FirstOrDefaultAsync(x => x.AccountId == accountId, ct);

    public async Task AddAsync(Portefeuille portfolio, CancellationToken ct = default)
    {
        _db.Portfolios.Add(portfolio);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Portefeuille portfolio, CancellationToken ct = default)
    {
        _db.Portfolios.Update(portfolio);
        await _db.SaveChangesAsync(ct);
    }
}
