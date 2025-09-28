using ProjetLog430.Domain.Model.Securite;
using ProjetLog430.Domain.Ports.Outbound;

namespace ProjetLog430.Infrastructure.Adapters.Repositories;

public sealed class InMemoryMfaChallengeRepository : IMfaChallengeRepository
{
    private readonly Dictionary<Guid, DefiMFA> _challenges = new();

    public Task<DefiMFA?> GetByIdAsync(Guid challengeId, CancellationToken ct = default)
    {
        _challenges.TryGetValue(challengeId, out var challenge);
        return Task.FromResult(challenge);
    }

    public Task AddAsync(DefiMFA challenge, CancellationToken ct = default)
    {
        _challenges[challenge.ChallengeId] = challenge;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(DefiMFA challenge, CancellationToken ct = default)
    {
        _challenges[challenge.ChallengeId] = challenge;
        return Task.CompletedTask;
    }
}