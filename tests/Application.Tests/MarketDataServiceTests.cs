using Xunit;
using Moq;
using FluentAssertions;
using ProjetLog430.Application.Services;
using ProjetLog430.Domain.Model.MarketData;
using ProjetLog430.Domain.Ports.Outbound;
using ProjetLog430.Domain.Model.Observabilite;
using Microsoft.Extensions.Logging;

namespace Application.Tests;

/// <summary>
/// Tests unitaires pour MarketDataService (UC-04)
/// Architecture hexagonale : teste les Result, pas les exceptions
/// </summary>
public class MarketDataServiceTests
{
    private readonly Mock<IMarketFeedPort> _mockMarketFeed;
    private readonly Mock<ISubscriptionRepository> _mockSubscriptions;
    private readonly Mock<IQuoteRepository> _mockQuotes;
    private readonly Mock<IAuditPort> _mockAudit;
    private readonly Mock<ILogger<MarketDataService>> _mockLogger;
    private readonly MarketDataService _service;

    public MarketDataServiceTests()
    {
        _mockMarketFeed = new Mock<IMarketFeedPort>();
        _mockSubscriptions = new Mock<ISubscriptionRepository>();
        _mockQuotes = new Mock<IQuoteRepository>();
        _mockAudit = new Mock<IAuditPort>();
        _mockLogger = new Mock<ILogger<MarketDataService>>();

        // Setup default: available symbols
        _mockMarketFeed.Setup(x => x.GetAvailableSymbols())
            .Returns(new List<string> { "AAPL", "GOOGL", "MSFT", "TSLA" });

        _mockAudit.Setup(x => x.WriteAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _service = new MarketDataService(
            _mockMarketFeed.Object,
            _mockSubscriptions.Object,
            _mockQuotes.Object,
            _mockAudit.Object,
            _mockLogger.Object);
    }

    #region SubscribeAsync Tests

    [Fact]
    public async Task SubscribeAsync_ValidSymbols_ReturnsSuccess()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var symbols = new List<string> { "AAPL", "GOOGL" };

        _mockSubscriptions.Setup(x => x.AddAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.SubscribeAsync(clientId, symbols, CanalStreaming.WebSocket);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Symbols.Should().BeEquivalentTo(new[] { "AAPL", "GOOGL" });
        result.Data.Canal.Should().Be("WebSocket");
        result.Data.Statut.Should().Be("Active");

        _mockSubscriptions.Verify(x => x.AddAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockAudit.Verify(x => x.WriteAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubscribeAsync_InvalidSymbols_ReturnsFailure()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var symbols = new List<string> { "INVALID", "FAKE" };

        // Act
        var result = await _service.SubscribeAsync(clientId, symbols, CanalStreaming.WebSocket);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_SYMBOLS");
        result.ErrorMessage.Should().Contain("INVALID");
        result.ErrorMessage.Should().Contain("FAKE");
        result.Data.Should().BeNull();

        _mockSubscriptions.Verify(x => x.AddAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubscribeAsync_MixedSymbols_ReturnsFailure()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var symbols = new List<string> { "AAPL", "INVALID" }; // Un bon, un mauvais

        // Act
        var result = await _service.SubscribeAsync(clientId, symbols, CanalStreaming.WebSocket);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_SYMBOLS");
        result.ErrorMessage.Should().Contain("INVALID");
    }

    #endregion

    #region AddSymbolsAsync Tests

    [Fact]
    public async Task AddSymbolsAsync_ValidSubscription_ReturnsSuccess()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var existingSubscription = Subscription.Creer(clientId, new List<string> { "AAPL" }, CanalStreaming.WebSocket);
        var symbolsToAdd = new List<string> { "GOOGL", "MSFT" };

        _mockSubscriptions.Setup(x => x.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSubscription);
        _mockSubscriptions.Setup(x => x.UpdateAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.AddSymbolsAsync(subscriptionId, symbolsToAdd);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Symbols.Should().HaveCount(3); // AAPL + GOOGL + MSFT
        result.Data.Symbols.Should().Contain(new[] { "AAPL", "GOOGL", "MSFT" });

        _mockSubscriptions.Verify(x => x.UpdateAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddSymbolsAsync_SubscriptionNotFound_ReturnsFailure()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var symbolsToAdd = new List<string> { "GOOGL" };

        _mockSubscriptions.Setup(x => x.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        // Act
        var result = await _service.AddSymbolsAsync(subscriptionId, symbolsToAdd);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("SUBSCRIPTION_NOT_FOUND");
        result.ErrorMessage.Should().Contain(subscriptionId.ToString());
    }

    [Fact]
    public async Task AddSymbolsAsync_CancelledSubscription_ReturnsFailure()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var subscription = Subscription.Creer(clientId, new List<string> { "AAPL" }, CanalStreaming.WebSocket);
        subscription.Annuler(); // Annuler l'abonnement

        _mockSubscriptions.Setup(x => x.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _service.AddSymbolsAsync(subscriptionId, new List<string> { "GOOGL" });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("SUBSCRIPTION_CANCELLED");
    }

    [Fact]
    public async Task AddSymbolsAsync_InvalidSymbols_ReturnsFailure()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var subscription = Subscription.Creer(clientId, new List<string> { "AAPL" }, CanalStreaming.WebSocket);

        _mockSubscriptions.Setup(x => x.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _service.AddSymbolsAsync(subscriptionId, new List<string> { "INVALID" });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_SYMBOLS");
        result.ErrorMessage.Should().Contain("INVALID");
    }

    #endregion

    #region RemoveSymbolsAsync Tests

    [Fact]
    public async Task RemoveSymbolsAsync_ValidSymbols_ReturnsSuccess()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var subscription = Subscription.Creer(clientId, new List<string> { "AAPL", "GOOGL", "MSFT" }, CanalStreaming.WebSocket);

        _mockSubscriptions.Setup(x => x.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _mockSubscriptions.Setup(x => x.UpdateAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.RemoveSymbolsAsync(subscriptionId, new List<string> { "GOOGL" });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Symbols.Should().HaveCount(2); // AAPL + MSFT restent
        result.Data.Symbols.Should().Contain(new[] { "AAPL", "MSFT" });
        result.Data.Symbols.Should().NotContain("GOOGL");
    }

    [Fact]
    public async Task RemoveSymbolsAsync_LastSymbol_ReturnsFailure()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var subscription = Subscription.Creer(clientId, new List<string> { "AAPL" }, CanalStreaming.WebSocket);

        _mockSubscriptions.Setup(x => x.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _service.RemoveSymbolsAsync(subscriptionId, new List<string> { "AAPL" });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("CANNOT_REMOVE_LAST_SYMBOL");
        result.ErrorMessage.Should().Contain("Impossible de retirer tous les symboles");
    }

    [Fact]
    public async Task RemoveSymbolsAsync_SubscriptionNotFound_ReturnsFailure()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();

        _mockSubscriptions.Setup(x => x.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        // Act
        var result = await _service.RemoveSymbolsAsync(subscriptionId, new List<string> { "AAPL" });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("SUBSCRIPTION_NOT_FOUND");
    }

    #endregion

    #region UnsubscribeAsync Tests

    [Fact]
    public async Task UnsubscribeAsync_ValidSubscription_ReturnsSuccess()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var subscription = Subscription.Creer(clientId, new List<string> { "AAPL" }, CanalStreaming.WebSocket);

        _mockSubscriptions.Setup(x => x.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _mockSubscriptions.Setup(x => x.UpdateAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UnsubscribeAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        subscription.Statut.Should().Be(StatutSubscription.Cancelled);

        _mockSubscriptions.Verify(x => x.UpdateAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockAudit.Verify(x => x.WriteAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnsubscribeAsync_SubscriptionNotFound_ReturnsFailure()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();

        _mockSubscriptions.Setup(x => x.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        // Act
        var result = await _service.UnsubscribeAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("SUBSCRIPTION_NOT_FOUND");
    }

    #endregion

    #region GetLatestQuotesAsync Tests

    [Fact]
    public async Task GetLatestQuotesAsync_ValidSymbols_ReturnsSuccess()
    {
        // Arrange
        var symbols = new List<string> { "AAPL", "GOOGL" };
        var aaplQuote = Quote.Creer("AAPL", 180.00m, 180.50m, 180.25m, 10000);
        var googlQuote = Quote.Creer("GOOGL", 140.00m, 140.50m, 140.25m, 5000);

        _mockQuotes.Setup(x => x.GetLatestQuoteAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(aaplQuote);
        _mockQuotes.Setup(x => x.GetLatestQuoteAsync("GOOGL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(googlQuote);

        // Act
        var result = await _service.GetLatestQuotesAsync(symbols);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Quotes.Should().NotBeNull();
        result.Quotes!.Should().HaveCount(2);
        result.Quotes.Should().Contain(q => q.Symbol == "AAPL");
        result.Quotes.Should().Contain(q => q.Symbol == "GOOGL");
    }

    [Fact]
    public async Task GetLatestQuotesAsync_InvalidSymbols_ReturnsFailure()
    {
        // Arrange
        var symbols = new List<string> { "INVALID" };

        // Act
        var result = await _service.GetLatestQuotesAsync(symbols);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_SYMBOLS");
        result.ErrorMessage.Should().Contain("INVALID");
    }

    [Fact]
    public async Task GetLatestQuotesAsync_QuoteNotInCache_FetchesFromFeed()
    {
        // Arrange
        var symbols = new List<string> { "AAPL" };
        var aaplQuote = Quote.Creer("AAPL", 180.00m, 180.50m, 180.25m, 10000);

        _mockQuotes.Setup(x => x.GetLatestQuoteAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Quote?)null); // Pas en cache

        _mockMarketFeed.Setup(x => x.GetLatestQuoteAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(aaplQuote);

        // Act
        var result = await _service.GetLatestQuotesAsync(symbols);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Quotes!.Should().HaveCount(1);
        result.Quotes.First().Symbol.Should().Be("AAPL");

        _mockMarketFeed.Verify(x => x.GetLatestQuoteAsync("AAPL", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
