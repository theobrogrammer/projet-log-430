using Microsoft.AspNetCore.Mvc;
using ProjetLog430.Application.Ports.Inbound; // IDepositUseCase
using ProjetLog430.Infrastructure.Web.DTOs;
// using ProjetLog430.Infrastructure.Web.Mapping;

namespace ProjetLog430.Infrastructure.Web.Controllers;

[ApiController]
[Route("api/v1/accounts/{accountId:guid}")]
public sealed class WalletController : ControllerBase
{
    private readonly IDepositUseCase _deposit;

    public WalletController(IDepositUseCase deposit) => _deposit = deposit;

    /// <summary>UC-03 : Dépôt (idempotent via Idempotency-Key)</summary>
    [HttpPost("deposit")]
    public async Task<ActionResult<DepositResponseDto>> Deposit(
        Guid accountId,
        [FromBody] DepositRequestDto dto,
        [FromHeader(Name = "Idempotency-Key")] string? idemKeyHeader,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return UnprocessableEntity(ModelState);

        var idem = string.IsNullOrWhiteSpace(idemKeyHeader) ? dto.IdempotencyKey : idemKeyHeader;

        var result = await _deposit.RequestAsync(
            accountId: accountId,
            amount: dto.Amount,
            currency: dto.Currency,
            idempotencyKey: idem!,
            ct: ct);

        var resp = new DepositResponseDto {
            PaymentTxId   = result.PaymentTxId,
            Status        = result.Status,
            NewCashBalance = result.NewCashBalance
        };
        return Ok(resp);
    }
}
