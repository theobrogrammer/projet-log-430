using Microsoft.EntityFrameworkCore;
using ProjetLog430.Domain.Model.Securite;
using ProjetLog430.Domain.Ports.Outbound;
using ProjetLog430.Infrastructure.Persistence;

namespace ProjetLog430.Infrastructure.Adapters.Repositories;

public sealed class InMemoryMfaChallengeRepository : IMfaChallengeRepository
{
    private readonly BrokerXDbContext _context;

    public InMemoryMfaChallengeRepository(BrokerXDbContext context)
    {
        _context = context;
    }

    public async Task<DefiMFA?> GetByIdAsync(Guid challengeId, CancellationToken ct = default)
    {
        return await _context.MfaChallenges
            .FirstOrDefaultAsync(c => c.ChallengeId == challengeId, ct);
    }

    public async Task AddAsync(DefiMFA challenge, CancellationToken ct = default)
    {
        _context.MfaChallenges.Add(challenge);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(DefiMFA challenge, CancellationToken ct = default)
    {
        _context.MfaChallenges.Update(challenge);
        await _context.SaveChangesAsync(ct);
    }
}