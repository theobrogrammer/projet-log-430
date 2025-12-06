using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using ProjetLog430.Infrastructure.Web.Hubs;

namespace Infrastructure.Tests.MarketData;

/// <summary>
/// Unit tests for UC-04: Real-Time Market Data Feed - MarketDataHub
/// </summary>
public class MarketDataHubTests
{
    [Fact]
    public async Task SubscribeToSymbol_ShouldAddToGroup()
    {
        // Arrange
        var mockCaller = new Mock<ISingleClientProxy>();
        var mockClients = new Mock<IHubCallerClients>();
        var mockGroups = new Mock<IGroupManager>();
        var mockContext = new Mock<HubCallerContext>();
        
        mockContext.Setup(c => c.ConnectionId).Returns("test-connection-id");
        mockClients.Setup(c => c.Caller).Returns(mockCaller.Object);
        
        var hub = new MarketDataHub
        {
            Clients = mockClients.Object,
            Groups = mockGroups.Object,
            Context = mockContext.Object
        };
        
        // Act
        await hub.SubscribeToSymbol("AAPL");
        
        // Assert
        mockGroups.Verify(
            g => g.AddToGroupAsync("test-connection-id", "AAPL", default),
            Times.Once
        );
    }

    [Fact]
    public async Task UnsubscribeFromSymbol_ShouldRemoveFromGroup()
    {
        // Arrange
        var mockCaller = new Mock<ISingleClientProxy>();
        var mockClients = new Mock<IHubCallerClients>();
        var mockGroups = new Mock<IGroupManager>();
        var mockContext = new Mock<HubCallerContext>();
        
        mockContext.Setup(c => c.ConnectionId).Returns("test-connection-id");
        mockClients.Setup(c => c.Caller).Returns(mockCaller.Object);
        
        var hub = new MarketDataHub
        {
            Clients = mockClients.Object,
            Groups = mockGroups.Object,
            Context = mockContext.Object
        };
        
        // Act
        await hub.UnsubscribeFromSymbol("AAPL");
        
        // Assert
        mockGroups.Verify(
            g => g.RemoveFromGroupAsync("test-connection-id", "AAPL", default),
            Times.Once
        );
    }

    [Fact]
    public async Task SubscribeToMultipleSymbols_ShouldAddToAllGroups()
    {
        // Arrange
        var mockCaller = new Mock<ISingleClientProxy>();
        var mockClients = new Mock<IHubCallerClients>();
        var mockGroups = new Mock<IGroupManager>();
        var mockContext = new Mock<HubCallerContext>();
        
        mockContext.Setup(c => c.ConnectionId).Returns("test-connection-id");
        mockClients.Setup(c => c.Caller).Returns(mockCaller.Object);
        
        var hub = new MarketDataHub
        {
            Clients = mockClients.Object,
            Groups = mockGroups.Object,
            Context = mockContext.Object
        };
        
        // Act
        await hub.SubscribeToSymbol("AAPL");
        await hub.SubscribeToSymbol("GOOGL");
        await hub.SubscribeToSymbol("MSFT");
        
        // Assert
        mockGroups.Verify(
            g => g.AddToGroupAsync("test-connection-id", "AAPL", default),
            Times.Once
        );
        mockGroups.Verify(
            g => g.AddToGroupAsync("test-connection-id", "GOOGL", default),
            Times.Once
        );
        mockGroups.Verify(
            g => g.AddToGroupAsync("test-connection-id", "MSFT", default),
            Times.Once
        );
    }



    [Fact]
    public async Task SubscribeToSymbol_ShouldIncrementSubscriptionCount()
    {
        // Arrange
        var mockCaller = new Mock<ISingleClientProxy>();
        var mockClients = new Mock<IHubCallerClients>();
        var mockGroups = new Mock<IGroupManager>();
        var mockContext = new Mock<HubCallerContext>();
        
        mockContext.Setup(c => c.ConnectionId).Returns("test-connection-id-subs");
        mockClients.Setup(c => c.Caller).Returns(mockCaller.Object);
        
        var hub = new MarketDataHub
        {
            Clients = mockClients.Object,
            Groups = mockGroups.Object,
            Context = mockContext.Object
        };
        
        var initialCount = MarketDataHub.GetSubscriptionCount("TSLA");
        
        // Act
        await hub.SubscribeToSymbol("TSLA");
        
        // Assert
        var newCount = MarketDataHub.GetSubscriptionCount("TSLA");
        newCount.Should().Be(initialCount + 1);
    }

    [Fact]
    public async Task UnsubscribeFromSymbol_ShouldDecrementSubscriptionCount()
    {
        // Arrange
        var mockCaller = new Mock<ISingleClientProxy>();
        var mockClients = new Mock<IHubCallerClients>();
        var mockGroups = new Mock<IGroupManager>();
        var mockContext = new Mock<HubCallerContext>();
        
        mockContext.Setup(c => c.ConnectionId).Returns("test-connection-id-unsubs");
        mockClients.Setup(c => c.Caller).Returns(mockCaller.Object);
        
        var hub = new MarketDataHub
        {
            Clients = mockClients.Object,
            Groups = mockGroups.Object,
            Context = mockContext.Object
        };
        
        await hub.SubscribeToSymbol("META");
        var countAfterSubscribe = MarketDataHub.GetSubscriptionCount("META");
        
        // Act
        await hub.UnsubscribeFromSymbol("META");
        
        // Assert
        var countAfterUnsubscribe = MarketDataHub.GetSubscriptionCount("META");
        countAfterUnsubscribe.Should().Be(countAfterSubscribe - 1);
    }
}
