using ProjetLog430.Domain.Model.Trading;

namespace ProjetLog430.Domain.Ports.Outbound;

/// <summary>
/// Port sortant pour la persistance des ordres.
/// Architecture Hexagonale : Interface définie dans Domain, implémentée dans Infrastructure.
/// </summary>
public interface IOrderRepository
{
    Task<Ordre?> GetByIdAsync(Guid orderId, CancellationToken ct = default);
    Task<Ordre?> GetByClientOrderIdAsync(Guid accountId, string clientOrderId, CancellationToken ct = default);
    Task<List<Ordre>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default);
    Task AddAsync(Ordre ordre, CancellationToken ct = default);
    Task UpdateAsync(Ordre ordre, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
