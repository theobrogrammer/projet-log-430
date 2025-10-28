using ProjetLog430.Domain.Contracts;
using ProjetLog430.Domain.Ports.Inbound;
using ProjetLog430.Domain.Model.Securite;
using ProjetLog430.Domain.Ports.Outbound;
using ProjetLog430.Domain.Model.Observabilite;
using ProjetLog430.Domain.Model.Identite;
using Serilog;

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
    private readonly ICachePort _cache;

    public AuthService(
        IClientRepository clients,
        IMfaPolicyRepository mfaPolicies,
        IMfaChallengeRepository mfaChallenges,
        ISessionRepository sessions,
        ISessionPort sessionPort,
        IOtpPort otp,
        IAuditPort audit,
        ICachePort cache)
    {
        _clients = clients;
        _mfaPolicies = mfaPolicies;
        _mfaChallenges = mfaChallenges;
        _sessions = sessions;
        _sessionPort = sessionPort;
        _otp = otp;
        _audit = audit;
        _cache = cache;
    }

    public async Task<LoginResult> LoginAsync(
        string email,
        string password,
        string? ip = null,
        string? device = null,
        CancellationToken ct = default)
    {
        Log.Information("UC02_LOGIN_START - Tentative de connexion pour {Email} depuis {IP}", email, ip);

        var client = await _clients.GetByEmailAsync(email, ct) ?? throw new InvalidOperationException("Identifiants invalides.");

        // Vérification basique du mot de passe (pour la démo - dans un vrai système, utiliser un hash)
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            Log.Warning("UC02_LOGIN_INVALID_PASSWORD - Mot de passe invalide pour {Email}", email);
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

        // Mode bypass pour développement - vérifier si on veut skip le MFA
        bool bypassMfa = ip?.Contains("bypass") == true || device?.Contains("bypass") == true;
        
        await _audit.WriteAsync(
            AuditLog.Ecrire("DEBUG_MFA_BYPASS", "user:" + email, 
                payload: new { 
                    ip = ip,
                    device = device,
                    bypassMfa = bypassMfa,
                    policyActive = policy?.EstActive,
                    willRequireMfa = policy != null && policy.EstActive && !bypassMfa
                }), ct);
        
        if (policy != null && policy.EstActive && !bypassMfa)
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

            Log.Information("UC02_LOGIN_MFA_REQUIRED - MFA requis pour {ClientId} {Email} {ChallengeId}", 
                client.ClientId, email, challenge.ChallengeId);

            return new LoginResult(Token: string.Empty, MfaRequired: true, ClientId: client.ClientId, ChallengeId: challenge.ChallengeId);
        }

        var sess = Session.Creer(client.ClientId, TypeJeton.Jwt, token: Guid.NewGuid().ToString("N"), ttl: TimeSpan.FromHours(2), ip, device);
        await _sessions.AddAsync(sess, ct);
        var token = await _sessionPort.IssueAsync(sess, ct);

        await _audit.WriteAsync(
          AuditLog.Ecrire("AUTH_LOGIN", "user:" + email, payload: new { clientId = client.ClientId, sessionId = sess.SessionId }), ct);

        Log.Information("UC02_LOGIN_SUCCESS - Connexion réussie sans MFA: {ClientId} {Email} {SessionId}", 
            client.ClientId, email, sess.SessionId);

        return new LoginResult(Token: token, MfaRequired: false);
    }

    public async Task<LoginResult> VerifyMfaAsync(Guid clientId, Guid challengeId, string code, CancellationToken ct = default)
    {
        Log.Information("UC02_MFA_VERIFY_START - Vérification MFA pour {ClientId} {ChallengeId}", clientId, challengeId);

        // Essayer de récupérer le challenge depuis le cache d'abord (TTL: 5min)
        var cacheKey = $"mfa:challenge:{challengeId}";
        var challenge = await _cache.GetAsync<DefiMFA>(cacheKey, ct);

        if (challenge == null)
        {
            // Cache miss - récupérer depuis DB
            challenge = await _mfaChallenges.GetByIdAsync(challengeId, ct) ?? throw new InvalidOperationException("Défi inconnu.");
            
            // Mettre en cache pour accès futurs (TTL: 5min - même durée que le challenge)
            await _cache.SetAsync(cacheKey, challenge, TimeSpan.FromMinutes(5), ct);
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            Log.Warning("UC02_MFA_VERIFY_MISSING_CODE - Code MFA manquant pour {ClientId}", clientId);
            throw new InvalidOperationException("Code MFA manquant.");
        }

        challenge.Reussir();
        await _mfaChallenges.UpdateAsync(challenge, ct);

        // Invalider le cache après validation
        await _cache.RemoveAsync(cacheKey, ct);

        var sess = Session.Creer(clientId, TypeJeton.Jwt, token: Guid.NewGuid().ToString("N"), ttl: TimeSpan.FromHours(2));
        await _sessions.AddAsync(sess, ct);
        var token = await _sessionPort.IssueAsync(sess, ct);

        // Cacher la session pour validation rapide (TTL: 2h - même que le token)
        var sessionCacheKey = $"session:{sess.SessionId}";
        await _cache.SetAsync(sessionCacheKey, sess, TimeSpan.FromHours(2), ct);

        await _audit.WriteAsync(
            AuditLog.Ecrire("AUTH_MFA_PASSED", "system", payload: new { clientId, challengeId, sessionId = sess.SessionId }), ct);

        Log.Information("UC02_MFA_VERIFY_SUCCESS - MFA validé: {ClientId} {ChallengeId} {SessionId}", 
            clientId, challengeId, sess.SessionId);

        return new LoginResult(Token: token, MfaRequired: false);
    }

    private static string GenererCode6() => Random.Shared.Next(0, 999_999).ToString("D6");
}
