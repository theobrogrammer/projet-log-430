using Microsoft.EntityFrameworkCore;
using ProjetLog430.Domain.Model.Trading;
using ProjetLog430.Domain.Ports.Outbound;

namespace ProjetLog430.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository pour la persistance des ordres (UC-05).
/// Architecture Hexagonale : Implémente IOrderRepository (Port Outbound).
/// </summary>
public sealed class OrderRepository : IOrderRepository
{
    private readonly BrokerXDbContext _context;

    public OrderRepository(BrokerXDbContext context)
    {
        _context = context;
    }

    public async Task<Ordre?> GetByIdAsync(Guid orderId, CancellationToken ct = default)
    {
        return await _context.Ordres
            .Include(o => o.Executions)
            .FirstOrDefaultAsync(o => o.OrderId == orderId, ct);
    }

    public async Task<Ordre?> GetByClientOrderIdAsync(Guid accountId, string clientOrderId, CancellationToken ct = default)
    {
        return await _context.Ordres
            .Include(o => o.Executions)
            .FirstOrDefaultAsync(o => o.AccountId == accountId && o.ClientOrderId == clientOrderId, ct);
    }

    public async Task<List<Ordre>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default)
    {
        return await _context.Ordres
            .Include(o => o.Executions)
            .Where(o => o.AccountId == accountId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Ordre ordre, CancellationToken ct = default)
    {
        await _context.Ordres.AddAsync(ordre, ct);
    }

    public Task UpdateAsync(Ordre ordre, CancellationToken ct = default)
    {
        _context.Ordres.Update(ordre);
        return Task.CompletedTask;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }
}
