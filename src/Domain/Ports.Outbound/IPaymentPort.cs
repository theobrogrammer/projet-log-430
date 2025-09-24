namespace ProjetLog430.Domain.Ports.Outbound;

public interface IPaymentPort
{
    /// <summary>
    /// Déclenche un dépôt auprès du PSP (simulateur) pour une TransactionPaiement existante.
    /// Le PSP rappellera le webhook local avec le même PaymentTxId.
    /// </summary>
    Task RequestDepositAsync(
        Guid paymentTxId,
        Guid accountId,
        decimal amount,
        string currency,
        CancellationToken ct = default);
}
