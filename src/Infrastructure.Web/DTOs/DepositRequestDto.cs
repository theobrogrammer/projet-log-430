using System.ComponentModel.DataAnnotations;

public sealed class DepositRequestDto
{
    [Required]
    [Range(0.01, 1_000_000.00, ErrorMessage = "Le montant doit être entre 0.01 et 1,000,000")]
    public required decimal Amount { get; init; }

    [Required]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Devise ISO-3 requise (ex: CAD, USD)")]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Devise doit être 3 lettres majuscules")]
    public required string Currency { get; init; }

    [Required]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Clé d'idempotence doit faire entre 8 et 128 caractères")]
    public required string IdempotencyKey { get; init; }
}