using Microsoft.AspNetCore.Mvc;
using ProjetLog430.Domain.Ports.Inbound; // IAuthUseCase
using ProjetLog430.Infrastructure.Web.DTOs;
// using ProjetLog430.Infrastructure.Web.Mapping;

namespace ProjetLog430.Infrastructure.Web.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthUseCase _auth;

    public AuthController(IAuthUseCase auth) => _auth = auth;

    /// <summary>UC-02 : Authentification (peut exiger un défi MFA)</summary>
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(
        [FromBody] LoginRequestDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return UnprocessableEntity(ModelState);

        // var p = dto.ToParams();
        var result = await _auth.LoginAsync(
            email: dto.Email,
            password: dto.Password,
            ip: HttpContext.Connection.RemoteIpAddress?.ToString(),
            device: Request.Headers.UserAgent.ToString(),
            ct: ct);

        var resp = new LoginResponseDto {
            Token       = result.Token,
            MfaRequired = result.MfaRequired
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
}
