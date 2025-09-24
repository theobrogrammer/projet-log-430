namespace ProjetLog430.Infrastructure.Web.DTOs;

public sealed class VerifyMfaRequestDto
{
    public Guid ClientId { get; init; }
    public Guid ChallengeId { get; init; }
    public string Code { get; init; } = default!;
}