using Microsoft.AspNetCore.Mvc;
using ProjetLog430.Domain.Ports.Inbound; // IAuthUseCase
using ProjetLog430.Domain.Ports.Outbound;
using ProjetLog430.Infrastructure.Web.DTOs;
using ProjetLog430.Domain.Model.Securite;
// using ProjetLog430.Infrastructure.Web.Mapping;

namespace ProjetLog430.Infrastructure.Web.Controllers;

public record CreateMfaPolicyRequest(TypeMfa Type); // Suppression de Email, récupéré depuis la session

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthUseCase _auth;
    private readonly IMfaPolicyRepository _mfaPolicies;
    private readonly IClientRepository _clients;
    private readonly IMfaChallengeRepository _mfaChallenges;
    private readonly ISessionRepository _sessions;

    public AuthController(IAuthUseCase auth, IMfaPolicyRepository mfaPolicies, IClientRepository clients, IMfaChallengeRepository mfaChallenges, ISessionRepository sessions)
    {
        _auth = auth;
        _mfaPolicies = mfaPolicies;
        _clients = clients;
        _mfaChallenges = mfaChallenges;
        _sessions = sessions;
    }

    /// <summary>UC-02 : Authentification (peut exiger un défi MFA)</summary>
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(
        [FromBody] LoginRequestDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return UnprocessableEntity(ModelState);

        // var p = dto.ToParams();
        
        // Vérifier s'il y a des headers de bypass pour développement
        string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
        string device = Request.Headers.UserAgent.ToString();
        
        // Ajouter les signaux de bypass depuis les headers custom
        if (Request.Headers.ContainsKey("X-Forwarded-For") && Request.Headers["X-Forwarded-For"].ToString().Contains("bypass"))
        {
            ip += " bypass";
        }
        if (Request.Headers.UserAgent.ToString().Contains("bypass"))
        {
            device += " bypass";
        }
        
        var result = await _auth.LoginAsync(
            email: dto.Email,
            password: dto.Password,
            ip: ip,
            device: device,
            ct: ct);

        var resp = new LoginResponseDto {
            Token       = result.Token,
            MfaRequired = result.MfaRequired,
            ClientId    = result.ClientId,
            ChallengeId = result.ChallengeId
        };
        return Ok(resp);
    }

    /// <summary>Validation du code MFA (OTP/TOTP/WebAuthn) → émet la session/jeton</summary>
    [HttpPost("mfa/verify")]
    public async Task<ActionResult<LoginResponseDto>> VerifyMfa(
        [FromBody] VerifyMfaRequestDto dto,
        CancellationToken ct)
    {
        var result = await _auth.VerifyMfaAsync(
            clientId: dto.ClientId,
            challengeId: dto.ChallengeId,
            code: dto.Code,
            ct: ct);

        var resp = new LoginResponseDto {
            Token       = result.Token,
            MfaRequired = false
        };
        return Ok(resp);
    }

    // === GESTION DES POLITIQUES MFA ===

    /// <summary>Créer ou réactiver une politique MFA pour l'utilisateur connecté</summary>
    [HttpPost("mfa/policy")]
    public async Task<ActionResult> CreateOrActivateMfaPolicy([FromBody] CreateMfaPolicyRequest request, CancellationToken ct)
    {
        try
        {
            // Récupérer l'utilisateur connecté depuis son token
            var client = await GetCurrentClientAsync(ct);
            if (client == null)
            {
                return Unauthorized(new { error = "Token invalide ou expiré. Veuillez vous reconnecter." });
            }

            // Vérifier s'il existe déjà une politique pour ce client
            var existingPolicy = await _mfaPolicies.GetByClientIdAsync(client.ClientId, ct);
            
            if (existingPolicy != null)
            {
                // Réactiver la politique existante
                existingPolicy.Activer();
                await _mfaPolicies.UpdateAsync(existingPolicy, ct);

                return Ok(new
                {
                    message = "Politique MFA réactivée avec succès",
                    clientId = client.ClientId,
                    email = client.Email,
                    policy = new
                    {
                        mfaId = existingPolicy.MfaId,
                        type = existingPolicy.Type.ToString(),
                        active = existingPolicy.EstActive
                    }
                });
            }
            else
            {
                // Créer une nouvelle politique
                var policy = PolitiqueMFA.Creer(
                    client.ClientId, 
                    request.Type, 
                    true); // Activée par défaut

                await _mfaPolicies.AddAsync(policy, ct);

                return Ok(new
                {
                    message = "Politique MFA créée avec succès",
                    clientId = client.ClientId,
                    email = client.Email,
                    policy = new
                    {
                        mfaId = policy.MfaId,
                        type = policy.Type.ToString(),
                        active = policy.EstActive
                    }
                });
            }
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Récupérer la politique MFA de l'utilisateur connecté</summary>
    [HttpGet("mfa/policy")]
    public async Task<IActionResult> GetCurrentUserMfaPolicy(CancellationToken ct = default)
    {
        try
        {
            // Récupérer l'utilisateur connecté depuis son token
            var client = await GetCurrentClientAsync(ct);
            if (client == null)
            {
                return Unauthorized(new { error = "Token invalide ou expiré. Veuillez vous reconnecter." });
            }

            var policy = await _mfaPolicies.GetByClientIdAsync(client.ClientId, ct);
            
            if (policy == null)
            {
                return Ok(new { message = "Aucune politique MFA trouvée", clientId = client.ClientId, estActive = false });
            }

            return Ok(new
            {
                mfaId = policy.MfaId,
                clientId = policy.ClientId,
                type = policy.Type.ToString(),
                estActive = policy.EstActive,
                createdAt = policy.CreatedAt,
                updatedAt = policy.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Désactiver la politique MFA de l'utilisateur connecté</summary>
    [HttpPost("mfa/policy/disable")]
    public async Task<IActionResult> DisableCurrentUserMfaPolicy(CancellationToken ct = default)
    {
        try
        {
            // Récupérer l'utilisateur connecté depuis son token
            var client = await GetCurrentClientAsync(ct);
            if (client == null)
            {
                return Unauthorized(new { error = "Token invalide ou expiré. Veuillez vous reconnecter." });
            }

            var policy = await _mfaPolicies.GetByClientIdAsync(client.ClientId, ct);
            
            if (policy == null)
            {
                return NotFound(new { error = "Aucune politique MFA trouvée", clientId = client.ClientId });
            }

            policy.Desactiver();
            await _mfaPolicies.UpdateAsync(policy, ct);

            return Ok(new { message = "Politique MFA désactivée", clientId = client.ClientId });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // === PAGES D'AUTHENTIFICATION ===

    /// <summary>Page dashboard (après connexion)</summary>
    [HttpGet("/dashboard")]
    public IActionResult Dashboard()
    {
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "dashboard.html");
        
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound("Dashboard page not found");
        }
        
        var content = System.IO.File.ReadAllText(filePath);
        return Content(content, "text/html");
    }

    /// <summary>Page paramètres MFA</summary>
    [HttpGet("/mfa-settings")]
    public IActionResult MfaSettings()
    {
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "mfa-settings.html");
        
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound("MFA Settings page not found");
        }
        
        var content = System.IO.File.ReadAllText(filePath);
        return Content(content, "text/html");
    }

#if DEBUG
    // === ENDPOINTS DE DEBUG (DÉVELOPPEMENT SEULEMENT) ===

    /// <summary>Endpoint de développement pour récupérer les infos d'OTP d'un client</summary>
    [HttpGet("debug/otp/{clientId:guid}")]
    public async Task<ActionResult> GetClientOtpInfo(Guid clientId, CancellationToken ct)
    {
        try
        {
            var client = await _clients.GetByIdAsync(clientId, ct);
            if (client == null)
            {
                return NotFound(new { error = "Client non trouvé", clientId });
            }

            var lastOtp = client.ContactOtps?.OrderByDescending(o => o.CreatedAt).FirstOrDefault();
            if (lastOtp == null)
            {
                return NotFound(new { error = "Aucun OTP trouvé pour ce client", clientId });
            }

            return Ok(new
            {
                clientId,
                client = new 
                {
                    email = client.Email,
                    status = client.Statut.ToString()
                },
                lastOtp = new
                {
                    otpId = lastOtp.OtpId,
                    canal = lastOtp.Canal.ToString(),
                    status = lastOtp.Statut.ToString(),
                    createdAt = lastOtp.CreatedAt,
                    expiresAt = lastOtp.ExpiresAt,
                    hasCode = lastOtp.CodeHash != null,
                    message = lastOtp.CodeHash != null 
                        ? "Code OTP généré - vérifiez les logs console" 
                        : "Aucun code généré"
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Erreur serveur", details = ex.Message });
        }
    }

    /// <summary>Endpoint de développement pour récupérer les défis MFA actifs d'un client</summary>
    [HttpGet("debug/mfa/{clientId:guid}")]
    public async Task<ActionResult> GetClientMfaChallenges(Guid clientId, CancellationToken ct)
    {
        try
        {
            var client = await _clients.GetByIdAsync(clientId, ct);
            if (client == null)
            {
                return NotFound(new { error = "Client non trouvé", clientId });
            }

            // Récupérer les défis MFA récents (dernières 24h)
            var challenges = await GetRecentMfaChallenges(clientId, ct);
            var activeChallenge = challenges.Where(c => c.Statut.ToString() == "EnCours").FirstOrDefault();

            return Ok(new
            {
                clientId,
                client = new 
                {
                    email = client.Email,
                    status = client.Statut.ToString()
                },
                mfaChallenges = new
                {
                    totalChallenges = challenges.Count(),
                    activeChallenge = activeChallenge != null ? new
                    {
                        challengeId = activeChallenge.ChallengeId,
                        type = activeChallenge.Type.ToString(),
                        status = activeChallenge.Statut.ToString(),
                        createdAt = activeChallenge.CreatedAt,
                        expiresAt = activeChallenge.ExpiresAt,
                        message = "Code MFA généré - vérifiez les logs console avec [OTP]"
                    } : null,
                    instruction = activeChallenge != null 
                        ? "docker logs projet-log-430-brokerx_app-1 --tail 10 | findstr [OTP]"
                        : "Aucun défi MFA actif"
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Erreur serveur", details = ex.Message });
        }
    }

    private Task<IEnumerable<DefiMFA>> GetRecentMfaChallenges(Guid clientId, CancellationToken ct)
    {
        try
        {
            // Logique pour récupérer les défis MFA récents
            var emptyList = new List<DefiMFA>();
            return Task.FromResult<IEnumerable<DefiMFA>>(emptyList);
        }
        catch
        {
            var emptyList = new List<DefiMFA>();
            return Task.FromResult<IEnumerable<DefiMFA>>(emptyList);
        }
    }
#endif

    // === MÉTHODES UTILITAIRES ===

    /// <summary>Récupérer les informations de l'utilisateur connecté depuis son token</summary>
    [HttpGet("me")]
    public async Task<ActionResult> GetCurrentUser(CancellationToken ct)
    {
        try
        {
            var client = await GetCurrentClientAsync(ct);
            if (client == null)
            {
                return Unauthorized(new { error = "Token invalide ou expiré" });
            }

            return Ok(new
            {
                clientId = client.ClientId,
                email = client.Email,
                nomComplet = client.NomComplet,
                status = client.Statut.ToString()
            });
        }
        catch (Exception ex)
        {
            return Unauthorized(new { error = "Erreur lors de la récupération des informations utilisateur", details = ex.Message });
        }
    }

    /// <summary>Méthode privée pour récupérer le client connecté depuis le token Bearer</summary>
    private async Task<ProjetLog430.Domain.Model.Identite.Client?> GetCurrentClientAsync(CancellationToken ct)
    {
        // Récupérer le token depuis l'en-tête Authorization
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        Console.WriteLine($"[DEBUG] Auth header: {authHeader ?? "NULL"}");
        
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            Console.WriteLine("[DEBUG] No valid Bearer token found");
            return null;
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();
        Console.WriteLine($"[DEBUG] Token extracted: {token.Substring(0, Math.Min(20, token.Length))}...");
        
        try
        {
            // Décoder le token (format: SessionId|ExpirationDate en Base64)
            var decodedBytes = Convert.FromBase64String(token);
            var payload = System.Text.Encoding.UTF8.GetString(decodedBytes);
            var parts = payload.Split('|');
            Console.WriteLine($"[DEBUG] Decoded payload: {payload}");
            
            if (parts.Length != 2 || !Guid.TryParse(parts[0], out var sessionId))
            {
                Console.WriteLine($"[DEBUG] Invalid payload format or SessionId");
                return null;
            }

            Console.WriteLine($"[DEBUG] SessionId parsed: {sessionId}");
            
            // Vérifier si la session existe et n'est pas expirée
            var session = await _sessions.GetByIdAsync(sessionId, ct);
            Console.WriteLine($"[DEBUG] Session found: {session != null}, Expired: {session?.ExpiresAt < DateTime.UtcNow}");
            
            if (session == null || session.ExpiresAt < DateTime.UtcNow)
            {
                Console.WriteLine($"[DEBUG] Session null or expired. ExpiresAt: {session?.ExpiresAt}, Now: {DateTime.UtcNow}");
                return null;
            }

            // Récupérer le client associé à cette session
            var client = await _clients.GetByIdAsync(session.ClientId, ct);
            Console.WriteLine($"[DEBUG] Client found: {client != null}");
            return client;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Exception in GetCurrentClientAsync: {ex.Message}");
            return null;
        }
    }
}
