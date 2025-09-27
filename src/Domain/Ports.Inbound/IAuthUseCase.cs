namespace ProjetLog430.Application.Ports.Inbound;

using ProjetLog430.Application.Contracts;
public interface IAuthUseCase
{
    /// <summary>
    /// UC-02 : Authentification primaire (email+password).
    /// Si une PolitiqueMFA active existe, le résultat a MfaRequired=true et aucun token n'est émis.
    /// </summary>
    Task<LoginResult> LoginAsync(
        string email,
        string password,
        string? ip = null,
        string? device = null,
        CancellationToken ct = default);

    /// <summary>
    /// Validation d'un défi MFA (OTP/TOTP/WebAuthn).
    /// En cas de succès, émet la Session et renvoie le jeton.
    /// </summary>
    Task<LoginResult> VerifyMfaAsync(
        Guid clientId,
        Guid challengeId,
        string code,
        CancellationToken ct = default);
}
