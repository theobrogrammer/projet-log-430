using ProjetLog430.Domain.Model.Identite;

namespace ProjetLog430.Domain.Ports.Outbound;

/// <summary>
/// Port sortant pour la persistance des comptes.
/// Architecture Hexagonale : Interface définie dans Domain, implémentée dans Infrastructure.
/// </summary>
public interface ICompteRepository
{
    Task<Compte?> GetByIdAsync(Guid accountId, CancellationToken ct = default);
    Task<List<Compte>> GetByClientIdAsync(Guid clientId, CancellationToken ct = default);
    Task AddAsync(Compte compte, CancellationToken ct = default);
    Task UpdateAsync(Compte compte, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
