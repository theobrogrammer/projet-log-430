namespace ProjetLog430.Domain.Model.Securite;

public enum TypeJeton { Jwt, Opaque }

public sealed class Session
{
    public Guid SessionId { get; }
    public Guid ClientId  { get; }
    public TypeJeton TokenType { get; }
    public string Token { get; private set; }          // si tu stockes l’opacité/ID de session
    public DateTimeOffset IssuedAt { get; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public string? Ip { get; private set; }
    public string? Device { get; private set; }
    public bool Revoked { get; private set; }

    // Constructeur privé pour Entity Framework
    private Session()
    {
        SessionId = Guid.Empty;
        ClientId = Guid.Empty;
        TokenType = TypeJeton.Jwt;
        Token = string.Empty;
        IssuedAt = DateTimeOffset.MinValue;
        ExpiresAt = DateTimeOffset.MinValue;
        Ip = null;
        Device = null;
        Revoked = false;
    }

    private Session(Guid id, Guid clientId, TypeJeton type, string token, DateTimeOffset issuedAt,
                    DateTimeOffset expiresAt, string? ip, string? device)
    {
        SessionId = id;
        ClientId  = clientId;
        TokenType = type;
        Token     = string.IsNullOrWhiteSpace(token) ? throw new ArgumentException("token requis", nameof(token)) : token;
        IssuedAt  = issuedAt;
        ExpiresAt = expiresAt;
        Ip        = string.IsNullOrWhiteSpace(ip) ? null : ip;
        Device    = string.IsNullOrWhiteSpace(device) ? null : device;
        Revoked   = false;
    }

    public static Session Creer(Guid clientId, TypeJeton type, string token, TimeSpan ttl, string? ip = null, string? device = null)
        => new(Guid.NewGuid(), clientId, type, token, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.Add(ttl), ip, device);

    public bool EstExpiree(DateTimeOffset? maintenant = null)
        => Revoked || (maintenant ?? DateTimeOffset.UtcNow) >= ExpiresAt;

    public void Renouveler(TimeSpan ttl)
    {
        if (Revoked) throw new InvalidOperationException("Session révoquée.");
        ExpiresAt = DateTimeOffset.UtcNow.Add(ttl);
    }

    public void Revoquer()
    {
        Revoked = true;
    }

    public void MettreAJourContexte(string? ip, string? device)
    {
        Ip     = string.IsNullOrWhiteSpace(ip) ? Ip : ip;
        Device = string.IsNullOrWhiteSpace(device) ? Device : device;
    }
}
