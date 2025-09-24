namespace ProjetLog430.Application.Ports.Inbound;

public interface ISettlementCallbackUseCase
{
    /// <summary>
    /// Callback interne (webhook local) pour notifier le règlement d'une PayTx.
    /// Met à jour la Transaction, écrit le Ledger, crédite le Portefeuille.
    /// </summary>
    Task OnSettlementAsync(
        Guid paymentTxId,
        string status,            // "Settled" / "Failed"
        string? signature = null, // optionnel (simulation)
        CancellationToken ct = default);
}
