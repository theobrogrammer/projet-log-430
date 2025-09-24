using System.Collections.Concurrent;
using ProjetLog430.Domain.Model.Identite;
using ProjetLog430.Domain.Ports.Outbound;

namespace ProjetLog430.Infrastructure.Adapters.Repositories;

public sealed class InMemoryClientRepository : IClientRepository
{
    private readonly ConcurrentDictionary<Guid, Client> _byId = new();
    private readonly ConcurrentDictionary<string, Guid> _byEmail = new(StringComparer.OrdinalIgnoreCase);

    public Task<Client?> GetByIdAsync(Guid clientId, CancellationToken ct = default)
        => Task.FromResult(_byId.TryGetValue(clientId, out var c) ? c : null);

    public Task<Client?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var key = email.Trim().ToLowerInvariant();
        return Task.FromResult(_byEmail.TryGetValue(key, out var id) && _byId.TryGetValue(id, out var c) ? c : null);
    }

    public Task AddAsync(Client client, CancellationToken ct = default)
    {
        _byId[client.ClientId] = client;
        _byEmail[client.Email] = client.ClientId;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Client client, CancellationToken ct = default)
    {
        _byId[client.ClientId] = client;
        _byEmail[client.Email] = client.ClientId;
        return Task.CompletedTask;
    }
}
