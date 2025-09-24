namespace ProjetLog430.Domain.Ports.Outbound;

using ProjetLog430.Domain.Model.Securite;

public interface IMfaPolicyRepository
{
    Task<PolitiqueMFA?> GetByClientIdAsync(Guid clientId, CancellationToken ct = default);
    Task AddAsync(PolitiqueMFA policy, CancellationToken ct = default);
    Task UpdateAsync(PolitiqueMFA policy, CancellationToken ct = default);
}
