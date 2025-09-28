namespace ProjetLog430.Domain.Ports.Outbound;

using ProjetLog430.Domain.Model.PortefeuilleReglement;

public interface IPortfolioRepository
{
    Task<Portefeuille?> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default);
    Task AddAsync(Portefeuille portefeuille, CancellationToken ct = default);
    Task UpdateAsync(Portefeuille portefeuille, CancellationToken ct = default);
}
