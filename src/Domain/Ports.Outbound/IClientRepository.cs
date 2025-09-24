namespace ProjetLog430.Domain.Ports.Outbound;

using ProjetLog430.Domain.Model.Identite;

public interface IClientRepository
{
    Task<Client?> GetByIdAsync(Guid clientId, CancellationToken ct = default);
    Task<Client?> GetByEmailAsync(string email, CancellationToken ct = default);

    Task AddAsync(Client client, CancellationToken ct = default);
    Task UpdateAsync(Client client, CancellationToken ct = default);
}
