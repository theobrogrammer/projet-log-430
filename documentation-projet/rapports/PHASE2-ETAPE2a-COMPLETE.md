# ✅ Phase 2 Étape 2a - Observabilité + Tests de Charge

## 📊 Statut: COMPLÉTÉ

**Date**: 28 octobre 2025  
**Équipe**: LOG-430  
**Durée d'implémentation**: ~6 heures

---

## 🎯 Objectifs de l'étape

- [x] **Prometheus + Grafana**: Métriques et dashboards
- [x] **Serilog**: Logs structurés JSON
- [x] **k6**: Tests de charge et baseline
- [ ] **NGINX Load Balancer**: (reporté - dépend des résultats k6)
- [ ] **Redis Cache**: (reporté - dépend des résultats k6)

---

## ✅ Livrables

### 1. Prometheus + Grafana (Golden Signals)

**Fichiers**:
- `docker-compose.yml`: Services prometheus + grafana configurés
- `prometheus/prometheus.yml`: Config scraping API
- `docs/grafana-dashboard-golden-signals.json`: Dashboard 4 signaux

**Métriques exposées**:
- `http_requests_received_total`: Compteur requêtes HTTP
- `http_request_duration_seconds`: Histogramme latence
- `dotnet_*`: Métriques .NET (GC, threads, memory)

**Dashboard Grafana**:
- **Latency**: P50/P95/P99 en temps réel
- **Traffic**: RPS (requests per second)
- **Errors**: Taux d'erreurs 4xx/5xx
- **Saturation**: CPU et Memory usage

**Accès**: http://localhost:3000 (admin/admin)

---

### 2. Logs Structurés (Serilog)

**Packages installés**:
- Serilog.AspNetCore 9.0.0
- Serilog.Formatting.Compact 3.0.0
- Serilog.Enrichers.Thread/Environment

**Configuration** (`Program.cs`):
- Format: CompactJsonFormatter (une ligne par événement)
- Sinks: Console + File (rotation quotidienne, 7 jours)
- Enrichers: ThreadId, MachineName, TraceId, Application
- Niveaux: Microsoft/System → Warning, Application/Domain → Information

**Événements instrumentés**:

```csharp
// SignupService.cs
Log.Information("UC01_SIGNUP_START - Début inscription pour {Email}", email);
Log.Information("UC01_SIGNUP_SUCCESS - Inscription réussie: {ClientId} {AccountId}", ...);

// AuthService.cs
Log.Information("UC02_LOGIN_START - Tentative de connexion pour {Email} depuis {IP}", ...);
Log.Information("UC02_LOGIN_SUCCESS - Connexion réussie: {ClientId} {Email} {SessionId}", ...);
```

**Fichiers logs**:
- `/app/logs/app-YYYYMMDD.jsonl`: Logs applicatifs
- `/app/logs/audit.jsonl`: Logs d'audit séparés

**Script de visualisation**:
```bash
./logs-readable.sh
# Sortie:
# 00:58:14 | UC01_SIGNUP_START | test@example.com
# 00:58:15 | UC01_SIGNUP_SUCCESS | test@example.com | 02dd16f7
```

**Documentation**: `docs/SERILOG-EXPLICATIONS.md` (500 lignes)

---

### 3. Tests de Charge k6

**Installation**: k6 v1.3.0 via APT repository officiel

**Scripts créés** (`scripts/k6/`):

1. **signup.js**: Constant VUs (10 users, 1min)
2. **login.js**: Ramping VUs (0→50→0, 3min)
3. **deposit.js**: Idempotency test (20 users, 2min)
4. **mixed.js**: Scénario réaliste (60% read / 40% write, 5min)

**Thresholds configurés**:
```javascript
thresholds: {
  'http_req_duration': ['p(95)<500'],      // P95 < 500ms
  'http_req_failed': ['rate<0.05'],        // Erreurs < 5%
  'read_success_rate': ['rate>0.98'],      // Succès reads > 98%
  'write_success_rate': ['rate>0.92'],     // Succès writes > 92%
}
```

**Exécution**:
```bash
# Baseline complet
./run-k6-test.sh baseline scripts/k6/mixed.js

# Tests individuels
k6 run scripts/k6/signup.js
k6 run scripts/k6/login.js
```

---

## 📈 Résultats Baseline (N=1 instance)

### Synthèse

| Métrique | Mesuré | Objectif | Status |
|----------|--------|----------|--------|
| **Total Requests** | 6,992 | - | ✅ |
| **RPS (avg)** | 22.95 | > 30 | ❌ |
| **Latency P95** | 637ms | < 500ms | ❌ |
| **Latency Max** | 5.78s | < 2s | ❌ |
| **Error Rate** | 12.55% | < 5% | ❌ |
| **Read Success** | 49.36% | > 98% | ❌ |
| **Write Success** | 69.37% | > 92% | ❌ |

**Verdict**: ⚠️ Système nécessite optimisations avant scaling

### Breakdown Performance

**Read Operations** (58.9%):
- Success rate: 49.36% (échecs: health check format JSON)
- P95 latency: **8.29ms** ✅ Excellent
- Types: Health checks, Metrics endpoint

**Write Operations** (41.1%):
- Success rate: 69.37% (échecs: UC-03 non implémenté)
- P95 latency: **3.51s** ❌ Très élevé
- Types: Signup, Login, Deposit

### Problèmes Identifiés

1. **UC-03 Deposit non implémenté**:
   - 878 échecs / 878 tentatives (100%)
   - Impact: +12% error rate global
   - Action: Implémenter WalletService avec idempotency-key

2. **Latence Write excessive** (P95 = 3.51s):
   - Causes: Pas d'index DB, OTP synchrone, pas de connection pool
   - Action: Optimiser DB queries + async OTP

3. **Health Check format**:
   - 2,086 échecs de validation JSON
   - Action: Standardiser response `{"status":"Healthy"}`

---

## 📁 Documentation Créée

### Fichiers principaux

1. **`docs/SERILOG-EXPLICATIONS.md`** (500 lignes)
   - Pourquoi Serilog vs ILogger
   - Architecture et flux complet
   - Exemples d'usage avec code
   - 5 cas d'usage concrets avec jq
   - FAQ et intégration Phase 2

2. **`docs/RUNBOOK-OPS.md`** (150 lignes - version concise)
   - Démarrage rapide (< 30 min)
   - Monitoring Prometheus/Grafana
   - Logs structurés et debugging
   - Troubleshooting courant
   - Commandes essentielles

3. **`scripts/k6/README.md`**
   - Description des 4 scripts
   - Thresholds et objectifs
   - Analyse des résultats avec jq
   - Workflow complet de test

4. **`resultats-k6/baseline/ANALYSE.md`**
   - Métriques détaillées baseline
   - Analyse des 3 problèmes majeurs
   - Actions prioritaires avec code
   - Timeline et prochains tests

5. **`resultats-k6/baseline/RAPPORT-VISUEL.md`**
   - Graphiques ASCII art
   - Tableaux comparatifs
   - Distribution latence
   - Recommandations Phase 1/2/3

6. **`TODO-K6-FIXES.md`**
   - 5 actions prioritaires
   - Code complet pour fixes
   - Tests de validation
   - Timeline estimée (30h)

---

## 🛠️ Scripts Utilitaires

### 1. `logs-readable.sh`

Affiche les logs JSON en format lisible temps réel:
```bash
./logs-readable.sh
# 00:58:14 | UC01_SIGNUP_START | test@example.com
# 00:58:15 | UC01_SIGNUP_SUCCESS | test@example.com | 02dd16f7
```

### 2. `run-k6-test.sh`

Automatise l'exécution des tests k6:
```bash
./run-k6-test.sh <test-name> <script-path>
# Exemples:
./run-k6-test.sh baseline scripts/k6/mixed.js
./run-k6-test.sh after-uc03 scripts/k6/mixed.js
```

Fonctionnalités:
- Vérifie API + Grafana
- Nettoie les logs
- Lance monitoring en arrière-plan
- Exécute k6 avec JSON output
- Génère résumé automatique
- Donne instructions pour screenshots

---

## 🔍 Commandes Utiles

### Monitoring temps réel

```bash
# Logs formatés
./logs-readable.sh

# Stats Docker
docker stats brokerx-api

# Prometheus targets
curl http://localhost:9090/api/v1/targets | jq '.data.activeTargets[].health'

# Grafana (browser)
firefox http://localhost:3000
```

### Analyse logs

```bash
# Derniers logs JSON
docker compose exec api cat /app/logs/app-$(date +%Y%m%d).jsonl | tail -10 | jq .

# Filtrer par Use Case
docker compose logs api | grep "UC01"

# Tracer une requête (par TraceId)
TRACE_ID="2966dd20d67e6562e818127825978682"
docker compose exec api cat /app/logs/app-$(date +%Y%m%d).jsonl | \
  jq -r --arg tid "$TRACE_ID" 'select(.["@tr"] == $tid)'

# Récupérer code OTP
docker logs brokerx-api 2>&1 | grep "OTP" | tail -1 | grep -oP 'code=\K\d+'
```

### Tests k6

```bash
# Test baseline complet
./run-k6-test.sh baseline scripts/k6/mixed.js

# Analyser résultats
cat resultats-k6/baseline/baseline.json | jq '.metrics.http_req_duration'

# Comparer deux tests
diff <(cat resultats-k6/baseline/baseline.json | jq '.metrics') \
     <(cat resultats-k6/after-uc03/after-uc03.json | jq '.metrics')
```

---

## 📸 Screenshots Grafana

### À capturer pour le rapport

1. **Dashboard 4 Golden Signals** pendant le test:
   - Time range: 00:56:00 → 01:01:00 (durée du test)
   - Panels:
     * Latency (P95) - montrer pic à 637ms
     * Traffic (RPS) - plateau à ~23 req/s
     * Errors - spike à 12.55%
     * Saturation - CPU/Memory usage

2. **Prometheus Targets**:
   - http://localhost:9090/targets
   - Montrer brokerx-api UP (1/1)

3. **Exemple de log structuré**:
   ```json
   {
     "@t": "2025-10-28T00:58:14.7597315Z",
     "@mt": "UC01_SIGNUP_START - Début inscription pour {Email}",
     "@tr": "2966dd20d67e6562e818127825978682",
     "Email": "test@example.com",
     "ThreadId": 25,
     "Application": "BrokerX"
   }
   ```

**Emplacement**: `resultats-k6/baseline/screenshots/`

---

## 🎯 Prochaines Étapes

### Immediate (Avant Load Balancing)

1. **Implémenter UC-03 Deposit** (Priorité 1)
   - Entity: TransactionPaiement + IdempotencyKey UNIQUE
   - Service: WalletService.DepositAsync()
   - Controller: POST /api/v1/wallet/deposit
   - Tests: Vérifier idempotence (status 409 sur retry)

2. **Fixer Health Check** (15 min)
   - Response JSON: `{"status":"Healthy"}`

3. **Optimiser DB** (2-3h)
   - Index sur Client.Email, Compte.ClientId
   - Connection pool MySQL (min=5, max=20)
   - AsNoTracking() sur reads EF Core

### Phase suivante

4. **NGINX Load Balancer** (N=2,3,4)
   - Config upstream avec 4 instances
   - Tests comparatifs RPS et latence

5. **Redis Cache**
   - Cache client lookups
   - Mesurer cache hit rate

---

## 📚 Références

### Documentation

- **Architecture**: `docs/arc42/arc42.md`
- **ADRs**: `docs/adr/` (ADR-001, 002, 003)
- **Troubleshooting**: `docs/troubleshooting-guide.md`
- **Serilog**: `docs/SERILOG-EXPLICATIONS.md`
- **Runbook**: `docs/RUNBOOK-OPS.md`

### Outils

- **Prometheus**: http://localhost:9090
- **Grafana**: http://localhost:3000 (admin/admin)
- **API Health**: http://localhost:5000/health
- **API Metrics**: http://localhost:5000/metrics

### Commandes clés

```bash
# Démarrage
docker compose up -d

# Monitoring
./logs-readable.sh
firefox http://localhost:3000

# Tests
./run-k6-test.sh baseline scripts/k6/mixed.js

# Analyse
cat resultats-k6/baseline/baseline.json | jq '.metrics'
```

---

## 📊 Métriques de Réussite

### Critères d'acceptation Phase 2 Étape 2a

- [x] Prometheus opérationnel et scraping API
- [x] Grafana dashboard 4 Golden Signals fonctionnel
- [x] Serilog logs JSON structurés avec rotation
- [x] k6 scripts créés et baseline exécuté
- [x] Documentation complète (runbook + explications)
- [ ] Load balancing NGINX (reporté après fixes)
- [ ] Redis cache (reporté après fixes)

### Qualité de l'implémentation

- ✅ **Code Quality**: Separation of concerns, clean architecture
- ✅ **Observability**: Logs, metrics, traces (TraceId correlation)
- ✅ **Documentation**: Runbook, README, ADRs
- ✅ **Testing**: 4 scénarios k6 avec thresholds
- ⚠️ **Performance**: Baseline révèle optimisations nécessaires

---

## 🏆 Achievements

1. **Observability Complete**: Metrics + Logs + Dashboards ✅
2. **Load Testing Framework**: k6 scripts prêts pour CI/CD ✅
3. **Baseline Established**: Références pour comparaisons futures ✅
4. **Documentation Exhaustive**: Runbook + 6 docs détaillés ✅
5. **Issues Identified**: 5 actions prioritaires avec solutions ✅

---

**Statut final**: Phase 2 Étape 2a ✅ COMPLÉTÉ  
**Prochaine étape**: Fixes bloquants → Load Balancing + Cache  
**Date**: 28 octobre 2025  
**Temps total**: ~6 heures d'implémentation + tests
