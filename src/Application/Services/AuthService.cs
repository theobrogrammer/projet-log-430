using ProjetLog430.Domain.Contracts;
using ProjetLog430.Domain.Ports.Inbound;
using ProjetLog430.Domain.Model.Securite;
using ProjetLog430.Domain.Ports.Outbound;
using ProjetLog430.Domain.Model.Observabilite;
using ProjetLog430.Domain.Model.Identite;

namespace ProjetLog430.Application.Services;

public sealed class AuthService : IAuthUseCase
{
    private readonly IClientRepository _clients;
    private readonly IMfaPolicyRepository _mfaPolicies;
    private readonly IMfaChallengeRepository _mfaChallenges;
    private readonly ISessionRepository _sessions;
    private readonly ISessionPort _sessionPort;
    private readonly IOtpPort _otp;
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

        // Vérification basique du mot de passe (pour la démo - dans un vrai système, utiliser un hash)
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            throw new InvalidOperationException("Identifiants invalides.");
        }

        await _audit.WriteAsync(
            AuditLog.Ecrire("DEBUG_LOGIN", "user:" + email, 
                payload: new { clientId = client.ClientId, email = client.Email }), ct);

        var policy = await _mfaPolicies.GetByClientIdAsync(client.ClientId, ct);
        
        await _audit.WriteAsync(
            AuditLog.Ecrire("DEBUG_MFA_POLICY", "user:" + email, 
                payload: new { 
                    clientId = client.ClientId, 
                    policyFound = policy != null,
                    policyActive = policy != null ? policy.EstActive : false,
                    policyType = policy != null ? policy.Type.ToString() : "null"
                }), ct);

        if (policy != null && policy.EstActive)
        {
            var ttl = TimeSpan.FromMinutes(5);
            var challenge = DefiMFA.Demarrer(client.ClientId, policy.Type, ttl);
            await _mfaChallenges.AddAsync(challenge, ct);

            if (policy.Type == TypeMfa.Sms || policy.Type == TypeMfa.Totp)
            {
                var code = GenererCode6();
                await _otp.SendContactOtpAsync(client.ClientId, challenge.ChallengeId, CanalOTP.Email, email, code, ct);
            }

            await _audit.WriteAsync(
                AuditLog.Ecrire("AUTH_MFA_CHALLENGE", "user:" + email,
                    payload: new { clientId = client.ClientId, challengeId = challenge.ChallengeId, policy = policy.Type.ToString() }), ct);

            return new LoginResult(Token: string.Empty, MfaRequired: true);
        }

        var sess = Session.Creer(client.ClientId, TypeJeton.Jwt, token: Guid.NewGuid().ToString("N"), ttl: TimeSpan.FromHours(2), ip, device);
        await _sessions.AddAsync(sess, ct);
        var token = await _sessionPort.IssueAsync(sess, ct);

        await _audit.WriteAsync(
          AuditLog.Ecrire("AUTH_LOGIN", "user:" + email, payload: new { clientId = client.ClientId, sessionId = sess.SessionId }), ct);

        return new LoginResult(Token: token, MfaRequired: false);
    }

    public async Task<LoginResult> VerifyMfaAsync(Guid clientId, Guid challengeId, string code, CancellationToken ct = default)
    {
        var challenge = await _mfaChallenges.GetByIdAsync(challengeId, ct) ?? throw new InvalidOperationException("Défi inconnu.");

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
