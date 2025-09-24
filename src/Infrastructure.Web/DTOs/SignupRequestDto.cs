public sealed class SignupRequestDto
{
    public required string Email { get; init; }
    public string? Phone { get; init; }
    public required string FullName { get; init; }
    public DateOnly? BirthDate { get; init; }
}