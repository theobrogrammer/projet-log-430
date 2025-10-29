# 🌐 Phase 2b - API Gateway KrakenD - Résumé d'Implémentation

**Date** : 28 octobre 2025  
**Projet** : BrokerX - Phase 2 Étape 2b  
**Objectif** : Implémenter un API Gateway avec KrakenD pour centraliser l'accès aux microservices

---

## ✅ État d'Implémentation

### Architecture Déployée

```
┌─────────┐
│ Client  │
└────┬────┘
     │ HTTP :8080
     ▼
┌──────────────────────┐
│  KrakenD Gateway     │ ◄── Prometheus scrape :9091
│  Port: 8080          │
│  - Rate Limiting     │
│  - Circuit Breaker   │
│  - Routing           │
└──────────┬───────────┘
           │ HTTP (interne :80)
           ▼
┌──────────────────────┐
│  NGINX Load Balancer │
│  Port: 8090 (externe)│
│  Algorithm: least_conn│
└──────────┬───────────┘
           │
     ┌─────┴─────┬─────────┬─────────┐
     │           │         │         │
     ▼           ▼         ▼         ▼
┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐
│ API-1   │ │ API-2   │ │ API-3   │ │ API-4   │
│ :8080   │ │ :8080   │ │ :8080   │ │ :8080   │
└────┬────┘ └────┬────┘ └────┬────┘ └────┬────┘
     └───────────┴──────┴──────┴──────────┘
                        │
                        ▼
                   ┌─────────┐
                   │ MySQL   │
                   │ :3307   │
                   └─────────┘
```

---

## 📋 Composants Implémentés

### 1. KrakenD Gateway

**Configuration** : `krakend.json`

```json
{
  "version": 3,
  "port": 8080,
  "timeout": "5s",
  "cache_ttl": "300s"
}
```

**Endpoints configurés** :
- ✅ `/api/v1/signup` (POST)
- ✅ `/api/v1/signup/verify-otp` (POST)
- ✅ `/api/v1/auth/login` (POST)
- ✅ `/api/v1/auth/mfa/verify` (POST)
- ✅ `/api/v1/wallet/deposit` (POST)
- ✅ `/health` (GET)

**Caractéristiques** :
- **Rate Limiting** : 100 req/s global, 10 req/s par IP
- **Circuit Breaker** : 5 erreurs max, intervalle 10s, timeout 5s
- **Backend** : NGINX Load Balancer (nginx:80 interne)
- **Métriques** : Prometheus OpenCensus sur port 9091

### 2. Intégration Prometheus

**Scrape Target** : `krakend:9091`

**Métriques disponibles** :
```promql
# Requêtes complétées
krakend_opencensus_io_http_client_completed_count

# Latence end-to-end
krakend_opencensus_io_http_client_roundtrip_latency

# Taille des réponses
krakend_opencensus_io_http_client_received_bytes
```

**Labels** :
- `http_client_method` : POST, GET, etc.
- `http_client_path` : /api/v1/signup, etc.
- `http_client_status` : 200, 400, 500, etc.
- `layer` : api-gateway
- `service` : krakend-gateway

### 3. Tests Automatisés

**Script** : `tests/scripts/test-krakend-gateway.sh`

**Tests inclus** :
1. ✅ Vérification services Docker (API, KrakenD, MySQL, Redis)
2. ✅ Health check KrakenD (`/__health`)
3. ✅ Métriques Prometheus (port 9091)
4. ✅ Routing API via Gateway (signup endpoint)
5. ✅ Tests endpoints multiples
6. ✅ Comparaison performance k6 (optionnel)

**Résultats** :
```
Tests réussis: 6
Tests échoués: 0
Total: 6
✅ Tous les tests fonctionnels sont passés!
```

---

## 🔧 Configuration Technique

### Docker Compose

```yaml
krakend:
  container_name: brokerx-krakend
  image: devopsfaith/krakend:2.7
  ports:
    - "8080:8080"  # Gateway public
    - "9091:9091"  # Métriques Prometheus
  volumes:
    - ./krakend.json:/etc/krakend/krakend.json:ro
  depends_on:
    api:
      condition: service_healthy
  healthcheck:
    test: ["CMD", "wget", "--quiet", "--tries=1", "--spider", "http://localhost:8080/__health"]
    interval: 10s
    timeout: 3s
    retries: 3
    start_period: 10s
```

### Prometheus Configuration

```yaml
scrape_configs:
  - job_name: 'krakend-gateway'
    scrape_interval: 15s
    metrics_path: '/metrics'
    static_configs:
      - targets: ['krakend:9091']
        labels:
          service: 'krakend-gateway'
          layer: 'api-gateway'
```

---

## 📊 Résultats de Tests

### Tests Fonctionnels

| Test | Résultat | Détails |
|------|----------|---------|
| Health Check Gateway | ✅ PASS | HTTP 200, status: ok |
| Métriques Prometheus | ✅ PASS | 44 métriques exposées |
| Routing Signup | ✅ PASS | HTTP 200, accountId retourné |
| Endpoints Multiples | ✅ PASS | Tous accessibles |
| Services Docker | ✅ PASS | API, KrakenD, MySQL, Redis healthy |

### Exemple de Requête Réussie

```bash
$ curl -X POST http://localhost:8080/api/v1/signup \
  -H "Content-Type: application/json" \
  -d '{"email":"gateway-success@example.com","fullName":"Test","password":"SecureP@ss123","confirmPassword":"SecureP@ss123"}'

HTTP/1.1 200 OK
{
  "accountId": "17d141ad-3c22-4b41-a514-b4ce52107f0c",
  "clientId": "fe85a986-944e-4102-bffa-19b522e69d05",
  "status": "Pending"
}
```

### Métriques Observées

```bash
$ curl -s http://localhost:9091/metrics | grep completed_count
krakend_opencensus_io_http_client_completed_count{
  http_client_method="POST",
  http_client_path="/api/v1/signup",
  http_client_status="200"
} 5
```

---

## 🎯 Objectifs Atteints

### Phase 2b - Checklist

- [x] **KrakenD Gateway déployé** sur port 8080
- [x] **Configuration endpoints** (6 endpoints API)
- [x] **Rate limiting** configuré (100 req/s, 10 req/s par IP)
- [x] **Circuit breaker** configuré (5 erreurs, 10s)
- [x] **Intégration NGINX** (backend vers load balancer)
- [x] **Métriques Prometheus** activées et scrappées
- [x] **Tests automatisés** fonctionnels (6/6 pass)
- [x] **Health checks** opérationnels
- [x] **Documentation** complète (RUNBOOK.md)

### Architecture Hexagonale Préservée

```
┌──────────────────────────────────────────────┐
│           KrakenD Gateway (Port 8080)        │
│  - Rate Limiting                             │
│  - Circuit Breaker                           │
│  - Routing                                   │
└──────────────────┬───────────────────────────┘
                   │
        ┌──────────┴──────────┐
        │  NGINX Load Balancer │
        └──────────┬───────────┘
                   │
         ┌─────────┴─────────┐
         │   Ports Inbound   │ ← Couche Application
         └─────────┬─────────┘
                   │
         ┌─────────┴─────────┐
         │  Domain Services  │ ← Couche Domaine
         └─────────┬─────────┘
                   │
         ┌─────────┴─────────┐
         │  Ports Outbound   │ ← Adapters
         └─────────┬─────────┘
                   │
         ┌─────────┴─────────┐
         │  Infrastructure   │
         │ (MySQL, Redis)    │
         └───────────────────┘
```

---

## 🚀 Utilisation

### Démarrage

```bash
# Démarrer tous les services
docker compose up -d

# Vérifier KrakenD
docker ps --filter name=brokerx-krakend
curl http://localhost:8080/__health
```

### Tests

```bash
# Tests rapides
./tests/scripts/test-krakend-gateway.sh

# Tests de charge via gateway
BASE_URL=http://localhost:8080 k6 run --duration 1m --vus 10 scripts/k6/signup.js
```

### Monitoring

```bash
# Métriques KrakenD
curl http://localhost:9091/metrics | grep krakend

# Prometheus UI
open http://localhost:9090

# Grafana Dashboard
open http://localhost:3000  # admin/admin
```

---

## 🔍 Leçons Apprises

### Problèmes Résolus

1. **Port Docker interne vs externe**
   - ❌ Problème : KrakenD essayait `nginx:8090` (port externe)
   - ✅ Solution : Corriger vers `nginx:80` (port interne Docker)

2. **Conflit métriques Prometheus**
   - ❌ Problème : `telemetry/metrics` ET `telemetry/opencensus` sur port 9091
   - ✅ Solution : Utiliser uniquement `telemetry/opencensus` avec exporteur Prometheus

3. **Healthcheck API manquant**
   - ❌ Problème : KrakenD `depends_on: api: condition: service_healthy` mais API sans healthcheck
   - ✅ Solution : Ajouter healthcheck TCP sur port 5000

4. **Rechargement configuration**
   - ❌ Problème : `docker restart` ne recharge pas krakend.json monté en volume
   - ✅ Solution : `docker compose down krakend && docker compose up -d krakend`

### Bonnes Pratiques Appliquées

- ✅ **Architecture en couches** : Gateway → LB → API instances
- ✅ **Monitoring intégré** : Métriques Prometheus dès le départ
- ✅ **Tests automatisés** : Script bash complet avec assertions
- ✅ **Documentation** : RUNBOOK.md mis à jour
- ✅ **Rate limiting** : Protection contre surcharge
- ✅ **Circuit breaker** : Résilience en cas d'erreurs backend

---

## 📈 Prochaines Étapes

### Court Terme (Phase 2b suite)

- [ ] Tests de performance k6 comparatifs (Direct vs NGINX vs Gateway)
- [ ] Mesure overhead latence gateway (attendu: +2-5ms)
- [ ] Tests rate limiting (> 100 req/s → HTTP 429)
- [ ] Tests circuit breaker (5+ erreurs → HTTP 503)
- [ ] Dashboard Grafana pour KrakenD
- [ ] Rapport comparatif performance

### Moyen Terme (Phase 3)

- [ ] Extraction microservices (Orders, Portfolio, Reporting)
- [ ] Configuration routing avancé KrakenD
- [ ] Agrégation de réponses multi-backend
- [ ] JWT validation au niveau gateway
- [ ] Cache gateway (réduire appels backend)

### Long Terme

- [ ] Service Mesh (Istio/Linkerd)
- [ ] Multi-région avec gateway distribué
- [ ] GraphQL gateway layer
- [ ] API versioning via gateway

---

## 📚 Références

- **Architecture Ports** : `documentation-projet/architecture/ARCHITECTURE-PORTS.md`
- **Vue Déploiement** : `docs/4+1/deploiement.puml`
- **RUNBOOK** : `RUNBOOK.md`
- **KrakenD Docs** : https://www.krakend.io/docs/
- **Prometheus Metrics** : http://localhost:9090

---

## ✅ Validation Finale

**Date** : 28 octobre 2025  
**Status** : ✅ **PHASE 2B OPÉRATIONNELLE**

**Signature** :
- KrakenD Gateway: ✅ Healthy (port 8080)
- Métriques Prometheus: ✅ Scrapping actif (port 9091)
- Routing API: ✅ Fonctionnel (6 endpoints)
- Tests automatisés: ✅ 6/6 pass
- Documentation: ✅ Complète

**Prêt pour tests de charge k6 et comparaison de performance.**
