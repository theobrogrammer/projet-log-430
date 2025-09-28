using BCrypt.Net;

namespace ProjetLog430.Domain.Model.Identite;


public sealed class Client
{
    public Guid ClientId { get; private set; }
    public string Email { get; private set; }
    public string? Telephone { get; private set; }
    public string NomComplet { get; private set; }
    public DateOnly? DateNaissance { get; private set; }
    public string PasswordHash { get; private set; } // Hash sécurisé du mot de passe
    public StatutClient Statut { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // Relations
    public DossierKYC? Kyc { get; private set; }

    private readonly List<VerifContactOTP> _contactOtps = new();
    public IReadOnlyCollection<VerifContactOTP> ContactOtps => _contactOtps.AsReadOnly();

    private readonly List<Compte> _comptes = new();
    public IReadOnlyCollection<Compte> Comptes => _comptes.AsReadOnly();

    // Constructeur privé pour Entity Framework
    private Client()
    {
        ClientId = Guid.NewGuid();
        Email = string.Empty;
        Telephone = null;
        NomComplet = string.Empty;
        DateNaissance = null;
        PasswordHash = string.Empty;
        Statut = StatutClient.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    private Client(Guid id, string email, string? telephone, string nomComplet, DateOnly? dateNaissance, string passwordHash)
    {
        ClientId = id;
        Email = ValiderEmail(email);
        Telephone = NormaliserTelephone(telephone);
        NomComplet = ExigerNonVide(nomComplet, nameof(nomComplet));
        DateNaissance = dateNaissance;
        PasswordHash = ExigerNonVide(passwordHash, nameof(passwordHash));
        Statut = StatutClient.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public static Client Creer(string email, string? telephone, string nomComplet, string passwordHash, DateOnly? dateNaissance = null)
        => new(Guid.NewGuid(), email, telephone, nomComplet, dateNaissance, passwordHash);

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

    public bool VerifyContactOtp(string code)
    {
        // Trouver l'OTP actif (Pending et non expiré)
        var activeOtp = _contactOtps
            .Where(o => o.Statut == StatutOTP.Pending && o.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault();

        if (activeOtp == null)
            throw new InvalidOperationException("Aucun OTP actif trouvé ou OTP expiré.");

        try 
        {
            activeOtp.Verifier(code); // Vérifie le code et change le statut à Verified
            Touch();
            
            // Tenter d'activer automatiquement si éligible
            try 
            {
                ActiverSiAdmissible();
            }
            catch (InvalidOperationException)
            {
                // KYC pas encore vérifié - normal, on continue
            }
            
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
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
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email invalide.", nameof(email));
        
        var trimmed = email.Trim();
        
        // Validation ultra-simple : juste un @ au milieu avec du contenu des deux côtés
        if (trimmed.Length < 3 || 
            !trimmed.Contains('@') ||
            trimmed.IndexOf('@') == 0 ||
            trimmed.LastIndexOf('@') == trimmed.Length - 1 ||
            trimmed.IndexOf('@') != trimmed.LastIndexOf('@'))
        {
            throw new ArgumentException("Email invalide.", nameof(email));
        }
        
        return trimmed.ToLowerInvariant();
    }

    private static string ExigerNonVide(string v, string nom)
    {
        if (string.IsNullOrWhiteSpace(v)) throw new ArgumentException($"{nom} requis.", nom);
        return v.Trim();
    }

    private static string? NormaliserTelephone(string? tel)
        => string.IsNullOrWhiteSpace(tel) ? null : tel.Trim();

    // Méthodes pour la gestion sécurisée des mots de passe
    public static string HashPassword(string plainTextPassword)
    {
        if (string.IsNullOrWhiteSpace(plainTextPassword))
            throw new ArgumentException("Le mot de passe ne peut pas être vide.", nameof(plainTextPassword));
        
        return BCrypt.Net.BCrypt.HashPassword(plainTextPassword, BCrypt.Net.BCrypt.GenerateSalt());
    }

    public bool VerifyPassword(string plainTextPassword)
    {
        if (string.IsNullOrWhiteSpace(plainTextPassword)) return false;
        return BCrypt.Net.BCrypt.Verify(plainTextPassword, PasswordHash);
    }

    public void UpdatePassword(string newPlainTextPassword)
    {
        PasswordHash = HashPassword(newPlainTextPassword);
        Touch();
    }
}
