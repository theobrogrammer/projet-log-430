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
    private readonly IPortfolioRepository _wallets;
    private readonly ILedgerPort _ledger;
    private readonly IPaymentPort _payments;
    private readonly IAuditPort _audit;
    private readonly ICachePort _cache;

    public WalletService(
        IPayTxRepository paytx,
        IPortfolioRepository wallets,
        ILedgerPort ledger,
        IPaymentPort payments,
        IAuditPort audit,
        ICachePort cache)
    {
        _paytx = paytx;
        _wallets = wallets;
        _ledger = ledger;
        _payments = payments;
        _audit = audit;
        _cache = cache;
    }

    public async Task<DepositResult> RequestAsync(Guid accountId, decimal amount, string currency, string idempotencyKey, CancellationToken ct = default)
    {
        // 1) Idempotence : existe déjà ?
        var existing = await _paytx.GetByIdempotencyKeyAsync(idempotencyKey, ct);
        if (existing is not null)
        {
            // Récupérer wallet depuis cache ou DB
            var cachedWallet = await GetWalletWithCacheAsync(accountId, ct);
            return MapToResult(existing, cachedWallet);
        }

        // 2) Créer la PayTx (Pending) et persister
        var tx = TransactionPaiement.Creer(accountId, amount, currency, idempotencyKey);
        await _paytx.AddAsync(tx, ct);

        // 3) Appeler le PSP (simulateur) — webhook local mettra à jour plus tard
        await _payments.RequestDepositAsync(tx.PaymentTxId, accountId, amount, currency, ct);

        await _audit.WriteAsync(
           AuditLog.Ecrire("DEPOSIT_REQUESTED", "system",
                payload: new { paymentTxId = tx.PaymentTxId, accountId, amount, currency }), ct);

        var wallet = await GetWalletWithCacheAsync(accountId, ct);
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

            // Invalider le cache wallet après modification du solde
            var cacheKey = $"wallet:balance:{tx.AccountId}";
            await _cache.RemoveAsync(cacheKey, ct);

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

    /// <summary>
    /// Récupère le wallet avec cache (TTL: 1 minute).
    /// Pattern: Try cache → If miss → DB → Set cache
    /// </summary>
    private async Task<Portefeuille?> GetWalletWithCacheAsync(Guid accountId, CancellationToken ct)
    {
        var cacheKey = $"wallet:balance:{accountId}";
        
        // Essayer le cache d'abord
        var cached = await _cache.GetAsync<Portefeuille>(cacheKey, ct);
        if (cached != null)
        {
            return cached;
        }

        // Cache miss - récupérer depuis DB
        var wallet = await _wallets.GetByAccountIdAsync(accountId, ct);
        
        // Mettre en cache si trouvé (TTL: 1 minute)
        if (wallet != null)
        {
            await _cache.SetAsync(cacheKey, wallet, TimeSpan.FromMinutes(1), ct);
        }

        return wallet;
    }

    private static DepositResult MapToResult(TransactionPaiement tx, Portefeuille? wallet)
        => new(
            PaymentTxId: tx.PaymentTxId,
            Status: tx.Statut.ToString(),
            NewCashBalance: wallet?.SoldeMonnaie ?? 0m
        );
}
