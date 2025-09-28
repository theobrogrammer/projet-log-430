namespace ProjetLog430.Domain.Model.Identite;


public sealed class DossierKYC
{
    public Guid KycId { get; private set; }
    public Guid ClientId { get; private set; }
    public string Niveau { get; private set; } = "Basic";
    public StatutKYC Statut { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // Constructeur privé pour Entity Framework
    private DossierKYC()
    {
        KycId = Guid.NewGuid();
        ClientId = Guid.Empty;
        Niveau = "Basic";
        Statut = StatutKYC.Pending;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

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
