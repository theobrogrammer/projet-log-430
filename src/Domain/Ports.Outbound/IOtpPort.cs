namespace ProjetLog430.Domain.Ports.Outbound;

using ProjetLog430.Domain.Model.Identite;

public interface IOtpPort
{
    /// <summary>Envoie un OTP de vérification de contact (email/SMS) pour un client.</summary>
    Task SendContactOtpAsync(
        Guid clientId,
        Guid otpId,
        CanalOTP canal,
        string destination,  // email ou numéro SMS
        string code,         // valeur OTP à transmettre
        CancellationToken ct = default);
}
