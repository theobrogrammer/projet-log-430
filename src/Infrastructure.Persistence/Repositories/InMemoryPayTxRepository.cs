using Microsoft.EntityFrameworkCore;
using ProjetLog430.Domain.Model.PortefeuilleReglement;
using ProjetLog430.Domain.Ports.Outbound;
using ProjetLog430.Infrastructure.Persistence;

namespace ProjetLog430.Infrastructure.Adapters.Repositories;

public sealed class InMemoryPayTxRepository : IPayTxRepository
{
    private readonly BrokerXDbContext _db;
    public InMemoryPayTxRepository(BrokerXDbContext db) => _db = db;

    public Task<TransactionPaiement?> GetByIdAsync(Guid paymentTxId, CancellationToken ct = default)
        => _db.PayTxs.AsNoTracking().FirstOrDefaultAsync(x => x.PaymentTxId == paymentTxId, ct);

    public Task<TransactionPaiement?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
        => _db.PayTxs.AsNoTracking().FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, ct);

    public async Task AddAsync(TransactionPaiement tx, CancellationToken ct = default)
    {
        _db.PayTxs.Add(tx);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(TransactionPaiement tx, CancellationToken ct = default)
    {
        _db.PayTxs.Update(tx);
        await _db.SaveChangesAsync(ct);
    }
}
