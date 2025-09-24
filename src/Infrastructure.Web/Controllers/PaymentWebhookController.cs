using Microsoft.AspNetCore.Mvc;
using ProjetLog430.Application.Ports.Inbound; // ISettlementCallbackUseCase
using ProjetLog430.Infrastructure.Web.DTOs;

namespace ProjetLog430.Infrastructure.Web.Controllers;

[ApiController]
[Route("api/v1/payment")]
public sealed class PaymentWebhookController : ControllerBase
{
    private readonly ISettlementCallbackUseCase _callback;

    public PaymentWebhookController(ISettlementCallbackUseCase callback) => _callback = callback;

    /// <summary>Callback de règlement (appelé par le simulateur local)</summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] PaymentWebhookDto dto, CancellationToken ct)
    {
        // Signature facultative pour la démo : dto.Signature
        await _callback.OnSettlementAsync(
            paymentTxId: dto.PaymentTxId,
            status: dto.Status,
            signature: dto.Signature,
            ct: ct);

        return Ok(); // ou 202 Accepted si traitement asynchrone
        // tu peux renvoyer ProblemDetails uniformisé depuis un middleware en cas d'erreur
    }
}
