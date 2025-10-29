# 📘 Runbook Opérationnel - BrokerX

**Guide rapide pour démarrer, tester et monitorer la plateforme.**

---

## 🚀 Démarrage Rapide

```bash
# Démarrer tout
docker compose up -d

# Vérifier que ça fonctionne
curl http://localhost:5000/health
```

**✅ Services actifs :**
- **API** : http://localhost:5000
- **Grafana** : http://localhost:3000 (admin/admin)
- **Prometheus** : http://localhost:9090

⏱️ **Temps de démarrage** : ~10-15 secondes

---

## 📊 Monitoring

### Voir les logs

```bash
# Logs formatés (lisibles)
./tests/scripts/logs-readable.sh

# Récupérer code OTP pour tests
docker logs brokerx-api 2>&1 | grep "\[OTP\]" | tail -1 | grep -oP 'code=\K\d+'
```

### Dashboard Grafana (recommandé)

**Ouvrir** : http://localhost:3000 (admin/admin)

**Métriques visibles :**
- ⏱️ **Latence** P95 (objectif: < 500ms)
- 📈 **Trafic** (requêtes/seconde)  
- ❌ **Erreurs** (objectif: < 5%)
- 💾 **Cache** hit rate (> 80%)

### Métriques brutes

```bash
# Voir toutes les métriques
curl http://localhost:5000/metrics

# Métriques cache uniquement  
curl http://localhost:5000/metrics | grep cache_
```

---

## 🧪 Tests de Performance

### Test rapide (1 min)

```bash
# Test simple signup
k6 run scripts/k6/signup.js
```

### Tests Phase 2a : Load Balancing + Redis

```bash
# Test complet scaling (N=1,2,3,4 instances) - 30 min
./tests/scripts/test-scaling.sh comparison

# Test Redis cache - 2 min
./tests/scripts/test-redis-metrics.sh
```

**📊 Résultats attendus :**
- Load Balancing: 22-23 RPS constant (N=1 à 4)
- Redis Cache: Hit rate > 0%, latence < 5ms
- Tous les résultats dans `tests/resultats-k6/` et `tests/resultats-redis/`

---

## 🔧 Commandes Utiles

```bash
# Redémarrer l'API
docker compose restart api

# Voir les logs
docker compose logs -f api

# Accès MySQL
docker exec -it brokerx-mysql mysql -uroot -proot123 brokerx

# Accès Redis
docker exec -it brokerx-redis redis-cli

# Vérifier clés cache
docker exec brokerx-redis redis-cli keys "*"
```

---

## 🌐 Gateway (Phase 2b)

```bash
# Tester via gateway
curl -X POST http://localhost:8080/api/v1/signup \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","fullName":"Test","password":"SecureP@ss123","confirmPassword":"SecureP@ss123"}'

# Tests complets
./tests/scripts/test-krakend-gateway.sh
```

**Services :**
- **Gateway** : http://localhost:8080
- **Métriques** : http://localhost:9091/metrics

---

## 🚨 Problèmes Courants

**API ne démarre pas** :
```bash
docker compose logs api --tail 20
docker compose down && docker compose up -d
```

**Redis non connecté** :
```bash
# Vérifier Redis actif
docker ps | grep redis

# Arrêter autres Redis en conflit
docker stop $(docker ps -q --filter "name=redis" --filter "name!=brokerx")

# Rebuild API
docker compose build api && docker compose up -d api
```

**Grafana pas de données** :
```bash
# Vérifier Prometheus scrape les 5 instances
curl http://localhost:9090/api/v1/targets | grep "brokerx-api"
```

---

## 📚 Documentation Détaillée

- **Architecture Ports** : `documentation-projet/architecture/ARCHITECTURE-PORTS.md`
- **Tests Load Balancing** : `tests/resultats-k6/scaling-comparison/`
- **Tests Redis Cache** : `tests/resultats-redis/VALIDATION-REDIS-CACHE.md`
- **KrakenD Gateway** : `docs/PHASE2B-GATEWAY-SUMMARY.md`
- **Scripts** : `tests/scripts/`

---

## 🚨 Problèmes Courants

```bash
# Via KrakenD Gateway (port 8080)
BASE_URL=http://localhost:8080 k6 run --duration 1m --vus 10 scripts/k6/signup.js
```

### Comparaison automatique

```bash
# Compare les 3 modes (Direct, NGINX, Gateway)
./tests/scripts/test-krakend-gateway.sh
```

**Résultats attendus :**
- **Overhead gateway** : +2-5ms latence P95
- **Rate limiting** : 429 si > 100 req/s
- **Circuit breaker** : 503 après 5 erreurs consécutives

---

## 📊 Résumé Architecture Phase 2

| Composant | Port | Rôle | Status |
|-----------|------|------|--------|
| **KrakenD Gateway** | 8080 | Point d'entrée public | ✅ Phase 2b |
| **NGINX Load Balancer** | 8090 | Distribution de charge | ✅ Phase 2a |
| **API Instance 1-4** | 5001-5004 (debug) | Backend API | ✅ Phase 2a |
| **Redis Cache** | 6379 (interne) | Cache distribué | ✅ Phase 2a |
| **MySQL** | 3307 | Base de données | ✅ |
| **Prometheus** | 9090 | Métriques | ✅ |
| **Grafana** | 3000 | Dashboards | ✅ |

**Flux de requête :**
```
Client 
  → KrakenD:8080 (rate limit, circuit breaker)
    → NGINX:8090 (load balancing)
      → [API-1:8080, API-2:8080, API-3:8080, API-4:8080]
        → Redis:6379 (cache)
        → MySQL:3307 (persistence)
```

