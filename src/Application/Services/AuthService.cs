// src/Application/Services/AuthService.cs
using ProjetLog430.Domain.Contracts;
using ProjetLog430.Domain.Ports.Inbound;
using ProjetLog430.Domain.Model.Securite;
using ProjetLog430.Domain.Ports.Outbound;
using ProjetLog430.Domain.Model.Observabilite;


namespace ProjetLog430.Application.Services;

public sealed class AuthService : IAuthUseCase
{
    private readonly IClientRepository _clients;
    private readonly IMfaPolicyRepository _mfaPolicies;
    private readonly IMfaChallengeRepository _mfaChallenges;
    private readonly ISessionRepository _sessions;
    private readonly ISessionPort _sessionPort;
    private readonly IOtpPort _otp;       // réutilisé si MFA par OTP SMS/Email
    private readonly IAuditPort _audit;

    public AuthService(
        IClientRepository clients,
        IMfaPolicyRepository mfaPolicies,
        IMfaChallengeRepository mfaChallenges,
        ISessionRepository sessions,
        ISessionPort sessionPort,
        IOtpPort otp,
        IAuditPort audit)
    {
        _clients = clients;
        _mfaPolicies = mfaPolicies;
        _mfaChallenges = mfaChallenges;
        _sessions = sessions;
        _sessionPort = sessionPort;
        _otp = otp;
        _audit = audit;
    }

    public async Task<LoginResult> LoginAsync(
        string email,
        string password,
        string? ip = null,
        string? device = null,
        CancellationToken ct = default)
    {
        var client = await _clients.GetByEmailAsync(email, ct) ?? throw new InvalidOperationException("Identifiants invalides.");
        // TODO: vérifier le mot de passe (port d’identités/cred si tu en ajoutes un). Phase 1: on simplifie.

        // MFA ?
        var policy = await _mfaPolicies.GetByClientIdAsync(client.ClientId, ct);
        if (policy?.EstActive == true)
        {
            var ttl = TimeSpan.FromMinutes(5);
            var challenge = DefiMFA.Demarrer(client.ClientId, policy.Type, ttl);
            await _mfaChallenges.AddAsync(challenge, ct);

            // Si MFA = SMS/Email, envoi d’un code via le même port OTP (démo)
            if (policy.Type is TypeMfa.Sms or TypeMfa.Totp)
            {
                var code = GenererCode6();
                // Ici on n’a pas la destination SMS ; pour la démo, on trace via audit.
                await _otp.SendContactOtpAsync(client.ClientId, challenge.ChallengeId, CanalOTP.Email, email, code, ct);
            }

            await _audit.WriteAsync(
                AuditLog.Ecrire("AUTH_MFA_CHALLENGE", $"user:{email}",
                    payload: new { clientId = client.ClientId, challengeId = challenge.ChallengeId, policy = policy.Type.ToString() }), ct);

            // On signale juste que le MFA est requis (token non émis à ce stade).
            return new LoginResult(Token: string.Empty, MfaRequired: true);
        }

        // Sinon : émettre la session tout de suite
        var sess = Session.Creer(client.ClientId, TypeJeton.Jwt, token: Guid.NewGuid().ToString("N"), ttl: TimeSpan.FromHours(2), ip, device);
        await _sessions.AddAsync(sess, ct);
        var token = await _sessionPort.IssueAsync(sess, ct);

        await _audit.WriteAsync(
          AuditLog.Ecrire("AUTH_LOGIN", $"user:{email}", payload: new { clientId = client.ClientId, sessionId = sess.SessionId }), ct);

        return new LoginResult(Token: token, MfaRequired: false);
    }

    public async Task<LoginResult> VerifyMfaAsync(Guid clientId, Guid challengeId, string code, CancellationToken ct = default)
    {
        var challenge = await _mfaChallenges.GetByIdAsync(challengeId, ct)
            ?? throw new InvalidOperationException("Défi inconnu.");

        // Démo : on accepte tout code non vide (tu pourras brancher une vraie vérif plus tard)
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Code MFA manquant.");

        challenge.Reussir();
        await _mfaChallenges.UpdateAsync(challenge, ct);

        var sess = Session.Creer(clientId, TypeJeton.Jwt, token: Guid.NewGuid().ToString("N"), ttl: TimeSpan.FromHours(2));
        await _sessions.AddAsync(sess, ct);
        var token = await _sessionPort.IssueAsync(sess, ct);

        await _audit.WriteAsync(
            AuditLog.Ecrire("AUTH_MFA_PASSED", "system", payload: new { clientId, challengeId, sessionId = sess.SessionId }), ct);

        return new LoginResult(Token: token, MfaRequired: false);
    }

    private static string GenererCode6() => Random.Shared.Next(0, 999_999).ToString("D6");
}
