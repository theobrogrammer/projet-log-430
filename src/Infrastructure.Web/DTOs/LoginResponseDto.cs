public sealed class LoginResponseDto
{
    public required string Token { get; init; } // jamais retourner le hash de password !
    public bool MfaRequired { get; init; }
    public Guid? ClientId { get; init; }    // Nécessaire pour les appels MFA
    public Guid? ChallengeId { get; init; } // Nécessaire pour la vérification MFA
    public Guid? AccountId { get; init; }   // Nécessaire pour les opérations de trading/wallet
    public string? Email { get; init; }     // Pour affichage dans le frontend
}