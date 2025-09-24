namespace ProjetLog430.Application.Ports.Inbound;

using ProjetLog430.Application.Contracts;

public interface IDepositUseCase
{
    /// <summary>
    /// UC-03 : Déclenche une transaction de dépôt idempotente (Pending au départ).
    /// Si la même IdempotencyKey est rejouée, renvoie le même résultat.
    /// </summary>
    Task<DepositResult> RequestAsync(
        Guid accountId,
        decimal amount,
        string currency,
        string idempotencyKey,
        CancellationToken ct = default);
}
