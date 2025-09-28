namespace ProjetLog430.Domain.Contracts;

public sealed record OtpVerificationResult(
    bool Success,
    string? ErrorMessage,
    string? NewStatus
)
{
    public static OtpVerificationResult Succeed(string newStatus)
        => new(true, null, newStatus);

    public static OtpVerificationResult Failed(string errorMessage)
        => new(false, errorMessage, null);
}