using ProjetLog430.Domain.Model.Securite;
using ProjetLog430.Domain.Ports.Outbound;

namespace ProjetLog430.Infrastructure.Adapters.Repositories;

public sealed class InMemorySessionRepository : ISessionRepository
{
    private readonly Dictionary<Guid, Session> _sessions = new();

    public Task<Session?> GetByIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public Task AddAsync(Session session, CancellationToken ct = default)
    {
        _sessions[session.SessionId] = session;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Session session, CancellationToken ct = default)
    {
        _sessions[session.SessionId] = session;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid sessionId, CancellationToken ct = default)
    {
        _sessions.Remove(sessionId);
        return Task.CompletedTask;
    }
}