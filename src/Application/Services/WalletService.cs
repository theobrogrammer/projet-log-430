// src/Application/Services/WalletService.cs
using ProjetLog430.Domain.Contracts;
using ProjetLog430.Domain.Ports.Inbound;
using ProjetLog430.Domain.Model.PortefeuilleReglement;
using ProjetLog430.Domain.Ports.Outbound;
using ProjetLog430.Domain.Model.Observabilite;


namespace ProjetLog430.Application.Services;

public sealed class WalletService : IDepositUseCase, ISettlementCallbackUseCase
{
    private readonly IPayTxRepository _paytx;
    private readonly IPortefeuilleRepository _wallets;
    private readonly ILedgerPort _ledger;
    private readonly IPaymentPort _payments;
    private readonly IAuditPort _audit;

    public WalletService(
        IPayTxRepository paytx,
        IPortefeuilleRepository wallets,
        ILedgerPort ledger,
        IPaymentPort payments,
        IAuditPort audit)
    {
        _paytx = paytx;
        _wallets = wallets;
        _ledger = ledger;
        _payments = payments;
        _audit = audit;
    }

    public async Task<DepositResult> RequestAsync(Guid accountId, decimal amount, string currency, string idempotencyKey, CancellationToken ct = default)
    {
        // 1) Idempotence : existe déjà ?
        var existing = await _paytx.GetByIdempotencyKeyAsync(idempotencyKey, ct);
        if (existing is not null)
            return MapToResult(existing, await _wallets.GetByAccountIdAsync(accountId, ct));

        // 2) Créer la PayTx (Pending) et persister
        var tx = TransactionPaiement.Creer(accountId, amount, currency, idempotencyKey);
        await _paytx.AddAsync(tx, ct);

        // 3) Appeler le PSP (simulateur) — webhook local mettra à jour plus tard
        await _payments.RequestDepositAsync(tx.PaymentTxId, accountId, amount, currency, ct);

        await _audit.WriteAsync(
           AuditLog.Ecrire("DEPOSIT_REQUESTED", "system",
                payload: new { paymentTxId = tx.PaymentTxId, accountId, amount, currency }), ct);

        var wallet = await _wallets.GetByAccountIdAsync(accountId, ct);
        return MapToResult(tx, wallet);
    }

    public async Task OnSettlementAsync(Guid paymentTxId, string status, string? signature = null, CancellationToken ct = default)
    {
        var tx = await _paytx.GetByIdAsync(paymentTxId, ct) ?? throw new InvalidOperationException("Transaction inconnue.");
        var wallet = await _wallets.GetByAccountIdAsync(tx.AccountId, ct) ?? throw new InvalidOperationException("Portefeuille introuvable.");

        if (status.Equals("Settled", StringComparison.OrdinalIgnoreCase))
        {
            tx.MarquerReglee();

            // Crédit + Ledger (idempotent : si rejoué, MarquerReglee() est safe, à toi de protéger le double ledger au niveau infra/DB si nécessaire)
            wallet.Crediter(tx.Amount, tx.Currency);
            await _wallets.UpdateAsync(wallet, ct);

            var entry = EcritureLedger.PourDepot(tx.AccountId, tx.Amount, tx.Currency, tx.PaymentTxId);
            await _ledger.AddAsync(entry, ct);

            await _audit.WriteAsync(
              AuditLog.Ecrire("DEPOSIT_SETTLED", "webhook:pay-sim",
                    payload: new { paymentTxId, tx.AccountId, tx.Amount, tx.Currency }), ct);
        }
        else
        {
            tx.MarquerEchoue("provider_declined");
            await _audit.WriteAsync(
              AuditLog.Ecrire("DEPOSIT_FAILED", "webhook:pay-sim",
                    payload: new { paymentTxId, reason = "provider_declined" }), ct);
        }

        await _paytx.UpdateAsync(tx, ct);
    }

    private static DepositResult MapToResult(TransactionPaiement tx, Portefeuille? wallet)
        => new(
            PaymentTxId: tx.PaymentTxId,
            Status: tx.Statut.ToString(),
            NewCashBalance: wallet?.SoldeMonnaie ?? 0m
        );
}
