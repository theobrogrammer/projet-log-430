# 📊 Tests de charge k6 - Résultats

Ce dossier contient les résultats des tests de charge k6 pour l'API BrokerX.

## 📁 Structure

```
resultats-k6/
├── baseline/                      # Résultats baseline (N=1 instance)
│   ├── ANALYSE.md                # Analyse détaillée des résultats
│   ├── baseline.json             # Données brutes k6
│   ├── baseline-summary.json     # Résumé k6
│   ├── k6-console-output.txt     # Output console complet
│   ├── api-logs-*.log            # Logs API pendant le test
│   └── screenshots/              # Screenshots Grafana
│       ├── 01-latency.png
│       ├── 02-traffic.png
│       ├── 03-errors.png
│       └── 04-saturation.png
├── after-uc03/                    # Après implémentation UC-03
├── after-db-opt/                  # Après optimisations DB
└── load-balancing-n4/             # Avec 4 instances (NGINX)
```

## 🚀 Exécution rapide

### Test baseline complet

```bash
./run-k6-test.sh baseline scripts/k6/mixed.js
```

Ce script:
1. ✅ Vérifie que l'API est UP
2. ✅ Vérifie Grafana (optionnel)
3. ✅ Nettoie les logs
4. ✅ Démarre le monitoring (logs-readable.sh)
5. ✅ Exécute k6
6. ✅ Sauvegarde tous les résultats
7. ✅ Affiche les instructions pour screenshots

### Tests individuels

```bash
# UC-01 Inscription uniquement
k6 run --out json=resultats-k6/signup-test.json scripts/k6/signup.js

# UC-02 Authentification uniquement
k6 run --out json=resultats-k6/login-test.json scripts/k6/login.js

# UC-03 Dépôt (idempotency)
k6 run --out json=resultats-k6/deposit-test.json scripts/k6/deposit.js
```

## 📊 Résultats Baseline (N=1)

### Résumé exécutif

| Métrique | Mesuré | Objectif | Status |
|----------|--------|----------|--------|
| **P95 Latency** | 637ms | < 500ms | ❌ |
| **P95 Writes** | 3.51s | < 800ms | ❌ |
| **P95 Reads** | 8.29ms | < 200ms | ✅ |
| **Error Rate** | 12.55% | < 5% | ❌ |
| **RPS** | 22.95 | > 30 | ⚠️ |

**Verdict**: Nécessite optimisations (voir ANALYSE.md)

### Problèmes identifiés

1. **UC-03 non implémenté**: 878 erreurs (100% des deposits)
2. **Latence writes élevée**: P95 = 3.51s (objectif: 800ms)
3. **Health check format**: 2,086 échecs de validation

### Actions prioritaires

- [ ] Implémenter UC-03 avec idempotency-key
- [ ] Optimiser queries DB (index, connection pool)
- [ ] Fixer format health check JSON

## 📈 Analyse des métriques

### Visualisation avec jq

```bash
# Métriques HTTP
cat resultats-k6/baseline/baseline.json | jq '
  select(.type=="Point" and .metric=="http_req_duration") | 
  .data.tags, .data.value
' | tail -20

# Taux d'erreurs
cat resultats-k6/baseline/baseline.json | jq '
  select(.metric=="http_req_failed") | 
  {time: .data.time, value: .data.value}
' | tail -10

# Custom metrics
cat resultats-k6/baseline/baseline.json | jq '
  select(.metric | startswith("read_") or startswith("write_"))
' | tail -20
```

### Métriques clés extraites

```json
{
  "http_req_duration": {
    "avg": 223.3,
    "med": 3.99,
    "p90": 569.48,
    "p95": 637.28,
    "max": 5780
  },
  "http_reqs": {
    "count": 6992,
    "rate": 22.95
  },
  "http_req_failed": {
    "rate": 0.1255
  }
}
```

## 📸 Screenshots Grafana

### Procédure de capture

1. **Ouvrir Grafana**: http://localhost:3000
2. **Sélectionner dashboard**: "4 Golden Signals"
3. **Ajuster time range**: Selon la durée du test
4. **Capturer les 4 panels**:
   - Latency (P95/P99 graph)
   - Traffic (RPS graph)
   - Errors (Error rate %)
   - Saturation (CPU/Memory)

5. **Sauvegarder**:
   ```bash
   # Dans resultats-k6/<test-name>/screenshots/
   01-latency.png
   02-traffic.png
   03-errors.png
   04-saturation.png
   ```

### Exemples de requêtes Grafana

```promql
# Latency P95
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[1m]))

# RPS
rate(http_requests_received_total[1m])

# Error rate
rate(http_requests_received_total{code=~"5.."}[1m]) / 
  rate(http_requests_received_total[1m])

# CPU
rate(process_cpu_seconds_total[1m]) * 100
```

## 🔄 Workflow de test complet

### 1. Baseline (état actuel)

```bash
./run-k6-test.sh baseline scripts/k6/mixed.js
# Sauvegarder screenshots dans resultats-k6/baseline/screenshots/
```

### 2. Après optimisations

```bash
# Implémenter UC-03
# Ajouter index DB
# Optimiser connection pool

./run-k6-test.sh after-uc03 scripts/k6/mixed.js
# Comparer avec baseline
```

### 3. Load Balancing (N=4)

```bash
# Configurer NGINX avec 4 instances
docker compose up -d --scale api=4

./run-k6-test.sh load-balancing-n4 scripts/k6/mixed.js
# Observer amélioration RPS et latency
```

### 4. Avec Redis Cache

```bash
# Ajouter Redis
# Implémenter cache pour reads

./run-k6-test.sh with-redis scripts/k6/mixed.js
# Mesurer cache hit rate
```

## 📊 Comparaison des résultats

### Script de comparaison

```bash
#!/bin/bash
# compare-tests.sh

TEST1=${1:-"baseline"}
TEST2=${2:-"after-uc03"}

echo "Comparaison: $TEST1 vs $TEST2"
echo "=============================="

jq -n --slurpfile a resultats-k6/$TEST1/$TEST1.json \
      --slurpfile b resultats-k6/$TEST2/$TEST2.json '
{
  test1: {
    p95: ($a | map(select(.metric=="http_req_duration" and .type=="Point")) | last | .data.value),
    errors: ($a | map(select(.metric=="http_req_failed" and .type=="Point")) | last | .data.value)
  },
  test2: {
    p95: ($b | map(select(.metric=="http_req_duration" and .type=="Point")) | last | .data.value),
    errors: ($b | map(select(.metric=="http_req_failed" and .type=="Point")) | last | .data.value)
  }
}
'
```

## 🎯 Seuils de réussite (thresholds)

### Définis dans les scripts k6

```javascript
thresholds: {
  'http_req_duration': ['p(95)<500'],       // P95 < 500ms
  'http_req_failed': ['rate<0.05'],         // Error rate < 5%
  'read_success_rate': ['rate>0.98'],       // Read success > 98%
  'write_success_rate': ['rate>0.92'],      // Write success > 92%
}
```

### Interprétation des exit codes

- **0**: Tous les thresholds passés ✅
- **99**: Certains thresholds échoués ⚠️
- **Autre**: Erreur d'exécution ❌

## 📚 Documentation complète

- **Scripts k6**: `scripts/k6/README.md`
- **Analyse baseline**: `resultats-k6/baseline/ANALYSE.md`
- **Runbook ops**: `docs/RUNBOOK-OPS.md`
- **Architecture**: `docs/arc42/arc42.md`

---

**Dernière mise à jour**: 28 octobre 2025  
**Framework**: k6 v1.3.0  
**Auteur**: Équipe LOG-430
