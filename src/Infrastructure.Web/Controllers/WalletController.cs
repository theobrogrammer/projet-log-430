using Microsoft.AspNetCore.Mvc;
using ProjetLog430.Domain.Ports.Inbound; // IDepositUseCase
using ProjetLog430.Domain.Ports.Outbound; // IPortfolioRepository
using ProjetLog430.Infrastructure.Web.DTOs;
// using ProjetLog430.Infrastructure.Web.Mapping;

namespace ProjetLog430.Infrastructure.Web.Controllers;

[ApiController]
[Route("api/v1/accounts/{accountId:guid}")]
public sealed class WalletController : ControllerBase
{
    private readonly IDepositUseCase _deposit;
    private readonly IPortfolioRepository _portfolios;

    public WalletController(IDepositUseCase deposit, IPortfolioRepository portfolios)
    {
        _deposit = deposit;
        _portfolios = portfolios;
    }

    /// <summary>UC-03 : Consultation du solde</summary>
    [HttpGet("balance")]
    public async Task<ActionResult<WalletBalanceDto>> GetBalance(Guid accountId, CancellationToken ct)
    {
        var wallet = await _portfolios.GetByAccountIdAsync(accountId, ct);
        if (wallet == null) 
            return NotFound($"Aucun portefeuille trouvé pour le compte {accountId}");

        return Ok(new WalletBalanceDto 
        {
            AccountId = accountId,
            Currency = wallet.Devise,
            Balance = wallet.SoldeMonnaie,
            LastUpdated = wallet.UpdatedAt
        });
    }

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
