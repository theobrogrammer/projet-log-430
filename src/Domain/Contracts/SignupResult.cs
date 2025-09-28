
namespace ProjetLog430.Domain.Contracts;

/// <summary>Result of UC-01: signup (client + account created, status pending/active/rejected).</summary>
public sealed record SignupResult(
    Guid ClientId,
    Guid AccountId,
    string Status // "Pending" | "Active" | "Rejected"
);
