
namespace ProjetLog430.Domain.Contracts;

/// <summary>Result of UC-02: authentication. If MFA is required, Token is empty and MfaRequired = true.</summary>
public sealed record LoginResult(
    string Token,     // JWT or opaque token (empty when MFA required)
    bool MfaRequired  // true when a second step is needed
);
