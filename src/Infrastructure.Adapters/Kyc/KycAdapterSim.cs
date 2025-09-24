// src/Infrastructure.Adapters/Kyc/KycAdapterSim.cs
using ProjetLog430.Domain.Model.Identite;
using ProjetLog430.Domain.Ports.Outbound;

namespace ProjetLog430.Infrastructure.Adapters.Kyc;

public sealed class KycAdapterSim : IKycPort
{
    public Task SubmitAsync(Guid clientId, Guid kycId, CancellationToken ct = default)
    {
        // Démo: pas d’appel externe. On "accepte" toujours.
        return Task.CompletedTask;
    }

    public Task<StatutKYC> GetStatusAsync(Guid clientId, Guid kycId, CancellationToken ct = default)
        => Task.FromResult(StatutKYC.Verified);
}
