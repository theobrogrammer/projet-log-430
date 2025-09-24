namespace ProjetLog430.Domain.Model.Identite;

public enum CanalOTP { Email, Sms }
public enum StatutOTP { Pending, Verified, Expired }

public sealed class VerifContactOTP
{
    public Guid OtpId { get; }
    public Guid ClientId { get; }
    public CanalOTP Canal { get; }
    public StatutOTP Statut { get; private set; }
    public DateTimeOffset ExpiresAt { get; }
    public DateTimeOffset CreatedAt { get; }

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

    public void Verifier()
    {
        if (DateTimeOffset.UtcNow > ExpiresAt)
        {
            Statut = StatutOTP.Expired;
            throw new InvalidOperationException("OTP expiré.");
        }
        if (Statut != StatutOTP.Pending) throw new InvalidOperationException("OTP non en attente.");
        Statut = StatutOTP.Verified;
    }

    public void Expirer()
    {
        if (Statut == StatutOTP.Verified) return;
        Statut = StatutOTP.Expired;
    }
}
