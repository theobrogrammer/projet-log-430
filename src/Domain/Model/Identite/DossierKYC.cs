namespace ProjetLog430.Domain.Model.Identite;

public enum StatutKYC { Pending, Verified, Rejected }

public sealed class DossierKYC
{
    public Guid KycId { get; }
    public Guid ClientId { get; }
    public string Niveau { get; private set; } = "Basic";
    public StatutKYC Statut { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private DossierKYC(Guid kycId, Guid clientId)
    {
        KycId = kycId;
        ClientId = clientId;
        Statut = StatutKYC.Pending;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static DossierKYC Ouvrir(Guid clientId) => new(Guid.NewGuid(), clientId);

    public void MarquerVerifie(string niveau = "Basic")
    {
        Niveau = string.IsNullOrWhiteSpace(niveau) ? "Basic" : niveau;
        Statut = StatutKYC.Verified;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarquerRejete()
    {
        Statut = StatutKYC.Rejected;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
