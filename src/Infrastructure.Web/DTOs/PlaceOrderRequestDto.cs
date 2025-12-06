using System.ComponentModel.DataAnnotations;

namespace ProjetLog430.Infrastructure.Web.DTOs;

/// <summary>
/// UC-05: DTO pour placement d'ordre marché/limite
/// </summary>
public sealed class PlaceOrderRequestDto
{
    /// <summary>Client order ID (idempotence key)</summary>
    [Required]
    public required string ClientOrderId { get; init; }

    /// <summary>Symbole de l'instrument (ex: AAPL, TSLA)</summary>
    [Required]
    public required string Symbol { get; init; }

    /// <summary>Sens de l'ordre: "Buy" ou "Sell"</summary>
    [Required]
    public required string Side { get; init; }

    /// <summary>Type d'ordre: "Market" ou "Limit"</summary>
    [Required]
    public required string Type { get; init; }

    /// <summary>Quantité en actions</summary>
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
    public required int Quantity { get; init; }

    /// <summary>Prix limite (requis pour Type=Limit, ignoré pour Type=Market)</summary>
    public decimal? Price { get; init; }

    /// <summary>Durée de validité: "DAY", "IOC", "FOK", "GTC" (default: DAY)</summary>
    public string? TimeInForce { get; init; }
}
