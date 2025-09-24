//OPTIONNEL!!!
//OPTIONNEL!!!
//OPTIONNEL!!!
//OPTIONNEL!!!

namespace ProjetLog430.Domain.Ports.Outbound;

using ProjetLog430.Domain.Model.Securite;

public interface ISessionRepository
{
    Task<Session?> GetByIdAsync(Guid sessionId, CancellationToken ct = default);
    Task AddAsync(Session session, CancellationToken ct = default);
    Task UpdateAsync(Session session, CancellationToken ct = default);
}
