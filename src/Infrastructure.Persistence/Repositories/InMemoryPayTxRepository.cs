using System.Collections.Concurrent;
using ProjetLog430.Domain.Model.PortefeuilleReglement;
using ProjetLog430.Domain.Ports.Outbound;

namespace ProjetLog430.Infrastructure.Adapters.Repositories;

public sealed class InMemoryPayTxRepository : IPayTxRepository
{
    private readonly ConcurrentDictionary<Guid, TransactionPaiement> _byId = new();
    private readonly ConcurrentDictionary<string, Guid> _byIdem = new(StringComparer.Ordinal);

    public Task<TransactionPaiement?> GetByIdAsync(Guid paymentTxId, CancellationToken ct = default)
        => Task.FromResult(_byId.TryGetValue(paymentTxId, out var tx) ? tx : null);

    public Task<TransactionPaiement?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
        => Task.FromResult(_byIdem.TryGetValue(idempotencyKey, out var id) && _byId.TryGetValue(id, out var tx) ? tx : null);

    public Task AddAsync(TransactionPaiement tx, CancellationToken ct = default)
    { _byId[tx.PaymentTxId] = tx; _byIdem[tx.IdempotencyKey] = tx.PaymentTxId; return Task.CompletedTask; }

    public Task UpdateAsync(TransactionPaiement tx, CancellationToken ct = default)
    { _byId[tx.PaymentTxId] = tx; return Task.CompletedTask; }
}
