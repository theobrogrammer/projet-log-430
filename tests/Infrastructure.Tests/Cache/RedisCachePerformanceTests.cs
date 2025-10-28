using FluentAssertions;
using ProjetLog430.Domain.Ports.Outbound;
using ProjetLog430.Infrastructure.Adapters.Cache;
using StackExchange.Redis;

namespace Infrastructure.Tests.Cache;

/// <summary>
/// Tests de performance pour Redis Cache.
/// Phase 2 Étape 2a - Benchmarking.
/// Compare les performances cache vs no-cache.
/// </summary>
public class RedisCachePerformanceTests : IAsyncLifetime
{
    private IConnectionMultiplexer? _redis;
    private ICachePort? _cache;
    private readonly string _connectionString;

    public RedisCachePerformanceTests()
    {
        // Utilise Redis existant (docker-compose)
        _connectionString = "localhost:6379,abortConnect=false";
    }

    public async Task InitializeAsync()
    {
        try
        {
            _redis = await ConnectionMultiplexer.ConnectAsync(_connectionString);
            _cache = new RedisCacheAdapter(_redis);
            
            // Clear cache avant les tests
            var endpoints = _redis.GetEndPoints();
            var server = _redis.GetServer(endpoints.First());
            await server.FlushDatabaseAsync();
        }
        catch (RedisConnectionException)
        {
            // Redis not available - tests will be skipped
            _redis = null;
            _cache = null;
        }
    }

    public async Task DisposeAsync()
    {
        _redis?.Dispose();
        await Task.CompletedTask;
    }

    [Fact(Skip = "Performance test - run manually")]
    public async Task CachePerformance_ShouldBeFasterThanNoCache()
    {
        // Arrange
        const int iterations = 100;
        var key = "perf:test:balance";
        var value = new WalletBalance
        {
            AccountId = Guid.NewGuid(),
            Balance = 1000.50m,
            Currency = "CAD",
            LastUpdated = DateTime.UtcNow
        };

        // Warm cache
        await _cache!.SetAsync(key, value, TimeSpan.FromMinutes(5));

        // Act - Measure cache hits
        var cacheStopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            var result = await _cache.GetAsync<WalletBalance>(key);
            result.Should().NotBeNull();
        }
        cacheStopwatch.Stop();

        // Act - Measure cache misses (simulate DB calls with delay)
        await _cache.RemoveAsync(key);
        var noCacheStopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            await Task.Delay(1); // Simulate DB latency (1ms)
            var result = new WalletBalance
            {
                AccountId = value.AccountId,
                Balance = value.Balance,
                Currency = value.Currency,
                LastUpdated = DateTime.UtcNow
            };
            result.Should().NotBeNull();
        }
        noCacheStopwatch.Stop();

        // Assert
        var cacheAvg = cacheStopwatch.ElapsedMilliseconds / (double)iterations;
        var noCacheAvg = noCacheStopwatch.ElapsedMilliseconds / (double)iterations;
        var improvement = ((noCacheAvg - cacheAvg) / noCacheAvg) * 100;

        // Log results
        Console.WriteLine($"Cache avg: {cacheAvg:F2}ms");
        Console.WriteLine($"No cache avg: {noCacheAvg:F2}ms");
        Console.WriteLine($"Improvement: {improvement:F1}%");

        // Cache should be at least 50% faster
        cacheAvg.Should().BeLessThan(noCacheAvg * 0.5);
    }

    [Fact(Skip = "Performance test - run manually")]
    public async Task ConcurrentWrites_ShouldHandleHighLoad()
    {
        // Arrange
        const int concurrentTasks = 50;
        const int operationsPerTask = 20;

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, concurrentTasks).Select(async taskId =>
        {
            for (int i = 0; i < operationsPerTask; i++)
            {
                var key = $"concurrent:task{taskId}:op{i}";
                var value = new TestData { Value = $"Data-{taskId}-{i}" };
                await _cache!.SetAsync(key, value, TimeSpan.FromMinutes(1));
            }
        });

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        var totalOps = concurrentTasks * operationsPerTask;
        var opsPerSecond = totalOps / (stopwatch.ElapsedMilliseconds / 1000.0);

        Console.WriteLine($"Total operations: {totalOps}");
        Console.WriteLine($"Time: {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Ops/sec: {opsPerSecond:F0}");

        // Should handle at least 500 ops/sec
        opsPerSecond.Should().BeGreaterThan(500);
    }

    [Fact]
    public async Task TTLExpiration_ShouldWorkCorrectly()
    {
        // Arrange
        var key = "ttl:test";
        var value = new TestData { Value = "Expires soon" };
        var ttl = TimeSpan.FromSeconds(2);

        // Act & Assert - Immediate get
        await _cache!.SetAsync(key, value, ttl);
        var result1 = await _cache.GetAsync<TestData>(key);
        result1.Should().NotBeNull();

        // Wait 1 second - should still exist
        await Task.Delay(TimeSpan.FromSeconds(1));
        var result2 = await _cache.GetAsync<TestData>(key);
        result2.Should().NotBeNull();

        // Wait 2 more seconds - should be expired
        await Task.Delay(TimeSpan.FromSeconds(2));
        var result3 = await _cache.GetAsync<TestData>(key);
        result3.Should().BeNull();
    }

    [Fact]
    public async Task LargePayload_ShouldBeHandled()
    {
        // Arrange
        var key = "large:payload";
        var value = new LargeObject
        {
            Id = Guid.NewGuid(),
            Data = string.Join("", Enumerable.Repeat("X", 10_000)) // 10KB string
        };

        // Act
        await _cache!.SetAsync(key, value);
        var result = await _cache.GetAsync<LargeObject>(key);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(value.Id);
        result.Data.Length.Should().Be(10_000);
    }

    // Test data classes
    private class WalletBalance
    {
        public Guid AccountId { get; set; }
        public decimal Balance { get; set; }
        public string Currency { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; }
    }

    private class TestData
    {
        public string Value { get; set; } = string.Empty;
    }

    private class LargeObject
    {
        public Guid Id { get; set; }
        public string Data { get; set; } = string.Empty;
    }
}
