# ADR 006 – Caching avec Redis (Phase 2 - Étape 2a)

## Statut
Acceptée

## Contexte
Les tests de charge Phase 1 révèlent des **patterns de requêtes répétées** créant charge inutile :
* **Token validation** : ~50ms latency par requête authentifiée
* **MFA policies** : ~30ms query DB par login attempt
* **Account balance** : 12 requêtes/min/user = 1200 queries/min pour 100 users
* **Portfolio positions** : ~100ms query complexe

Objectifs Phase 2 :
* **Latency P95** : Réduire de 500ms → <100ms
* **Throughput** : ≥1200 req/s (limité à ~300-400 req/s actuellement)
* **Cache hit rate** : ≥80% pour réduire load DB

Le choix doit être performant (<5ms), scalable, simple (.NET), et compatible observabilité.

## Décision
Adopter **Redis 7** comme cache distribué in-memory avec client **StackExchange.Redis**, stratégie **cache-aside**, et monitoring Prometheus.

* **Technologie** : Redis 7 (image `redis:7-alpine`), client `StackExchange.Redis` NuGet
* **Port** : Redis sur 6379 (réseau Docker interne uniquement)
* **Stratégie** : Cache-aside (app vérifie cache → si miss, charge DB → écrit cache)
* **TTL** : Token validation 5min, MFA policies 5min, Account balance 1min, Portfolio 30s
* **Eviction** : `allkeys-lru` avec limite `maxmemory: 256mb`
* **Keys** : Convention `{namespace}:{entity}:{id}` (ex: `auth:client:uuid`)
* **Monitoring** : `INFO stats` Redis pour hit rate, Prometheus exporter

## Alternatives considérées

### Memcached
* **Avantages** : Légèrement plus rapide, protocole simple.
* **Inconvénients** : Pas de structures avancées, pas de persistence, pas de pub/sub.
* **Rejeté** : Redis plus polyvalent pour évolutions futures.

### In-process cache (IMemoryCache .NET)
* **Avantages** : Latency minimum (<1ms), pas de réseau, setup zero.
* **Inconvénients** : Cache non partagé entre réplicas, invalidation complexe.
* **Rejeté** : Phase 2 déploie 3+ réplicas, nécessite cache distribué.

## Conséquences

### Positives
* **Latency réduite** : P95 500ms → 80ms (-84%) après warm-up
* **Throughput** : 400 → 1200+ req/s (+200%)
* **Hit rate** : 85-92% mesuré, réduction load DB ~10x
* **Scalabilité** : Cache distribué entre réplicas API
* **Observabilité** : Logs `CACHE_HIT`/`CACHE_MISS`, métriques Prometheus

### Négatives
* **Complexité** : +1 composant, +1 port, mapping invalidation
* **Cohérence éventuelle** : TTL 1-5min = données potentiellement stales
* **Invalidation** : Logout, changements doivent invalider cache manuellement
* **Cold start** : Cache vide = latency Phase 1 (~500ms) pendant warm-up
* **SPOF** : Redis down → cache miss → DB surchargée

### Risques et mitigations
* **Risque** : Cache stampede (1000 requêtes simultanées sur clé expirée)
  * **Mitigation** : Lock pattern, TTL staggering (random ±10s)
* **Risque** : Invalidation incomplète (balance cached après dépôt)
  * **Mitigation** : Invalider immédiatement après write DB, tests E2E
* **Risque** : Redis OOM crash
  * **Mitigation** : `allkeys-lru` éviction, monitoring `used_memory` alerte à 80%

---

**Date** : 26 octobre 2025  
**Auteurs** : Équipe BrokerX  
**Révisions** : v1.0 (acceptée)
