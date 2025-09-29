using Microsoft.EntityFrameworkCore;
using ProjetLog430.Domain.Model.Securite;
using ProjetLog430.Domain.Ports.Outbound;
using ProjetLog430.Infrastructure.Persistence;

namespace ProjetLog430.Infrastructure.Adapters.Repositories;

public sealed class InMemoryMfaPolicyRepository : IMfaPolicyRepository
{
    private readonly BrokerXDbContext _context;

    public InMemoryMfaPolicyRepository(BrokerXDbContext context)
    {
        _context = context;
    }

    public async Task<PolitiqueMFA?> GetByClientIdAsync(Guid clientId, CancellationToken ct = default)
    {
        return await _context.MfaPolicies
            .FirstOrDefaultAsync(p => p.ClientId == clientId, ct);
    }

    public async Task AddAsync(PolitiqueMFA policy, CancellationToken ct = default)
    {
        _context.MfaPolicies.Add(policy);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(PolitiqueMFA policy, CancellationToken ct = default)
    {
        _context.MfaPolicies.Update(policy);
        await _context.SaveChangesAsync(ct);
    }
}