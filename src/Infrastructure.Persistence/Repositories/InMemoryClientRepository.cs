using Microsoft.EntityFrameworkCore;
using ProjetLog430.Domain.Model.Identite;
using ProjetLog430.Domain.Ports.Outbound;
using ProjetLog430.Infrastructure.Persistence;

namespace ProjetLog430.Infrastructure.Adapters.Repositories;

public sealed class InMemoryClientRepository : IClientRepository
{
    private readonly BrokerXDbContext _db;
    public InMemoryClientRepository(BrokerXDbContext db) => _db = db;

    public Task<Client?> GetByIdAsync(Guid clientId, CancellationToken ct = default)
        => _db.Clients
            .Include(c => c.ContactOtps) // Charger les OTPs liés
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ClientId == clientId, ct);

    public Task<Client?> GetByEmailAsync(string email, CancellationToken ct = default)
        => _db.Clients
            .Include(c => c.ContactOtps) // Charger les OTPs liés
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Email == email, ct);

    public async Task AddAsync(Client client, CancellationToken ct = default)
    {
        _db.Clients.Add(client);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Client client, CancellationToken ct = default)
    {
        _db.Clients.Update(client);
        await _db.SaveChangesAsync(ct);
    }
}
