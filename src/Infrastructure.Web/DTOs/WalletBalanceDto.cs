namespace ProjetLog430.Infrastructure.Web.DTOs;

public sealed class WalletBalanceDto
{
    public required Guid AccountId { get; init; }
    public required string Currency { get; init; }
    public decimal Balance { get; init; }
    public DateTimeOffset LastUpdated { get; init; }
}