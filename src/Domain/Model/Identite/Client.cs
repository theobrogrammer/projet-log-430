namespace ProjetLog430.Domain.Model.Identite;

public enum StatutClient { Pending, Active, Rejected }

public sealed class Client
{
    public Guid ClientId { get; }
    public string Email { get; private set; }
    public string? Telephone { get; private set; }
    public string NomComplet { get; private set; }
    public DateOnly? DateNaissance { get; private set; }
    public StatutClient Statut { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // Relations
    public DossierKYC? Kyc { get; private set; }

    private readonly List<VerifContactOTP> _contactOtps = new();
    public IReadOnlyCollection<VerifContactOTP> ContactOtps => _contactOtps.AsReadOnly();

    private readonly List<Compte> _comptes = new();
    public IReadOnlyCollection<Compte> Comptes => _comptes.AsReadOnly();

    private Client(Guid id, string email, string? telephone, string nomComplet, DateOnly? dateNaissance)
    {
        ClientId = id;
        Email = ValiderEmail(email);
        Telephone = NormaliserTelephone(telephone);
        NomComplet = ExigerNonVide(nomComplet, nameof(nomComplet));
        DateNaissance = dateNaissance;
        Statut = StatutClient.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public static Client Creer(string email, string? telephone, string nomComplet, DateOnly? dateNaissance = null)
        => new(Guid.NewGuid(), email, telephone, nomComplet, dateNaissance);

    public void DemarrerKycSiNecessaire()
    {
        if (Kyc is null) Kyc = DossierKYC.Ouvrir(ClientId);
        Touch();
    }

    public VerifContactOTP DemarrerOtpActivation(CanalOTP canal, TimeSpan ttl)
    {
        var otp = VerifContactOTP.Demarrer(ClientId, canal, ttl);
        _contactOtps.Add(otp);
        Touch();
        return otp;
    }

    public void ActiverSiAdmissible()
    {
        if (Statut == StatutClient.Active) return;
        if (Kyc?.Statut != StatutKYC.Verified) throw new InvalidOperationException("KYC non vérifié.");
        if (!_contactOtps.Any(o => o.Statut == StatutOTP.Verified))
            throw new InvalidOperationException("OTP de contact non vérifié.");
        Statut = StatutClient.Active;
        Touch();
    }

    public Compte OuvrirCompte()
    {
        if (Statut == StatutClient.Rejected) throw new InvalidOperationException("Client rejeté.");
        var compte = Compte.Ouvrir(this);
        _comptes.Add(compte);
        Touch();
        return compte;
    }

    public void Rejeter(string raison = "KYC échoué")
    {
        Statut = StatutClient.Rejected;
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    private static string ValiderEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ArgumentException("Email invalide.", nameof(email));
        return email.Trim().ToLowerInvariant();
    }

    private static string ExigerNonVide(string v, string nom)
    {
        if (string.IsNullOrWhiteSpace(v)) throw new ArgumentException($"{nom} requis.", nom);
        return v.Trim();
    }

    private static string? NormaliserTelephone(string? tel)
        => string.IsNullOrWhiteSpace(tel) ? null : tel.Trim();
}
