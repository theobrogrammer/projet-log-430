using FluentAssertions;
using Moq;
using ProjetLog430.Infrastructure.Adapters.MarketData;
using ProjetLog430.Domain.Model.MarketData;
using ProjetLog430.Domain.Ports.Outbound;

namespace Infrastructure.Tests.MarketData;

/// <summary>
/// Unit tests for UC-04: Real-Time Market Data Feed - MarketFeedSimulator
/// </summary>
public class MarketFeedSimulatorTests
{
    private Mock<IQuoteRepository> CreateMockRepository()
    {
        var mock = new Mock<IQuoteRepository>();
        mock.Setup(r => r.GetLatestQuoteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Quote?)null); // Return null initially so simulator generates new quotes
        mock.Setup(r => r.AddAsync(It.IsAny<Quote>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    [Fact]
    public async Task StartFeedAsync_ShouldInitializeSymbols()
    {
        // Arrange
        var mockRepo = CreateMockRepository();
        var simulator = new MarketFeedSimulator(mockRepo.Object);
        
        // Act
        await simulator.StartFeedAsync(CancellationToken.None);
        
        // Assert
        var quote = await simulator.GetLatestQuoteAsync("AAPL");
        quote.Should().NotBeNull();
        quote!.Symbol.Should().Be("AAPL");
        quote.Bid.Should().BeGreaterThan(0);
        quote.Ask.Should().BeGreaterThan(0);
        quote.Last.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetLatestQuoteAsync_ShouldReturnValidQuote()
    {
        // Arrange
        var mockRepo = CreateMockRepository();
        var simulator = new MarketFeedSimulator(mockRepo.Object);
        await simulator.StartFeedAsync(CancellationToken.None);
        
        // Act
        var quote = await simulator.GetLatestQuoteAsync("GOOGL");
        
        // Assert
        quote.Should().NotBeNull();
        quote!.Symbol.Should().Be("GOOGL");
        quote.Bid.Should().BeLessThan(quote.Ask); // Bid should always be less than Ask
        quote.Last.Should().BeInRange(quote.Bid, quote.Ask); // Last should be between Bid and Ask
        quote.Volume.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetLatestQuoteAsync_ShouldReturnNull_ForInvalidSymbol()
    {
        // Arrange
        var mockRepo = CreateMockRepository();
        var simulator = new MarketFeedSimulator(mockRepo.Object);
        await simulator.StartFeedAsync(CancellationToken.None);
        
        // Act
        var quote = await simulator.GetLatestQuoteAsync("INVALID");
        
        // Assert
        quote.Should().BeNull();
    }

    [Fact]
    public async Task OnQuoteGenerated_ShouldFireEvent_WhenQuoteIsGenerated()
    {
        // Arrange
        var mockRepo = CreateMockRepository();
        var simulator = new MarketFeedSimulator(mockRepo.Object);
        await simulator.StartFeedAsync(CancellationToken.None);
        
        Quote? receivedQuote = null;
        simulator.OnQuoteGenerated += (sender, quote) =>
        {
            receivedQuote = quote;
        };
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        
        // Act
        await simulator.GenerateQuotesAsync(cts.Token);
        
        // Assert
        receivedQuote.Should().NotBeNull();
        receivedQuote!.Symbol.Should().NotBeNullOrEmpty();
        receivedQuote.Bid.Should().BeGreaterThan(0);
        receivedQuote.Ask.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GenerateQuotesAsync_ShouldGenerateMultipleQuotes()
    {
        // Arrange
        var mockRepo = CreateMockRepository();
        var simulator = new MarketFeedSimulator(mockRepo.Object);
        await simulator.StartFeedAsync(CancellationToken.None);
        
        var quotesReceived = new List<Quote>();
        simulator.OnQuoteGenerated += (sender, quote) =>
        {
            quotesReceived.Add(quote);
        };
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        
        // Act
        await simulator.GenerateQuotesAsync(cts.Token);
        
        // Assert
        quotesReceived.Should().HaveCountGreaterThan(5); // Should generate multiple quotes in 3 seconds
        quotesReceived.Select(q => q.Symbol).Distinct().Should().HaveCountGreaterThan(1); // Should have multiple symbols
    }

    [Fact]
    public async Task Quote_ShouldHaveValidSpread()
    {
        // Arrange
        var mockRepo = CreateMockRepository();
        var simulator = new MarketFeedSimulator(mockRepo.Object);
        await simulator.StartFeedAsync(CancellationToken.None);
        
        // Act
        var quote = await simulator.GetLatestQuoteAsync("MSFT");
        
        // Assert
        var spread = quote!.ObtenirSpread();
        spread.Should().BeGreaterThan(0);
        spread.Should().Be(quote.Ask - quote.Bid);
    }

    [Fact]
    public async Task Quote_ShouldHaveValidSpreadBps()
    {
        // Arrange
        var mockRepo = CreateMockRepository();
        var simulator = new MarketFeedSimulator(mockRepo.Object);
        await simulator.StartFeedAsync(CancellationToken.None);
        
        // Act
        var quote = await simulator.GetLatestQuoteAsync("AMZN");
        
        // Assert
        var spreadBps = quote!.ObtenirSpreadBps();
        spreadBps.Should().BeGreaterThan(0);
        
        // Verify the BPS calculation: ((Ask - Bid) / Last) * 10000
        var expectedBps = ((quote.Ask - quote.Bid) / quote.Last) * 10000;
        spreadBps.Should().BeApproximately(expectedBps, 0.01m);
    }

    [Theory]
    [InlineData("AAPL")]
    [InlineData("GOOGL")]
    [InlineData("MSFT")]
    [InlineData("AMZN")]
    [InlineData("TSLA")]
    [InlineData("META")]
    [InlineData("NVDA")]
    [InlineData("AMD")]
    [InlineData("DIS")]
    [InlineData("NFLX")]
    public async Task GetLatestQuoteAsync_ShouldReturnQuote_ForAllSupportedSymbols(string symbol)
    {
        // Arrange
        var mockRepo = CreateMockRepository();
        var simulator = new MarketFeedSimulator(mockRepo.Object);
        await simulator.StartFeedAsync(CancellationToken.None);
        
        // Act
        var quote = await simulator.GetLatestQuoteAsync(symbol);
        
        // Assert
        quote.Should().NotBeNull();
        quote!.Symbol.Should().Be(symbol.ToUpper());
        quote.Bid.Should().BeGreaterThan(0);
        quote.Ask.Should().BeGreaterThan(quote.Bid);
        quote.Volume.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetAvailableSymbols_ShouldReturnAllSymbols()
    {
        // Arrange
        var mockRepo = CreateMockRepository();
        var simulator = new MarketFeedSimulator(mockRepo.Object);
        
        // Act
        var symbols = simulator.GetAvailableSymbols();
        
        // Assert
        symbols.Should().HaveCount(10);
        symbols.Should().Contain(new[] { "AAPL", "GOOGL", "MSFT", "TSLA", "AMZN", "META", "NVDA", "AMD", "NFLX", "DIS" });
    }

    [Fact]
    public async Task GenerateQuoteForSymbol_ShouldPersistToRepository()
    {
        // Arrange
        var mockRepo = CreateMockRepository();
        var simulator = new MarketFeedSimulator(mockRepo.Object);
        
        // Act
        var quote = await simulator.GenerateQuoteForSymbol("AAPL", CancellationToken.None);
        
        // Assert
        mockRepo.Verify(r => r.AddAsync(It.IsAny<Quote>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
