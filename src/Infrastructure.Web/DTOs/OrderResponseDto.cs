namespace ProjetLog430.Infrastructure.Web.DTOs;

/// <summary>
/// UC-05: DTO de réponse pour un ordre
/// </summary>
public sealed class OrderResponseDto
{
    public required Guid OrderId { get; init; }
    public required string ClientOrderId { get; init; }
    public required Guid AccountId { get; init; }
    public required string Symbol { get; init; }
    public required string Side { get; init; }
    public required string Type { get; init; }
    public required int Quantity { get; init; }
    public decimal? Price { get; init; }
    public required string TimeInForce { get; init; }
    public required string Status { get; init; }
    public int FilledQuantity { get; init; }
    public decimal? AveragePrice { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string? RejectionReason { get; init; }
    public List<ExecutionResponseDto>? Executions { get; init; }
}

/// <summary>
/// UC-05: DTO pour une exécution partielle/totale
/// </summary>
public sealed class ExecutionResponseDto
{
    public required Guid ExecutionId { get; init; }
    public required decimal Price { get; init; }
    public required int Quantity { get; init; }
    public required DateTime Timestamp { get; init; }
}
