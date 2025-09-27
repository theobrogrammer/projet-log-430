namespace ProjetLog430.Application.Contracts;

/// <summary>Result of UC-03: deposit request (idempotent).</summary>
public sealed record DepositResult(
    Guid PaymentTxId,
    string Status,       // "Pending" | "Settled" | "Failed"
    decimal NewCashBalance
);
