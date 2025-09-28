// src/Application/Services/SignupService.cs
using ProjetLog430.Domain.Contracts;
using ProjetLog430.Domain.Ports.Inbound;
using ProjetLog430.Domain.Model.Identite;
using ProjetLog430.Domain.Model.PortefeuilleReglement;
using ProjetLog430.Domain.Ports.Outbound;
using ProjetLog430.Domain.Model.Observabilite;

namespace ProjetLog430.Application.Services;

public sealed class SignupService : ISignupUseCase
{
    private readonly IClientRepository _clients;
    private readonly IAccountRepository _comptes;
    private readonly IPortfolioRepository _portefeuilles;
    private readonly IKycPort _kyc;
    private readonly IOtpPort _otp;
    private readonly IAuditPort _audit;

    public SignupService(
        IClientRepository clients,
        IAccountRepository comptes,
        IPortfolioRepository portefeuilles,
        IKycPort kyc,
        IOtpPort otp,
        IAuditPort audit)
    {
        _clients = clients;
        _comptes = comptes;
        _portefeuilles = portefeuilles;
        _kyc = kyc;
        _otp = otp;
        _audit = audit;
    }

    public async Task<SignupResult> CreateAccountAsync(
        string email,
        string? phone,
        string fullName,
        string password,
        DateOnly? birthDate,
        CancellationToken ct = default)
    {
        // 1) Créer le client (Pending), dossier KYC & OTP de contact
        var passwordHash = Client.HashPassword(password);
        var client = Client.Creer(email, phone, fullName, passwordHash, birthDate);
        client.DemarrerKycSiNecessaire();
        var otp = client.DemarrerOtpActivation(CanalOTP.Email, TimeSpan.FromMinutes(10)); // email par défaut

        // 2) Persister client
        await _clients.AddAsync(client, ct);

        // 3) Ouvrir compte + portefeuille (devise de démo USD)
        var compte = client.OuvrirCompte();
        await _comptes.AddAsync(compte, ct);

        var portefeuille = Portefeuille.Ouvrir(compte.AccountId, "USD");
        await _portefeuilles.AddAsync(portefeuille, ct);

        // 4) Déclencher KYC (simulateur) + envoyer OTP de contact
        _ = _kyc.SubmitAsync(client.ClientId, client.Kyc!.KycId, ct); // fire-and-forget (démo)
        var code = GenererCode6();
        otp.SetCodeHash(code); // Stocker le hash du code dans l'OTP
        
        // IMPORTANT: Sauvegarder le client avec l'OTP mis à jour (hash du code)
        await _clients.UpdateAsync(client, ct);
        
        await _otp.SendContactOtpAsync(client.ClientId, otp.OtpId, CanalOTP.Email, email, code, ct);

        // 5) Audit
        await _audit.WriteAsync(
           
            AuditLog.Ecrire("CLIENT_SIGNUP", $"user:{email}",
                payload: new { clientId = client.ClientId, accountId = compte.AccountId }), ct);

        return new SignupResult(client.ClientId, compte.AccountId, client.Statut.ToString());
    }

    public async Task ResendContactOtpAsync(Guid clientId, CancellationToken ct = default)
    {
        var client = await _clients.GetByIdAsync(clientId, ct) ?? throw new InvalidOperationException("Client inconnu.");
        var otp = client.DemarrerOtpActivation(CanalOTP.Email, TimeSpan.FromMinutes(10));
        await _clients.UpdateAsync(client, ct);

        var code = GenererCode6();
        otp.SetCodeHash(code); // Stocker le hash du code dans l'OTP
        await _otp.SendContactOtpAsync(client.ClientId, otp.OtpId, CanalOTP.Email, client.Email, code, ct);

        await _audit.WriteAsync(
            AuditLog.Ecrire("CONTACT_OTP_RESENT", $"user:{client.Email}",
                payload: new { clientId }), ct);
    }

    public async Task<OtpVerificationResult> VerifyContactOtpAsync(Guid clientId, string code, CancellationToken ct = default)
    {
        try
        {
            var client = await _clients.GetByIdAsync(clientId, ct) ?? 
                throw new InvalidOperationException("Client inconnu.");

            // Vérifier le code OTP avec le domaine
            var isValid = client.VerifyContactOtp(code);
            if (!isValid)
                return OtpVerificationResult.Failed("Code OTP invalide ou expiré.");

            // Sauvegarder les changements (statut OTP + potentiellement statut Client)
            await _clients.UpdateAsync(client, ct);

            // Audit de la vérification OTP
            await _audit.WriteAsync(
                AuditLog.Ecrire("CONTACT_OTP_VERIFIED", $"user:{client.Email}",
                    payload: new { clientId, newStatus = client.Statut.ToString() }), ct);

            return OtpVerificationResult.Succeed(client.Statut.ToString());
        }
        catch (Exception ex)
        {
            await _audit.WriteAsync(
                AuditLog.Ecrire("CONTACT_OTP_VERIFICATION_FAILED", $"client:{clientId}",
                    payload: new { clientId, error = ex.Message }), ct);
                    
            return OtpVerificationResult.Failed(ex.Message);
        }
    }

    private static string GenererCode6() => Random.Shared.Next(0, 999_999).ToString("D6");
}
