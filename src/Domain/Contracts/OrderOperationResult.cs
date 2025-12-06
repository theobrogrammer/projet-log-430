namespace ProjetLog430.Domain.Contracts;

/// <summary>
/// Result object pour les opérations sur les ordres (UC-05).
/// Architecture Hexagonale : Contrat partagé entre Domain et Application.
/// Évite les exceptions pour les cas métier attendus.
/// </summary>
public sealed class OrderOperationResult
{
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }
    public Guid? OrderId { get; init; }
    public string? ClientOrderId { get; init; }
    public string? OrderStatus { get; init; }

    private OrderOperationResult() { }

    public static OrderOperationResult Ok(Guid orderId, string clientOrderId, string orderStatus)
    {
        return new OrderOperationResult
        {
            Success = true,
            OrderId = orderId,
            ClientOrderId = clientOrderId,
            OrderStatus = orderStatus,
            Message = "Order accepted successfully"
        };
    }

    public static OrderOperationResult Fail(string errorCode, string message)
    {
        return new OrderOperationResult
        {
            Success = false,
            ErrorCode = errorCode,
            Message = message
        };
    }
}

/// <summary>
/// Result pour la récupération d'un ordre
/// </summary>
public sealed class OrderQueryResult
{
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }
    public OrderDto? Order { get; init; }

    private OrderQueryResult() { }

    public static OrderQueryResult Ok(OrderDto order)
    {
        return new OrderQueryResult
        {
            Success = true,
            Order = order
        };
    }

    public static OrderQueryResult Fail(string errorCode, string message)
    {
        return new OrderQueryResult
        {
            Success = false,
            ErrorCode = errorCode,
            Message = message
        };
    }
}

/// <summary>
/// DTO pour exposer les informations d'un ordre
/// </summary>
public sealed record OrderDto
{
    public Guid OrderId { get; init; }
    public Guid AccountId { get; init; }
    public string ClientOrderId { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public string Side { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal? Price { get; init; }
    public string TimeInForce { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal FilledQuantity { get; init; }
    public decimal? AvgPrice { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string? RejectReason { get; init; }
    public List<ExecutionDto> Executions { get; init; } = new();
}

/// <summary>
/// DTO pour exposer les informations d'une exécution
/// </summary>
public sealed record ExecutionDto
{
    public Guid ExecutionId { get; init; }
    public decimal ExecQuantity { get; init; }
    public decimal ExecPrice { get; init; }
    public decimal Commission { get; init; }
    public DateTime Timestamp { get; init; }
}
