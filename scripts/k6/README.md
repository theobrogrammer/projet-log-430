# Scripts de test k6 - BrokerX API

Ce dossier contient les scripts de test de charge pour l'API BrokerX.

## 📋 Scripts disponibles

### 1. `signup.js` - UC-01 Inscription
- **Scénario**: Constant VUs
- **Charge**: 10 utilisateurs simultanés pendant 1 minute
- **Objectif**: Tester la capacité d'inscription sous charge constante
- **Usage**: `k6 run scripts/k6/signup.js`

### 2. `login.js` - UC-02 Authentification
- **Scénario**: Ramping VUs
- **Charge**: 0 → 20 → 50 → 0 sur 3 minutes
- **Objectif**: Tester la scalabilité du système d'authentification
- **Usage**: `k6 run scripts/k6/login.js`

### 3. `deposit.js` - UC-03 Dépôt (Idempotency)
- **Scénario**: Test d'idempotence
- **Charge**: 20 utilisateurs pendant 2 minutes
- **Objectif**: Valider que l'idempotency-key prévient les duplicatas
- **Usage**: `k6 run scripts/k6/deposit.js`

### 4. `mixed.js` - Scénario mixte réaliste
- **Scénario**: Trafic réaliste (60% read / 40% write)
- **Charge**: 0 → 10 → 30 → 50 → 30 → 0 sur 5 minutes
- **Objectif**: Baseline de performance production
- **Usage**: `k6 run --out json=resultats-k6/baseline.json scripts/k6/mixed.js`

## 🎯 Thresholds

Tous les scripts incluent des seuils de performance:

- **P95 < 500ms**: 95% des requêtes doivent répondre en moins de 500ms
- **Error Rate < 5%**: Moins de 5% d'erreurs HTTP
- **Success Rate > 90-95%**: Taux de succès selon le use case

## 🚀 Exécution rapide

```bash
# Test simple (signup)
k6 run scripts/k6/signup.js

# Test avec résultats JSON
k6 run --out json=resultats-k6/signup-results.json scripts/k6/signup.js

# Test baseline complet
k6 run --out json=resultats-k6/baseline.json scripts/k6/mixed.js

# Observer les logs pendant le test
./logs-readable.sh
```

## 📊 Analyser les résultats

Les résultats sont sauvegardés dans `resultats-k6/`:

```bash
# Voir le résumé
cat resultats-k6/baseline.json | jq '.metrics.http_req_duration'

# Extraire les métriques clés
cat resultats-k6/baseline.json | jq '{
  total_requests: .metrics.http_reqs.values.count,
  p95_duration: .metrics.http_req_duration.values["p(95)"],
  error_rate: .metrics.http_req_failed.values.rate
}'
```

## 🔍 Monitoring pendant les tests

1. **Grafana Dashboard**: http://localhost:3000
   - Observer les 4 Golden Signals en temps réel
   - Latency, Traffic, Errors, Saturation

2. **Prometheus**: http://localhost:9090
   - Requêtes ad-hoc: `rate(http_requests_received_total[1m])`

3. **Logs structurés**:
   ```bash
   ./logs-readable.sh
   ```

## 📈 Résultats attendus (baseline N=1)

| Métrique | Valeur cible | Seuil critique |
|----------|--------------|----------------|
| P95 Latency | < 300ms | < 500ms |
| P99 Latency | < 800ms | < 1000ms |
| RPS (Requests/sec) | > 50 | > 30 |
| Error Rate | < 1% | < 5% |
| CPU Usage | < 50% | < 80% |
| Memory Usage | < 512MB | < 1GB |

## 🔄 Prochaines étapes

Après avoir établi le baseline:

1. **Load Balancing (N=2,3,4)**:
   - Configurer NGINX
   - Tester avec scaling horizontal
   - Comparer les résultats

2. **Caching (Redis)**:
   - Ajouter Redis pour les lectures
   - Mesurer l'amélioration de latence
   - Analyser le cache hit rate

3. **Optimisations**:
   - Identifier les bottlenecks
   - Optimiser les requêtes DB
   - Ajuster les ressources
