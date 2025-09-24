// Infrastructure.Web/Mapping/DtoMapping.cs
// namespace Infrastructure.Web.Mapping;

// using Application.Contracts; // your UC params/results
// using Infrastructure.Web.DTOs;

public static class DtoMapping
{
    // UC-03 Deposit
    public static DepositCommand ToCommand(
        this DepositRequestDto dto,
        Guid accountId,
        string? idempotencyKeyHeader) =>
        new(
            AccountId: accountId,
            Amount: dto.Amount,
            Currency: dto.Currency,
            IdempotencyKey: idempotencyKeyHeader ?? dto.IdempotencyKey);

    public static DepositResponseDto ToDto(this DepositResult result) => new()
    {
        PaymentTxId = result.PaymentTxId,
        Status = result.Status,
        NewCashBalance = result.NewCashBalance
    };

    // UC-01 Signup
    public static SignupParams ToParams(this SignupRequestDto dto) =>
        new(dto.Email, dto.Phone, dto.FullName, dto.BirthDate);

    public static SignupResponseDto ToDto(this SignupResult res) => new()
    {
        ClientId = res.ClientId,
        AccountId = res.AccountId,
        Status = res.Status
    };

    // UC-02 Login
    public static LoginParams ToParams(this LoginRequestDto dto) =>
        new(dto.Email, dto.Password);

    public static LoginResponseDto ToDto(this LoginResult res) => new()
    {
        Token = res.Token,
        MfaRequired = res.MfaRequired
    };
}
