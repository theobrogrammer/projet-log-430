namespace ProjetLog430.Domain.Ports.Outbound;

using ProjetLog430.Domain.Model.PortefeuilleReglement;

public interface IPayTxRepository
{
    Task<TransactionPaiement?> GetByIdAsync(Guid paymentTxId, CancellationToken ct = default);

    /// <summary>Pour idempotence : retrouver la PayTx par IdempotencyKey.</summary>
    Task<TransactionPaiement?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);

    Task AddAsync(TransactionPaiement tx, CancellationToken ct = default);
    Task UpdateAsync(TransactionPaiement tx, CancellationToken ct = default);
}
