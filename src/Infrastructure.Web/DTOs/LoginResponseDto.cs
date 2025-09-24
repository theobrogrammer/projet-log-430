public sealed class LoginResponseDto
{
    public required string Token { get; init; } // jamais retourner le hash de password !
    public bool MfaRequired { get; init; }
}