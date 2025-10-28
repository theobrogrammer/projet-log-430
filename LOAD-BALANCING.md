# 📋 Configuration Load Balancing - NGINX

## Fichiers de configuration

### nginx.conf
Configuration NGINX avec upstream backend (4 serveurs API).

**Algorithme:** `least_conn` (connexions actives les plus faibles)  
**Health checks:** `max_fails=3`, `fail_timeout=30s`  
**Keepalive:** 32 connexions persistantes

### docker-compose.lb.yml
Configuration Docker Compose pour déployer:
- 4 instances API (brokerx-api-1 à 4) sur ports 5001-5004
- 1 instance NGINX (port 8090)

**Note:** Port 80 utilisé par Apache, port 8080 réservé pour KrakenD (Phase 2b)

**Usage:**
```bash
# Démarrer avec load balancing
docker compose -f docker-compose.yml -f docker-compose.lb.yml up -d

# Arrêter
docker compose -f docker-compose.yml -f docker-compose.lb.yml down
```

## Scripts de test

### tests/scripts/test-lb-quick.sh
Test rapide du load balancer (< 5 min).

**Actions:**
1. Démarre 4 API + NGINX
2. Vérifie santé NGINX
3. Teste distribution sur 20 requêtes
4. (Optionnel) Test k6 30s

**Usage:**
```bash
./tests/scripts/test-lb-quick.sh
```

### tests/scripts/test-scaling.sh
Tests comparatifs complets N=1,2,3,4 instances (~30 min).

**Modes:**
- `comparison`: Tests avec 1, 2, 3, 4 instances
- `fault-tolerance`: Simulation panne en production
- `baseline`: Test N=1 uniquement

**Usage:**
```bash
# Tests comparatifs complets
./tests/scripts/test-scaling.sh comparison

# Test de tolérance aux pannes
./tests/scripts/test-scaling.sh fault-tolerance
```

## Test manuel du load balancing

```bash
# 1. Démarrer
docker compose -f docker-compose.yml -f docker-compose.lb.yml up -d

# 2. Attendre 15s
sleep 15

# 3. Tester distribution (doit alterner entre instances)
for i in {1..10}; do 
  curl -s http://localhost:8090/health
  echo
done

# 4. Tester failover
docker stop brokerx-api-2
for i in {1..5}; do curl -s http://localhost:8090/health; echo; done
docker start brokerx-api-2

# 5. Test charge
k6 run --duration 1m --vus 30 scripts/k6/mixed.js
```

## Résultats attendus

### Sans load balancing (N=1)
- RPS: ~23 req/s
- P95 Latency: 637ms
- Error Rate: 12.55%

### Avec load balancing (N=4)
- RPS: ~85-90 req/s (x4)
- P95 Latency: <200ms (↓70%)
- Error Rate: <1% (↓90%)

## Métriques à surveiller

**Prometheus queries:**
```promql
# Distribution des requêtes par instance
sum by (instance) (rate(http_requests_received_total[5m]))

# Latence P95 par instance
histogram_quantile(0.95, sum by (le, instance) (rate(http_request_duration_seconds_bucket[5m])))

# Taux d'erreur par instance
sum by (instance) (rate(http_requests_received_total{code=~"5.."}[5m]))
```

## Troubleshooting

### NGINX ne démarre pas
```bash
# Vérifier syntax nginx.conf
docker run --rm -v $(pwd)/nginx.conf:/etc/nginx/nginx.conf:ro nginx nginx -t

# Vérifier logs
docker compose logs nginx
```

### API non accessible via NGINX
```bash
# Vérifier réseau
docker network inspect projet-log-430_brokerx-network

# Vérifier upstream NGINX
docker compose exec nginx cat /etc/nginx/nginx.conf | grep upstream -A 10

# Tester API directement
curl http://localhost:5001/health
```

### Distribution inégale
```bash
# Vérifier algorithme (doit être least_conn)
docker compose exec nginx cat /etc/nginx/nginx.conf | grep least_conn

# Vérifier keepalive
docker compose exec nginx cat /etc/nginx/nginx.conf | grep keepalive
```

## Documentation

Voir aussi:
- `RUNBOOK.md` - Guide opérationnel complet
- `documentation-projet/tests-charge/` - Résultats tests k6
- `docs/4+1/README-execution.md` - Plan d'exécution Phase 2
