namespace ProjetLog430.Domain.Ports.Outbound;

using ProjetLog430.Domain.Model.Identite;

public interface ICompteRepository
{
    Task<Compte?> GetByIdAsync(Guid accountId, CancellationToken ct = default);
    Task AddAsync(Compte compte, CancellationToken ct = default);
    Task UpdateAsync(Compte compte, CancellationToken ct = default);
}
