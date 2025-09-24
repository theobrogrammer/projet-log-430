namespace ProjetLog430.Domain.Ports.Outbound;

using ProjetLog430.Domain.Model.Securite;

public interface ISessionPort
{
    /// <summary>Émet le jeton (JWT/opaque) pour une Session créée côté domaine.</summary>
    Task<string> IssueAsync(
        Session session,
        CancellationToken ct = default);

    /// <summary>Révoque le jeton/Session.</summary>
    Task RevokeAsync(
        Guid sessionId,
        CancellationToken ct = default);
}
