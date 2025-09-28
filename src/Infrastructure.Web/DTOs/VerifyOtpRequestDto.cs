using System.ComponentModel.DataAnnotations;

namespace ProjetLog430.Infrastructure.Web.DTOs;

public sealed record VerifyOtpRequestDto
{
    [Required]
    public Guid ClientId { get; init; }

    [Required]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Le code OTP doit contenir exactement 6 chiffres")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Le code OTP doit être composé de 6 chiffres uniquement")]
    public string Code { get; init; } = "";
}