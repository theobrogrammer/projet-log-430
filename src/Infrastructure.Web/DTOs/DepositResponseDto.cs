public sealed class DepositResponseDto
{
    public required Guid PaymentTxId { get; init; }
    public required string Status { get; init; } // Pending/Settled/Failed
    public decimal NewCashBalance { get; init; }
}