using System.ComponentModel.DataAnnotations;

public sealed class SignupRequestDto
{
    [Required(ErrorMessage = "L'email est requis")]
    [EmailAddress(ErrorMessage = "Format d'email invalide")]
    public required string Email { get; init; }

    [Phone(ErrorMessage = "Format de téléphone invalide")]
    public string? Phone { get; init; }

    [Required(ErrorMessage = "Le nom complet est requis")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Le nom doit contenir entre 2 et 100 caractères")]
    public required string FullName { get; init; }

    public DateOnly? BirthDate { get; init; }

    [Required(ErrorMessage = "Le mot de passe est requis")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Le mot de passe doit contenir au moins 8 caractères")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]+$", 
        ErrorMessage = "Le mot de passe doit contenir au moins une minuscule, une majuscule, un chiffre et un caractère spécial")]
    public required string Password { get; init; }

    [Compare("Password", ErrorMessage = "La confirmation du mot de passe ne correspond pas")]
    public required string ConfirmPassword { get; init; }
}