namespace ProjetLog430.Domain.Ports.Inbound;

using ProjetLog430.Domain.Contracts;

public interface ISignupUseCase
{
    /// <summary>
    /// UC-01 : Inscription — crée le Client, ouvre le DossierKYC, lance un OTP de contact,
    /// et crée un Compte + Portefeuille associé (statut client "Pending" tant que KYC/OTP non validés).
    /// </summary>
    Task<SignupResult> CreateAccountAsync(
        string email,
        string? phone,
        string fullName,
        string password,
        DateOnly? birthDate,
        CancellationToken ct = default);

    /// <summary>
    /// Relance l'envoi d'un OTP de vérification de contact pour un client.
    /// </summary>
    Task ResendContactOtpAsync(
        Guid clientId,
        CancellationToken ct = default);

    /// <summary>
    /// Vérifie le code OTP saisi par l'utilisateur et active le compte si éligible.
    /// </summary>
    Task<OtpVerificationResult> VerifyContactOtpAsync(
        Guid clientId,
        string code,
        CancellationToken ct = default);
}
