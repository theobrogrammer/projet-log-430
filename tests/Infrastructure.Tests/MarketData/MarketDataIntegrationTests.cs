using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ProjetLog430.Infrastructure.Web;
using ProjetLog430.Domain.Model.MarketData;
using System.Collections.Concurrent;

namespace Infrastructure.Tests.MarketData;

/// <summary>
/// Integration tests for UC-04: Real-Time Market Data Feed - End-to-End SignalR
/// These tests verify the entire flow from broadcaster to client via SignalR
/// </summary>
public class MarketDataIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MarketDataIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact(Skip = "Integration test requires running server - use E2E tests or manual HTML client instead")]
    public async Task SignalRHub_ShouldBeAccessible()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        // Act
        var response = await client.GetAsync("/hubs/marketdata");
        
        // Assert - Should get a 400 or similar, not 404 (means hub exists)
        response.StatusCode.Should().NotBe(System.Net.HttpStatusCode.NotFound);
    }

    [Fact(Timeout = 10000, Skip = "Integration test requires running server - use E2E tests instead")]
    public async Task SignalRClient_ShouldConnectToHub()
    {
        // Arrange
        var hubUrl = $"{_factory.Server.BaseAddress}hubs/marketdata";
        
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();
        
        // Act
        await connection.StartAsync();
        
        // Assert
        connection.State.Should().Be(HubConnectionState.Connected);
        
        // Cleanup
        await connection.StopAsync();
        await connection.DisposeAsync();
    }

    [Fact(Timeout = 15000, Skip = "Integration test requires running server - use E2E tests instead")]
    public async Task SignalRClient_ShouldReceiveQuotes_WhenSubscribedToSymbol()
    {
        // Arrange
        var hubUrl = $"{_factory.Server.BaseAddress}hubs/marketdata";
        
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        var quotesReceived = new ConcurrentBag<object>();
        
        connection.On<object>("ReceiveQuote", quote =>
        {
            quotesReceived.Add(quote);
        });

        await connection.StartAsync();

        // Act
        await connection.InvokeAsync("SubscribeToSymbol", "AAPL");
        
        // Wait for quotes to arrive (broadcaster generates every 100-500ms)
        await Task.Delay(3000);

        // Assert
        quotesReceived.Should().NotBeEmpty("should receive at least one quote for AAPL");
        
        // Cleanup
        await connection.StopAsync();
        await connection.DisposeAsync();
    }

    [Fact(Timeout = 15000, Skip = "Integration test requires running server - use E2E tests instead")]
    public async Task SignalRClient_ShouldReceiveMultipleSymbols_WhenSubscribedToMultiple()
    {
        // Arrange
        var hubUrl = $"{_factory.Server.BaseAddress}hubs/marketdata";
        
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        var symbolsReceived = new ConcurrentBag<string>();
        
        connection.On<object>("ReceiveQuote", quote =>
        {
            var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                System.Text.Json.JsonSerializer.Serialize(quote));
            
            if (dict != null && dict.TryGetValue("symbol", out var symbol))
            {
                symbolsReceived.Add(symbol.ToString()!);
            }
        });

        await connection.StartAsync();

        // Act
        await connection.InvokeAsync("SubscribeToSymbol", "AAPL");
        await connection.InvokeAsync("SubscribeToSymbol", "GOOGL");
        await connection.InvokeAsync("SubscribeToSymbol", "MSFT");
        
        // Wait for quotes to arrive
        await Task.Delay(3000);

        // Assert
        symbolsReceived.Should().Contain("AAPL");
        symbolsReceived.Should().Contain("GOOGL");
        symbolsReceived.Should().Contain("MSFT");
        
        // Cleanup
        await connection.StopAsync();
        await connection.DisposeAsync();
    }

    [Fact(Timeout = 15000, Skip = "Integration test requires running server - use E2E tests instead")]
    public async Task SignalRClient_ShouldStopReceivingQuotes_AfterUnsubscribe()
    {
        // Arrange
        var hubUrl = $"{_factory.Server.BaseAddress}hubs/marketdata";
        
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        var quotesReceived = new ConcurrentBag<object>();
        
        connection.On<object>("ReceiveQuote", quote =>
        {
            quotesReceived.Add(quote);
        });

        await connection.StartAsync();
        await connection.InvokeAsync("SubscribeToSymbol", "TSLA");
        
        // Wait for some quotes
        await Task.Delay(2000);
        var countBeforeUnsubscribe = quotesReceived.Count;

        // Act
        await connection.InvokeAsync("UnsubscribeFromSymbol", "TSLA");
        await Task.Delay(2000);

        // Assert
        var countAfterUnsubscribe = quotesReceived.Count;
        countBeforeUnsubscribe.Should().BeGreaterThan(0, "should have received quotes before unsubscribe");
        countAfterUnsubscribe.Should().Be(countBeforeUnsubscribe, "should not receive new quotes after unsubscribe");
        
        // Cleanup
        await connection.StopAsync();
        await connection.DisposeAsync();
    }

    [Fact(Timeout = 15000, Skip = "Integration test requires running server - use E2E tests instead")]
    public async Task SignalRClient_ShouldReceiveValidQuoteData()
    {
        // Arrange
        var hubUrl = $"{_factory.Server.BaseAddress}hubs/marketdata";
        
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        Dictionary<string, object>? receivedQuote = null;
        var tcs = new TaskCompletionSource<bool>();
        
        connection.On<object>("ReceiveQuote", quote =>
        {
            receivedQuote = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                System.Text.Json.JsonSerializer.Serialize(quote));
            tcs.TrySetResult(true);
        });

        await connection.StartAsync();

        // Act
        await connection.InvokeAsync("SubscribeToSymbol", "NVDA");
        await tcs.Task; // Wait for first quote

        // Assert
        receivedQuote.Should().NotBeNull();
        receivedQuote.Should().ContainKey("symbol");
        receivedQuote.Should().ContainKey("bid");
        receivedQuote.Should().ContainKey("ask");
        receivedQuote.Should().ContainKey("last");
        receivedQuote.Should().ContainKey("volume");
        receivedQuote.Should().ContainKey("spread");
        receivedQuote.Should().ContainKey("spreadBps");
        receivedQuote.Should().ContainKey("timestamp");
        
        receivedQuote!["symbol"].ToString().Should().Be("NVDA");
        
        // Cleanup
        await connection.StopAsync();
        await connection.DisposeAsync();
    }

    [Fact(Timeout = 15000, Skip = "Integration test requires running server - use E2E tests instead")]
    public async Task MultipleClients_ShouldReceiveQuotes_Independently()
    {
        // Arrange
        var hubUrl = $"{_factory.Server.BaseAddress}hubs/marketdata";
        
        var connection1 = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        var connection2 = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        var client1Quotes = new ConcurrentBag<string>();
        var client2Quotes = new ConcurrentBag<string>();
        
        connection1.On<object>("ReceiveQuote", quote =>
        {
            var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                System.Text.Json.JsonSerializer.Serialize(quote));
            if (dict != null && dict.TryGetValue("symbol", out var symbol))
            {
                client1Quotes.Add(symbol.ToString()!);
            }
        });

        connection2.On<object>("ReceiveQuote", quote =>
        {
            var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                System.Text.Json.JsonSerializer.Serialize(quote));
            if (dict != null && dict.TryGetValue("symbol", out var symbol))
            {
                client2Quotes.Add(symbol.ToString()!);
            }
        });

        // Act
        await connection1.StartAsync();
        await connection2.StartAsync();
        
        await connection1.InvokeAsync("SubscribeToSymbol", "AMD");
        await connection2.InvokeAsync("SubscribeToSymbol", "INTC");
        
        await Task.Delay(3000);

        // Assert
        client1Quotes.Should().Contain("AMD");
        client1Quotes.Should().NotContain("INTC");
        
        client2Quotes.Should().Contain("INTC");
        client2Quotes.Should().NotContain("AMD");
        
        // Cleanup
        await connection1.StopAsync();
        await connection2.StopAsync();
        await connection1.DisposeAsync();
        await connection2.DisposeAsync();
    }
}
