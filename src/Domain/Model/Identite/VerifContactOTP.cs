namespace ProjetLog430.Domain.Model.Identite;

public enum CanalOTP { Email, Sms }
public enum StatutOTP { Pending, Verified, Expired }

public sealed class VerifContactOTP
{
    public Guid OtpId { get; private set; }
    public Guid ClientId { get; private set; }
    public CanalOTP Canal { get; private set; }
    public StatutOTP Statut { get; private set; }
    public string? CodeHash { get; private set; } // Hash sécurisé du code OTP
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // Constructeur privé pour Entity Framework
    private VerifContactOTP()
    {
        OtpId = Guid.NewGuid();
        ClientId = Guid.Empty;
        Canal = CanalOTP.Email;
        ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10);
        CreatedAt = DateTimeOffset.UtcNow;
        Statut = StatutOTP.Pending;
    }

    private VerifContactOTP(Guid otpId, Guid clientId, CanalOTP canal, DateTimeOffset expiresAt)
    {
        OtpId = otpId;
        ClientId = clientId;
        Canal = canal;
        ExpiresAt = expiresAt;
        CreatedAt = DateTimeOffset.UtcNow;
        Statut = StatutOTP.Pending;
    }

    public static VerifContactOTP Demarrer(Guid clientId, CanalOTP canal, TimeSpan ttl)
        => new(Guid.NewGuid(), clientId, canal, DateTimeOffset.UtcNow.Add(ttl));

    public void SetCodeHash(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Le code OTP ne peut pas être vide.", nameof(code));
        
        // Utiliser BCrypt pour hasher le code OTP (même principe que les mots de passe)
        CodeHash = BCrypt.Net.BCrypt.HashPassword(code);
    }

    public bool VerifyCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(CodeHash))
            return false;

        return BCrypt.Net.BCrypt.Verify(code, CodeHash);
    }

    public void Verifier(string code)
    {
        if (DateTimeOffset.UtcNow > ExpiresAt)
        {
            Statut = StatutOTP.Expired;
            throw new InvalidOperationException("OTP expiré.");
        }
        if (Statut != StatutOTP.Pending) 
            throw new InvalidOperationException("OTP non en attente.");
        if (!VerifyCode(code))
            throw new InvalidOperationException("Code OTP invalide.");
            
        Statut = StatutOTP.Verified;
    }

    public void Expirer()
    {
        if (Statut == StatutOTP.Verified) return;
        Statut = StatutOTP.Expired;
    }
}
