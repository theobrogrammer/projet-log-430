namespace ProjetLog430.Domain.Ports.Outbound;

using ProjetLog430.Domain.Model.Securite;

public interface IMfaChallengeRepository
{
    Task<DefiMFA?> GetByIdAsync(Guid challengeId, CancellationToken ct = default);
    Task AddAsync(DefiMFA challenge, CancellationToken ct = default);
    Task UpdateAsync(DefiMFA challenge, CancellationToken ct = default);
}
