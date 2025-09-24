namespace ProjetLog430.Domain.Model.Securite;

public enum TypeMfa { Totp, Sms, WebAuthn }

public sealed class PolitiqueMFA
{
    public Guid MfaId { get; }
    public Guid ClientId { get; }
    public TypeMfa Type { get; private set; }
    public bool EstActive { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private PolitiqueMFA(Guid mfaId, Guid clientId, TypeMfa type, bool active)
    {
        MfaId = mfaId;
        ClientId = clientId;
        Type = type;
        EstActive = active;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public static PolitiqueMFA Creer(Guid clientId, TypeMfa type, bool active = false)
        => new(Guid.NewGuid(), clientId, type, active);

    public void Activer()
    {
        if (EstActive) return;
        EstActive = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Desactiver()
    {
        if (!EstActive) return;
        EstActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ChangerType(TypeMfa nouveauType)
    {
        if (Type == nouveauType) return;
        Type = nouveauType;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
