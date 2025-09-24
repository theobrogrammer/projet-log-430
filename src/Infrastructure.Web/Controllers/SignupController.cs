using Microsoft.AspNetCore.Mvc;
using ProjetLog430.Application.Ports.Inbound; // ISignupUseCase
using ProjetLog430.Infrastructure.Web.DTOs;
// using ProjetLog430.Infrastructure.Web.Mapping;

namespace ProjetLog430.Infrastructure.Web.Controllers;

[ApiController]
[Route("api/v1/signup")]
public sealed class SignupController : ControllerBase
{
    private readonly ISignupUseCase _signup;

    public SignupController(ISignupUseCase signup) => _signup = signup;

    /// <summary>UC-01 : Inscription (ouvre le dossier KYC + lance OTP contact)</summary>
    [HttpPost]
    public async Task<ActionResult<SignupResponseDto>> Signup(
        [FromBody] SignupRequestDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return UnprocessableEntity(ModelState);

        // var param = dto.ToParams();
        var result = await _signup.CreateAccountAsync(
            email: dto.Email,
            phone: dto.Phone,
            fullName: dto.FullName,
            birthDate: dto.BirthDate,
            ct: ct);

        var resp = new SignupResponseDto {
            ClientId  = result.ClientId,
            AccountId = result.AccountId,
            Status    = result.Status
        };
        return Ok(resp);
    }

    /// <summary>Relancer l’OTP de vérification de contact (optionnel pour la démo)</summary>
    [HttpPost("resend-otp")]
    public async Task<IActionResult> ResendOtp([FromQuery] Guid clientId, CancellationToken ct)
    {
        await _signup.ResendContactOtpAsync(clientId, ct);
        return Accepted();
    }
}
