# ADR 005 – API Gateway avec KrakenD (Phase 2 - Étape 2b)

## Statut
Acceptée

## Contexte
L'évolution vers **microservices** (Orders, Portfolio, Reporting) nécessite :
* **Routage centralisé** : Point d'entrée unique pour clients, routage vers services backend appropriés
* **Rate limiting** : Protection contre abus (100 req/s par client)
* **Circuit breaker** : Isolation pannes entre services
* **Agrégation réponses** : Combiner données de plusieurs microservices
* **Monitoring centralisé** : Métriques à la frontière système

Le choix doit être performant (overhead <50ms), simple (config JSON), léger (<50MB), et compatible Prometheus.

## Décision
Adopter **KrakenD** comme API Gateway avec configuration JSON statique et intégration Prometheus.

* **Technologie** : KrakenD CE (image `devopsfaith/krakend:latest`)
* **Port** : Gateway sur port 8000, forward vers backends (8080-8083)
* **Rate limiting** : 100 req/s par endpoint, circuit breaker après 5 erreurs
* **Timeout** : 5s global, cache TTL 60s pour endpoints read-only
* **Routing** : `/api/v1/*` → API monolithe, `/orders/*` → Orders service, `/portfolio/*` → Portfolio, `/reports/*` → Reporting
* **Agrégation** : Endpoint `/dashboard` combine portfolio + orders + analytics
* **Monitoring** : Métriques Prometheus sur `/__stats`

## Alternatives considérées

### Kong Gateway
* **Avantages** : Écosystème riche (plugins OAuth, JWT), dashboard admin UI.
* **Inconvénients** : Nécessite PostgreSQL, RAM élevée (~300MB), complexité setup.
* **Rejeté** : Overhead infrastructure non justifié pour projet académique.

### Ocelot (.NET)
* **Avantages** : Native .NET, configuration JSON familière.
* **Inconvénients** : Performance inférieure, overhead .NET runtime, moins mature.
* **Rejeté** : KrakenD plus performant et configuration déclarative simple.

## Conséquences

### Positives
* **Single entry point** : Isolation services backend, meilleure sécurité
* **Performance** : Overhead <50ms P95 mesuré avec k6
* **Rate limiting** : Protection automatique (429 Too Many Requests)
* **Circuit breaker** : Isolation pannes (service down → 503 immédiat)
* **Agrégation** : Réduction 3 requêtes frontend → 1 requête gateway
* **Monitoring** : Dashboard Prometheus centralisé

### Négatives
* **SPOF** : KrakenD down → API inaccessible
* **Overhead** : +40-50ms latence par requête
* **Configuration statique** : Modification config nécessite reload
* **Debugging** : Erreurs peuvent venir gateway OU backend
* **Cache invalidation** : TTL 60s peut servir données stales

### Risques et mitigations
* **Risque** : Circuit breaker trop sensible
  * **Mitigation** : Tuning `max_errors` basé sur profiling
* **Risque** : Rate limiting bloque utilisateurs légitimes
  * **Mitigation** : Whitelist IPs internes, monitoring 429 count
* **Risque** : Gateway devient bottleneck
  * **Mitigation** : Tests scaling horizontal (2-3 réplicas)

---

**Date** : 26 octobre 2025  
**Auteurs** : Équipe BrokerX  
**Révisions** : v1.0 (acceptée)
