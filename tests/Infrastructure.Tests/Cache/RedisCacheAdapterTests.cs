using FluentAssertions;
using ProjetLog430.Domain.Ports.Outbound;
using ProjetLog430.Infrastructure.Adapters.Cache;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace Infrastructure.Tests.Cache;

/// <summary>
/// Tests d'intégration pour RedisCacheAdapter.
/// Phase 2 Étape 2a - Redis Cache.
/// Utilise Testcontainers pour démarrer Redis automatiquement.
/// </summary>
public class RedisCacheAdapterTests : IAsyncLifetime
{
    private RedisContainer? _redisContainer;
    private IConnectionMultiplexer? _redis;
    private ICachePort? _cache;

    public async Task InitializeAsync()
    {
        // Démarrer Redis dans un conteneur Docker temporaire
        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

        await _redisContainer.StartAsync();

        // Connexion Redis
        var connectionString = _redisContainer.GetConnectionString();
        _redis = await ConnectionMultiplexer.ConnectAsync(connectionString);
        _cache = new RedisCacheAdapter(_redis);
    }

    public async Task DisposeAsync()
    {
        _redis?.Dispose();
        if (_redisContainer != null)
            await _redisContainer.DisposeAsync();
    }

    [Fact]
    public async Task SetAsync_ShouldStoreValue_WithoutTTL()
    {
        // Arrange
        var key = "test:key:1";
        var value = new TestObject { Id = 1, Name = "Test" };

        // Act
        await _cache!.SetAsync(key, value);

        // Assert
        var result = await _cache.GetAsync<TestObject>(key);
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.Name.Should().Be("Test");
    }

    [Fact]
    public async Task SetAsync_ShouldStoreValue_WithTTL()
    {
        // Arrange
        var key = "test:key:ttl";
        var value = new TestObject { Id = 2, Name = "TTL Test" };
        var ttl = TimeSpan.FromSeconds(2);

        // Act
        await _cache!.SetAsync(key, value, ttl);

        // Assert - immediate get
        var result1 = await _cache.GetAsync<TestObject>(key);
        result1.Should().NotBeNull();

        // Wait for expiration
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Assert - should be expired
        var result2 = await _cache.GetAsync<TestObject>(key);
        result2.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNull_WhenKeyDoesNotExist()
    {
        // Arrange
        var key = "test:nonexistent";

        // Act
        var result = await _cache!.GetAsync<TestObject>(key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_ShouldDeleteKey()
    {
        // Arrange
        var key = "test:key:remove";
        var value = new TestObject { Id = 3, Name = "Remove Test" };
        await _cache!.SetAsync(key, value);

        // Act
        await _cache.RemoveAsync(key);

        // Assert
        var result = await _cache.GetAsync<TestObject>(key);
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenKeyExists()
    {
        // Arrange
        var key = "test:key:exists";
        var value = new TestObject { Id = 4, Name = "Exists Test" };
        await _cache!.SetAsync(key, value);

        // Act
        var exists = await _cache.ExistsAsync(key);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenKeyDoesNotExist()
    {
        // Arrange
        var key = "test:key:notexists";

        // Act
        var exists = await _cache!.ExistsAsync(key);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveByPatternAsync_ShouldDeleteMatchingKeys()
    {
        // Arrange
        await _cache!.SetAsync("wallet:balance:user1", new TestObject { Id = 1, Name = "User1" });
        await _cache!.SetAsync("wallet:balance:user2", new TestObject { Id = 2, Name = "User2" });
        await _cache!.SetAsync("mfa:challenge:123", new TestObject { Id = 3, Name = "Challenge" });

        // Act
        await _cache!.RemoveByPatternAsync("wallet:balance:*");

        // Assert
        var user1 = await _cache.GetAsync<TestObject>("wallet:balance:user1");
        var user2 = await _cache.GetAsync<TestObject>("wallet:balance:user2");
        var challenge = await _cache.GetAsync<TestObject>("mfa:challenge:123");

        user1.Should().BeNull();
        user2.Should().BeNull();
        challenge.Should().NotBeNull(); // Should not be deleted
    }

    [Fact]
    public async Task SetAsync_ShouldOverwriteExistingValue()
    {
        // Arrange
        var key = "test:key:overwrite";
        var value1 = new TestObject { Id = 1, Name = "Original" };
        var value2 = new TestObject { Id = 2, Name = "Updated" };

        // Act
        await _cache!.SetAsync(key, value1);
        await _cache.SetAsync(key, value2);

        // Assert
        var result = await _cache.GetAsync<TestObject>(key);
        result.Should().NotBeNull();
        result!.Id.Should().Be(2);
        result.Name.Should().Be("Updated");
    }

    [Fact]
    public async Task GetAsync_ShouldHandleComplexObjects()
    {
        // Arrange
        var key = "test:key:complex";
        var value = new ComplexObject
        {
            Id = Guid.NewGuid(),
            Metadata = new Dictionary<string, string>
            {
                ["key1"] = "value1",
                ["key2"] = "value2"
            },
            Tags = new List<string> { "tag1", "tag2", "tag3" },
            Timestamp = DateTime.UtcNow
        };

        // Act
        await _cache!.SetAsync(key, value);
        var result = await _cache.GetAsync<ComplexObject>(key);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(value.Id);
        result.Metadata.Should().BeEquivalentTo(value.Metadata);
        result.Tags.Should().BeEquivalentTo(value.Tags);
        result.Timestamp.Should().BeCloseTo(value.Timestamp, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ConcurrentAccess_ShouldHandleMultipleOperations()
    {
        // Arrange
        var tasks = new List<Task>();

        // Act - Multiple concurrent writes
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                var key = $"test:concurrent:{index}";
                var value = new TestObject { Id = index, Name = $"Concurrent {index}" };
                await _cache!.SetAsync(key, value);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - All keys should exist
        for (int i = 0; i < 10; i++)
        {
            var key = $"test:concurrent:{i}";
            var result = await _cache!.GetAsync<TestObject>(key);
            result.Should().NotBeNull();
            result!.Id.Should().Be(i);
        }
    }

    // Test objects
    private class TestObject
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class ComplexObject
    {
        public Guid Id { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }
}
