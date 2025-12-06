using Xunit;
using Moq;
using FluentAssertions;
using ProjetLog430.Application.Services;
using ProjetLog430.Domain.Ports.Outbound;
using ProjetLog430.Domain.Model.Trading;
using ProjetLog430.Domain.Model.Identite;
using ProjetLog430.Domain.Model.Observabilite;

namespace Application.Tests;

/// <summary>
/// Tests unitaires pour OrderService (UC-05)
/// Pattern inspiré de MarketDataServiceTests et WalletServiceTests
/// </summary>
public class OrderServiceTests
{
    private readonly Mock<IOrderRepository> _mockOrderRepo;
    private readonly Mock<ICompteRepository> _mockCompteRepo;
    private readonly Mock<IPortfolioRepository> _mockPortfolioRepo;
    private readonly Mock<IPreTradeCheckPort> _mockPreTradeCheck;
    private readonly Mock<IOrderMatchingPort> _mockOrderMatching;
    private readonly Mock<IAuditPort> _mockAudit;
    private readonly Mock<ICachePort> _mockCache;
    private readonly OrderService _service;

    public OrderServiceTests()
    {
        _mockOrderRepo = new Mock<IOrderRepository>();
        _mockCompteRepo = new Mock<ICompteRepository>();
        _mockPortfolioRepo = new Mock<IPortfolioRepository>();
        _mockPreTradeCheck = new Mock<IPreTradeCheckPort>();
        _mockOrderMatching = new Mock<IOrderMatchingPort>();
        _mockAudit = new Mock<IAuditPort>();
        _mockCache = new Mock<ICachePort>();

        _mockAudit.Setup(x => x.WriteAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _service = new OrderService(
            _mockOrderRepo.Object,
            _mockCompteRepo.Object,
            _mockPortfolioRepo.Object,
            _mockPreTradeCheck.Object,
            _mockOrderMatching.Object,
            _mockAudit.Object,
            _mockCache.Object);
    }

    [Fact]
    public async Task PlaceOrderAsync_InvalidSymbol_ReturnsError()
    {
        // Act
        var result = await _service.PlaceOrderAsync(
            Guid.NewGuid(), "TEST-001", "", "Buy", "Market", 100, null, "DAY");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_SYMBOL");
    }

    [Fact]
    public async Task PlaceOrderAsync_InvalidClientOrderId_ReturnsError()
    {
        // Act
        var result = await _service.PlaceOrderAsync(
            Guid.NewGuid(), "", "AAPL", "Buy", "Market", 100, null, "DAY");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CLIENT_ORDER_ID");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task PlaceOrderAsync_InvalidQuantity_ReturnsError(int quantity)
    {
        // Act
        var result = await _service.PlaceOrderAsync(
            Guid.NewGuid(), "TEST-001", "AAPL", "Buy", "Market", quantity, null, "DAY");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_QUANTITY");
    }

    [Fact]
    public async Task GetOrderAsync_OrderNotFound_ReturnsError()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _mockOrderRepo.Setup(x => x.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ordre?)null);

        // Act
        var result = await _service.GetOrderAsync(orderId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("ORDER_NOT_FOUND");
        _mockOrderRepo.Verify(x => x.GetByIdAsync(orderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelOrderAsync_OrderNotFound_ReturnsError()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _mockOrderRepo.Setup(x => x.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ordre?)null);

        // Act
        var result = await _service.CancelOrderAsync(orderId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("ORDER_NOT_FOUND");
    }
}
