namespace ProjetLog430.Domain.Ports.Outbound;

using ProjetLog430.Domain.Model.Identite;

public interface IKycPort
{
    /// <summary>Soumet un dossier KYC basique au fournisseur (simulation).</summary>
    Task SubmitAsync(
        Guid clientId,
        Guid kycId,
        CancellationToken ct = default);

    /// <summary>Interroge l'état courant du KYC (Pending/Verified/Rejected).</summary>
    Task<StatutKYC> GetStatusAsync(
        Guid clientId,
        Guid kycId,
        CancellationToken ct = default);
}
