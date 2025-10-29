using Xunit;
using Moq;
using ProjetLog430.Application.Services;
using ProjetLog430.Domain.Model.PortefeuilleReglement;
using ProjetLog430.Domain.Model.Identite;
using ProjetLog430.Domain.Model.Observabilite;
using ProjetLog430.Domain.Ports.Outbound;
using ProjetLog430.Domain.Contracts;
using Microsoft.Extensions.Logging;

namespace Application.Tests;

public class WalletServiceTests
{
    private readonly Mock<IPayTxRepository> _mockPayTxRepository;
    private readonly Mock<IPortfolioRepository> _mockPortfolioRepository;
    private readonly Mock<IAccountRepository> _mockAccountRepository;
    private readonly Mock<ILedgerPort> _mockLedgerPort;
    private readonly Mock<IPaymentPort> _mockPaymentPort;
    private readonly Mock<IAuditPort> _mockAuditPort;
    private readonly Mock<ICachePort> _mockCachePort;
    private readonly Mock<ILogger<WalletService>> _mockLogger;
    private readonly WalletService _walletService;

    public WalletServiceTests()
    {
        _mockPayTxRepository = new Mock<IPayTxRepository>();
        _mockPortfolioRepository = new Mock<IPortfolioRepository>();
        _mockAccountRepository = new Mock<IAccountRepository>();
        _mockLedgerPort = new Mock<ILedgerPort>();
        _mockPaymentPort = new Mock<IPaymentPort>();
        _mockAuditPort = new Mock<IAuditPort>();
        _mockCachePort = new Mock<ICachePort>();
        _mockLogger = new Mock<ILogger<WalletService>>();

        _walletService = new WalletService(
            _mockPayTxRepository.Object,
            _mockPortfolioRepository.Object,
            _mockAccountRepository.Object,
            _mockLedgerPort.Object,
            _mockPaymentPort.Object,
            _mockAuditPort.Object,
            _mockCachePort.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task RequestAsync_ValidDeposit_CreatesTransactionAndCallsPaymentPort()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var amount = 100.50m;
        var currency = "CAD";
        var idempotencyKey = "test-key-12345678";

        var client = Client.Creer("test@example.com", null, "Test User", "hash", null);
        var compte = client.OuvrirCompte();
        compte.SetId(accountId); // Méthode helper pour tests

        var wallet = Portefeuille.Ouvrir(accountId, currency, 500m);

        _mockAccountRepository.Setup(x => x.GetByIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(compte);
        _mockPayTxRepository.Setup(x => x.GetByIdempotencyKeyAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TransactionPaiement?)null);
        _mockPayTxRepository.Setup(x => x.AddAsync(It.IsAny<TransactionPaiement>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockPortfolioRepository.Setup(x => x.GetByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wallet);
        _mockCachePort.Setup(x => x.GetAsync<Portefeuille>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Portefeuille?)null);
        _mockPaymentPort.Setup(x => x.RequestDepositAsync(It.IsAny<Guid>(), accountId, amount, currency, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockAuditPort.Setup(x => x.WriteAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _walletService.RequestAsync(accountId, amount, currency, idempotencyKey);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Pending", result.Status);
        Assert.Equal(wallet.SoldeMonnaie, result.NewCashBalance);

        _mockPayTxRepository.Verify(x => x.AddAsync(It.IsAny<TransactionPaiement>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockPaymentPort.Verify(x => x.RequestDepositAsync(It.IsAny<Guid>(), accountId, amount, currency, It.IsAny<CancellationToken>()), Times.Once);
        _mockAuditPort.Verify(x => x.WriteAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestAsync_IdempotentRequest_ReturnsExistingResult()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var amount = 100.50m;
        var currency = "CAD";
        var idempotencyKey = "existing-key-12345678";

        var client = Client.Creer("test@example.com", null, "Test User", "hash", null);
        var compte = client.OuvrirCompte();
        compte.SetId(accountId);

        var existingTx = TransactionPaiement.Creer(accountId, amount, currency, idempotencyKey);
        existingTx.MarquerReglee();

        var wallet = Portefeuille.Ouvrir(accountId, currency, 600.50m);

        _mockAccountRepository.Setup(x => x.GetByIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(compte);
        _mockPayTxRepository.Setup(x => x.GetByIdempotencyKeyAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTx);
        _mockPortfolioRepository.Setup(x => x.GetByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wallet);
        _mockCachePort.Setup(x => x.GetAsync<Portefeuille>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Portefeuille?)null);

        // Act
        var result = await _walletService.RequestAsync(accountId, amount, currency, idempotencyKey);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Settled", result.Status);
        Assert.Equal(existingTx.PaymentTxId, result.PaymentTxId);
        Assert.Equal(wallet.SoldeMonnaie, result.NewCashBalance);

        // Ne doit pas créer de nouvelle transaction
        _mockPayTxRepository.Verify(x => x.AddAsync(It.IsAny<TransactionPaiement>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockPaymentPort.Verify(x => x.RequestDepositAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0.005)] // Trop petit
    [InlineData(2_000_000)] // Trop gros
    public async Task RequestAsync_InvalidAmount_ThrowsException(decimal invalidAmount)
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var currency = "CAD";
        var idempotencyKey = "test-key-12345678";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _walletService.RequestAsync(accountId, invalidAmount, currency, idempotencyKey));
    }

    [Fact]
    public async Task RequestAsync_UnsupportedCurrency_ThrowsException()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var amount = 100m;
        var currency = "XYZ"; // Devise non supportée
        var idempotencyKey = "test-key-12345678";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _walletService.RequestAsync(accountId, amount, currency, idempotencyKey));
    }

    [Fact]
    public async Task RequestAsync_InactiveAccount_ThrowsException()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var amount = 100m;
        var currency = "CAD";
        var idempotencyKey = "test-key-12345678";

        var client = Client.Creer("test@example.com", null, "Test User", "hash", null);
        var compte = client.OuvrirCompte();
        compte.SetId(accountId);
        compte.Suspendre("Test suspension");

        _mockAccountRepository.Setup(x => x.GetByIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(compte);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _walletService.RequestAsync(accountId, amount, currency, idempotencyKey));
    }

    [Fact]
    public async Task OnSettlementAsync_SettledStatus_UpdatesTransactionAndWallet()
    {
        // Arrange
        var paymentTxId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var amount = 100m;
        var currency = "CAD";

        var tx = TransactionPaiement.Creer(accountId, amount, currency, "test-key");
        var wallet = Portefeuille.Ouvrir(accountId, currency, 500m);

        _mockPayTxRepository.Setup(x => x.GetByIdAsync(paymentTxId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx);
        _mockPortfolioRepository.Setup(x => x.GetByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wallet);
        _mockPortfolioRepository.Setup(x => x.UpdateAsync(It.IsAny<Portefeuille>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockLedgerPort.Setup(x => x.AddAsync(It.IsAny<EcritureLedger>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockPayTxRepository.Setup(x => x.UpdateAsync(It.IsAny<TransactionPaiement>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockCachePort.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockAuditPort.Setup(x => x.WriteAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _walletService.OnSettlementAsync(paymentTxId, "Settled");

        // Assert
        Assert.Equal(StatutTransaction.Settled, tx.Statut);
        Assert.Equal(600m, wallet.SoldeMonnaie); // 500 + 100

        _mockPortfolioRepository.Verify(x => x.UpdateAsync(It.IsAny<Portefeuille>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockLedgerPort.Verify(x => x.AddAsync(It.IsAny<EcritureLedger>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockCachePort.Verify(x => x.RemoveAsync(It.Is<string>(k => k.Contains($"wallet:balance:{accountId}")), It.IsAny<CancellationToken>()), Times.Once);
        _mockAuditPort.Verify(x => x.WriteAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnSettlementAsync_FailedStatus_UpdatesTransactionOnly()
    {
        // Arrange
        var paymentTxId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var amount = 100m;
        var currency = "CAD";

        var tx = TransactionPaiement.Creer(accountId, amount, currency, "test-key");
        var wallet = Portefeuille.Ouvrir(accountId, currency, 500m);

        _mockPayTxRepository.Setup(x => x.GetByIdAsync(paymentTxId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx);
        _mockPortfolioRepository.Setup(x => x.GetByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wallet);
        _mockPayTxRepository.Setup(x => x.UpdateAsync(It.IsAny<TransactionPaiement>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockAuditPort.Setup(x => x.WriteAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _walletService.OnSettlementAsync(paymentTxId, "Failed");

        // Assert
        Assert.Equal(StatutTransaction.Failed, tx.Statut);
        Assert.Equal(500m, wallet.SoldeMonnaie); // Pas de changement

        _mockPortfolioRepository.Verify(x => x.UpdateAsync(It.IsAny<Portefeuille>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockLedgerPort.Verify(x => x.AddAsync(It.IsAny<EcritureLedger>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockAuditPort.Verify(x => x.WriteAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}