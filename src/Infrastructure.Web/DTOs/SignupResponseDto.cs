public sealed class SignupResponseDto
{
    public required Guid ClientId { get; init; }
    public required Guid AccountId { get; init; }
    public required string Status { get; init; } // Pending/Active
}