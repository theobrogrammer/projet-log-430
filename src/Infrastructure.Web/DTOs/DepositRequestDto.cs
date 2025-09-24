public sealed class DepositRequestDto
{
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string IdempotencyKey { get; init; }
}