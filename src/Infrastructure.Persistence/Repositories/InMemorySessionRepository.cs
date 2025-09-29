using Microsoft.EntityFrameworkCore;
using ProjetLog430.Domain.Model.Securite;
using ProjetLog430.Domain.Ports.Outbound;
using ProjetLog430.Infrastructure.Persistence;

namespace ProjetLog430.Infrastructure.Adapters.Repositories;

public sealed class InMemorySessionRepository : ISessionRepository
{
    private readonly BrokerXDbContext _context;

    public InMemorySessionRepository(BrokerXDbContext context)
    {
        _context = context;
    }

    public async Task<Session?> GetByIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await _context.Sessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);
    }

    public async Task AddAsync(Session session, CancellationToken ct = default)
    {
        await _context.Sessions.AddAsync(session, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Session session, CancellationToken ct = default)
    {
        _context.Sessions.Update(session);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _context.Sessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);
        
        if (session != null)
        {
            _context.Sessions.Remove(session);
            await _context.SaveChangesAsync(ct);
        }
    }
}