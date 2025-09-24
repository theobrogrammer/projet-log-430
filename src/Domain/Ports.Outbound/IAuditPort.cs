namespace ProjetLog430.Domain.Ports.Outbound;

using ProjetLog430.Domain.Model.Observabilite;

public interface IAuditPort
{
    /// <summary>Persiste/écrit un log d'audit structuré.</summary>
    Task WriteAsync(
        AuditLog log,
        CancellationToken ct = default);
}
