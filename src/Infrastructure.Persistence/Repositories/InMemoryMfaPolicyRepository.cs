using ProjetLog430.Domain.Model.Securite;
using ProjetLog430.Domain.Ports.Outbound;

namespace ProjetLog430.Infrastructure.Adapters.Repositories;

public sealed class InMemoryMfaPolicyRepository : IMfaPolicyRepository
{
    private readonly Dictionary<Guid, PolitiqueMFA> _policies = new();

    public Task<PolitiqueMFA?> GetByClientIdAsync(Guid clientId, CancellationToken ct = default)
    {
        var policy = _policies.Values.FirstOrDefault(p => p.ClientId == clientId);
        return Task.FromResult(policy);
    }

    public Task AddAsync(PolitiqueMFA policy, CancellationToken ct = default)
    {
        _policies[policy.MfaId] = policy;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(PolitiqueMFA policy, CancellationToken ct = default)
    {
        _policies[policy.MfaId] = policy;
        return Task.CompletedTask;
    }
}