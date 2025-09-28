// src/Infrastructure.Web/Controllers/DevController.cs
#if DEBUG
using Microsoft.AspNetCore.Mvc;
using ProjetLog430.Domain.Ports.Outbound;

namespace ProjetLog430.Infrastructure.Web.Controllers;

[ApiController]
[Route("api/dev")]
public sealed class DevController : ControllerBase
{
    private readonly IClientRepository _clients;
    private readonly IMfaChallengeRepository _mfaChallenges;

    public DevController(IClientRepository clients, IMfaChallengeRepository mfaChallenges)
    {
        _clients = clients;
        _mfaChallenges = mfaChallenges;
    }

    /// <summary>Endpoint de développement pour récupérer les infos d'OTP d'un client</summary>
    [HttpGet("otp/{clientId:guid}")]
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
    [HttpGet("mfa/{clientId:guid}")]
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

    private Task<IEnumerable<ProjetLog430.Domain.Model.Securite.DefiMFA>> GetRecentMfaChallenges(Guid clientId, CancellationToken ct)
    {
        // Cette implémentation dépend de votre repository MFA
        // Pour l'instant, on retourne une liste vide en attendant l'implémentation
        try
        {
            // Logique pour récupérer les défis MFA récents
            var emptyList = new List<ProjetLog430.Domain.Model.Securite.DefiMFA>();
            return Task.FromResult<IEnumerable<ProjetLog430.Domain.Model.Securite.DefiMFA>>(emptyList);
        }
        catch
        {
            var emptyList = new List<ProjetLog430.Domain.Model.Securite.DefiMFA>();
            return Task.FromResult<IEnumerable<ProjetLog430.Domain.Model.Securite.DefiMFA>>(emptyList);
        }
    }
}
#endif