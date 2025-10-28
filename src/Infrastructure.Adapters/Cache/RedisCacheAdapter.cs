using System.Diagnostics;
using System.Text.Json;
using ProjetLog430.Domain.Ports.Outbound;
using Prometheus;
using Serilog;
using StackExchange.Redis;

namespace ProjetLog430.Infrastructure.Adapters.Cache;

/// <summary>
/// Adaptateur Redis pour le cache distribué.
/// Implémente ICachePort (architecture hexagonale).
/// Phase 2 Étape 2a - Performance avec Redis.
/// </summary>
public sealed class RedisCacheAdapter : ICachePort
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly JsonSerializerOptions _jsonOptions;

    // Métriques Prometheus pour cache operations
    private static readonly Counter CacheOperations = Metrics.CreateCounter(
        "cache_operations_total", 
        "Total cache operations by type and key prefix",
        new CounterConfiguration { LabelNames = new[] { "operation", "key_prefix" } });

    private static readonly Histogram CacheLatency = Metrics.CreateHistogram(
        "cache_operation_duration_seconds",
        "Cache operation latency in seconds",
        new HistogramConfiguration { LabelNames = new[] { "operation" } });

    public RedisCacheAdapter(IConnectionMultiplexer redis)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _db = _redis.GetDatabase();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        
        // Log connexion Redis
        var endpoints = string.Join(", ", _redis.GetEndPoints().Select(e => e.ToString()));
        Log.Information("💾 RedisCacheAdapter initialisé - Endpoints: {Endpoints}, Status: {Status}", 
            endpoints, _redis.IsConnected ? "CONNECTED" : "DISCONNECTED");
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var value = await _db.StringGetAsync(key);
            sw.Stop();
            CacheLatency.WithLabels("get").Observe(sw.Elapsed.TotalSeconds);
            
            if (value.IsNullOrEmpty)
            {
                CacheOperations.WithLabels("miss", GetKeyPrefix(key)).Inc();
                Log.Information("CACHE_MISS - Clé: {Key}, Durée: {ElapsedMs}ms", key, sw.ElapsedMilliseconds);
                return null;
            }

            var result = JsonSerializer.Deserialize<T>(value!, _jsonOptions);
            CacheOperations.WithLabels("hit", GetKeyPrefix(key)).Inc();
            Log.Information("CACHE_HIT - Clé: {Key}, Durée: {ElapsedMs}ms", key, sw.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            CacheOperations.WithLabels("error", GetKeyPrefix(key)).Inc();
            Log.Error(ex, "CACHE_ERROR - GetAsync échoué pour clé: {Key}", key);
            return null; // Fallback gracieux - ne pas casser l'app si Redis down
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default) where T : class
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            
            if (ttl.HasValue)
            {
                await _db.StringSetAsync(key, json, ttl.Value);
            }
            else
            {
                await _db.StringSetAsync(key, json);
            }

            sw.Stop();
            CacheLatency.WithLabels("set").Observe(sw.Elapsed.TotalSeconds);
            CacheOperations.WithLabels("set", GetKeyPrefix(key)).Inc();
            
            Log.Information("CACHE_SET - Clé: {Key}, TTL: {TTL}, Durée: {ElapsedMs}ms", 
                key, ttl?.TotalSeconds.ToString("F0") + "s" ?? "NONE", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            CacheOperations.WithLabels("error", GetKeyPrefix(key)).Inc();
            Log.Error(ex, "CACHE_ERROR - SetAsync échoué pour clé: {Key}", key);
            // Ne pas throw - graceful degradation
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var deleted = await _db.KeyDeleteAsync(key);
            sw.Stop();
            CacheLatency.WithLabels("remove").Observe(sw.Elapsed.TotalSeconds);
            CacheOperations.WithLabels("remove", GetKeyPrefix(key)).Inc();
            Log.Information("CACHE_REMOVE - Clé: {Key}, Supprimée: {Deleted}, Durée: {ElapsedMs}ms", 
                key, deleted, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            CacheOperations.WithLabels("error", GetKeyPrefix(key)).Inc();
            Log.Error(ex, "CACHE_ERROR - RemoveAsync échoué pour clé: {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var exists = await _db.KeyExistsAsync(key);
            Log.Debug("CACHE_EXISTS - Clé: {Key}, Existe: {Exists}", key, exists);
            return exists;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CACHE_ERROR - ExistsAsync échoué pour clé: {Key}", key);
            return false;
        }
    }

    public async Task RemoveByPatternAsync(string pattern, CancellationToken ct = default)
    {
        try
        {
            var endpoints = _redis.GetEndPoints();
            var server = _redis.GetServer(endpoints.First());
            
            var keys = server.Keys(pattern: pattern, pageSize: 1000).ToArray();
            
            if (keys.Length > 0)
            {
                await _db.KeyDeleteAsync(keys);
                Log.Information("CACHE_REMOVE_PATTERN - Pattern: {Pattern}, Supprimées: {Count}", pattern, keys.Length);
                // CacheOperations.WithLabels("remove_pattern", GetKeyPrefix(pattern)).Inc();
            }
            else
            {
                Log.Debug("CACHE_REMOVE_PATTERN - Pattern: {Pattern}, Aucune clé trouvée", pattern);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CACHE_ERROR - RemoveByPatternAsync échoué pour pattern: {Pattern}", pattern);
            // CacheOperations.WithLabels("error", GetKeyPrefix(pattern)).Inc();
        }
    }

    /// <summary>
    /// Extrait le préfixe de la clé pour les métriques (ex: "wallet:balance:123" → "wallet").
    /// </summary>
    private static string GetKeyPrefix(string key)
    {
        var parts = key.Split(':');
        return parts.Length > 0 ? parts[0] : "unknown";
    }
}
