using Microsoft.AspNetCore.Mvc;
using ProjetLog430.Domain.Ports.Inbound;
using ProjetLog430.Infrastructure.Web.DTOs;

namespace ProjetLog430.Infrastructure.Web.Controllers;

/// <summary>
/// UC-05: Contrôleur pour la gestion des ordres (placement, consultation, annulation)
/// Architecture Hexagonale: Point d'entrée HTTP → IOrderUseCase
/// </summary>
[ApiController]
[Route("api/v1/orders")]
public sealed class OrderController : ControllerBase
{
    private readonly IOrderUseCase _orderUseCase;
    private readonly ILogger<OrderController> _logger;

    public OrderController(IOrderUseCase orderUseCase, ILogger<OrderController> logger)
    {
        _orderUseCase = orderUseCase;
        _logger = logger;
    }

    /// <summary>
    /// UC-05: Placer un ordre marché/limite avec contrôles pré-trade
    /// </summary>
    /// <param name="accountId">ID du compte client</param>
    /// <param name="dto">Détails de l'ordre</param>
    /// <param name="ct">Token d'annulation</param>
    /// <returns>Ordre créé ou erreur de validation</returns>
    [HttpPost]
    [Route("{accountId}")]
    public async Task<ActionResult<OrderResponseDto>> PlaceOrder(
        [FromRoute] Guid accountId,
        [FromBody] PlaceOrderRequestDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return UnprocessableEntity(ModelState);
        }

        _logger.LogInformation("📋 UC-05 PlaceOrder: AccountId={AccountId}, ClientOrderId={ClientOrderId}, Symbol={Symbol}, Side={Side}, Type={Type}, Qty={Quantity}",
            accountId, dto.ClientOrderId, dto.Symbol, dto.Side, dto.Type, dto.Quantity);

        var result = await _orderUseCase.PlaceOrderAsync(
            accountId: accountId,
            clientOrderId: dto.ClientOrderId,
            symbol: dto.Symbol,
            side: dto.Side,
            type: dto.Type,
            quantity: dto.Quantity,
            price: dto.Price,
            timeInForce: dto.TimeInForce ?? "DAY",
            ct: ct
        );

        if (!result.Success)
        {
            _logger.LogWarning("⚠️ UC-05 PlaceOrder Failed: {ErrorCode} - {Message}",
                result.ErrorCode, result.Message);

            // Mapper les codes d'erreur métier vers les statuts HTTP appropriés
            return result.ErrorCode switch
            {
                "ACCOUNT_NOT_FOUND" or "ORDER_NOT_FOUND" => NotFound(new { error = result.ErrorCode, message = result.Message }),
                "INVALID_SYMBOL" or "INVALID_CLIENT_ORDER_ID" or "INVALID_QUANTITY" 
                    or "INVALID_SIDE" or "INVALID_TYPE" or "INVALID_TIME_IN_FORCE" 
                    or "PRICE_REQUIRED" or "INVALID_TICK_SIZE" => BadRequest(new { error = result.ErrorCode, message = result.Message }),
                "INSUFFICIENT_FUNDS" or "SHORT_SELL_NOT_ALLOWED" or "MAX_NOTIONAL_EXCEEDED" 
                    or "INSTRUMENT_NOT_ACTIVE" or "PRICE_OUT_OF_BANDS" => Conflict(new { error = result.ErrorCode, message = result.Message }),
                "ORDER_ALREADY_EXISTS" => Conflict(new { error = result.ErrorCode, message = result.Message, orderId = result.OrderId }),
                _ => StatusCode(500, new { error = "INTERNAL_ERROR", message = result.Message })
            };
        }

        _logger.LogInformation("✅ UC-05 PlaceOrder Success: OrderId={OrderId}, Status={Status}",
            result.OrderId, result.OrderStatus);

        return Ok(new OrderResponseDto
        {
            OrderId = result.OrderId!.Value,
            ClientOrderId = result.ClientOrderId!,
            AccountId = accountId,
            Symbol = dto.Symbol,
            Side = dto.Side,
            Type = dto.Type,
            Quantity = dto.Quantity,
            Price = dto.Price,
            TimeInForce = dto.TimeInForce ?? "DAY",
            Status = result.OrderStatus!,
            FilledQuantity = 0,
            AveragePrice = null,
            CreatedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// UC-05: Récupérer un ordre par son ID
    /// </summary>
    /// <param name="accountId">ID du compte client</param>
    /// <param name="orderId">ID de l'ordre</param>
    /// <param name="ct">Token d'annulation</param>
    /// <returns>Détails de l'ordre ou 404</returns>
    [HttpGet]
    [Route("{accountId}/orders/{orderId}")]
    public async Task<ActionResult<OrderResponseDto>> GetOrder(
        [FromRoute] Guid accountId,
        [FromRoute] Guid orderId,
        CancellationToken ct)
    {
        _logger.LogInformation("🔍 UC-05 GetOrder: AccountId={AccountId}, OrderId={OrderId}",
            accountId, orderId);

        var result = await _orderUseCase.GetOrderAsync(orderId, ct);

        if (!result.Success)
        {
            _logger.LogWarning("⚠️ UC-05 GetOrder Failed: {ErrorCode} - {Message}",
                result.ErrorCode, result.Message);

            return result.ErrorCode switch
            {
                "ORDER_NOT_FOUND" or "ACCOUNT_NOT_FOUND" => NotFound(new { error = result.ErrorCode, message = result.Message }),
                _ => StatusCode(500, new { error = "INTERNAL_ERROR", message = result.Message })
            };
        }

        return Ok(MapToResponseDto(result.Order!));
    }

    /// <summary>
    /// UC-05: Récupérer tous les ordres d'un compte
    /// </summary>
    /// <param name="accountId">ID du compte client</param>
    /// <param name="ct">Token d'annulation</param>
    /// <returns>Liste des ordres du compte</returns>
    [HttpGet]
    [Route("{accountId}/orders")]
    public async Task<ActionResult<List<OrderResponseDto>>> GetOrdersByAccount(
        [FromRoute] Guid accountId,
        CancellationToken ct)
    {
        _logger.LogInformation("📋 UC-05 GetOrdersByAccount: AccountId={AccountId}", accountId);

        var orders = await _orderUseCase.GetOrdersByAccountAsync(accountId, ct);

        _logger.LogInformation("✅ UC-05 GetOrdersByAccount Success: Count={Count}", orders.Count);

        return Ok(orders.Select(MapToResponseDto).ToList());
    }

    /// <summary>
    /// UC-05: Annuler un ordre (si pas encore exécuté/terminal)
    /// </summary>
    /// <param name="accountId">ID du compte client</param>
    /// <param name="orderId">ID de l'ordre</param>
    /// <param name="ct">Token d'annulation</param>
    /// <returns>Ordre annulé ou erreur</returns>
    [HttpDelete]
    [Route("{accountId}/orders/{orderId}")]
    public async Task<ActionResult<OrderResponseDto>> CancelOrder(
        [FromRoute] Guid accountId,
        [FromRoute] Guid orderId,
        CancellationToken ct)
    {
        _logger.LogInformation("❌ UC-05 CancelOrder: AccountId={AccountId}, OrderId={OrderId}",
            accountId, orderId);

        var result = await _orderUseCase.CancelOrderAsync(orderId, ct);

        if (!result.Success)
        {
            _logger.LogWarning("⚠️ UC-05 CancelOrder Failed: {ErrorCode} - {Message}",
                result.ErrorCode, result.Message);

            return result.ErrorCode switch
            {
                "ORDER_NOT_FOUND" or "ACCOUNT_NOT_FOUND" => NotFound(new { error = result.ErrorCode, message = result.Message }),
                "ORDER_TERMINAL" => Conflict(new { error = result.ErrorCode, message = result.Message }),
                _ => StatusCode(500, new { error = "INTERNAL_ERROR", message = result.Message })
            };
        }

        _logger.LogInformation("✅ UC-05 CancelOrder Success: OrderId={OrderId}, Status={Status}",
            result.OrderId, result.OrderStatus);

        return Ok(new OrderResponseDto
        {
            OrderId = result.OrderId!.Value,
            ClientOrderId = result.ClientOrderId!,
            AccountId = accountId,
            Symbol = "N/A",
            Side = "N/A",
            Type = "N/A",
            Quantity = 0,
            TimeInForce = "N/A",
            Status = result.OrderStatus!,
            FilledQuantity = 0,
            CreatedAt = DateTime.UtcNow
        });
    }

    // ===============================================
    // Helper: Mapping OrderDto → OrderResponseDto
    // ===============================================
    private static OrderResponseDto MapToResponseDto(ProjetLog430.Domain.Contracts.OrderDto order)
    {
        return new OrderResponseDto
        {
            OrderId = order.OrderId,
            ClientOrderId = order.ClientOrderId,
            AccountId = order.AccountId,
            Symbol = order.Symbol,
            Side = order.Side,
            Type = order.Type,
            Quantity = (int)order.Quantity,
            Price = order.Price,
            TimeInForce = order.TimeInForce,
            Status = order.Status,
            FilledQuantity = (int)order.FilledQuantity,
            AveragePrice = order.AvgPrice,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
            RejectionReason = order.RejectReason,
            Executions = order.Executions?.Select(e => new ExecutionResponseDto
            {
                ExecutionId = e.ExecutionId,
                Price = e.ExecPrice,
                Quantity = (int)e.ExecQuantity,
                Timestamp = e.Timestamp
            }).ToList()
        };
    }
}
