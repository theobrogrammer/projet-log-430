public sealed class PaymentWebhookDto
{
    public required Guid PaymentTxId { get; init; }
    public required string Status { get; init; }  // Settled/Failed
    public string? Signature { get; init; }       // si tu simules une signature
}
