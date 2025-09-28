// src/Infrastructure.Adapters/Session/JwtSessionAdapter.cs
using System.Text;
using ProjetLog430.Domain.Model.Securite;
using ProjetLog430.Domain.Ports.Outbound;


namespace ProjetLog430.Infrastructure.Adapters.Session;

public sealed class JwtSessionAdapter : ISessionPort
{
    public Task<string> IssueAsync(ProjetLog430.Domain.Model.Securite.Session session, CancellationToken ct = default)
    {
        // Démo: jeton light (NE PAS utiliser en prod)
        var payload = $"{session.SessionId}|{session.ExpiresAt:O}";
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
        return Task.FromResult(token);
    }

    public Task RevokeAsync(Guid sessionId, CancellationToken ct = default) => Task.CompletedTask;
}
