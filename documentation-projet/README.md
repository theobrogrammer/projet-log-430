# 📚 Documentation Projet LOG430 - BrokerX

## Vue d'ensemble

Cette documentation regroupe tous les guides, analyses, et rapports du projet BrokerX organisés par catégorie.

```
documentation-projet/
├── architecture/      # Patterns, ADR, guides d'implémentation
├── observabilite/     # Monitoring, logs, métriques
├── operations/        # Runbooks, guides de démarrage
├── rapports/          # Analyses de performance, résultats
└── tests-charge/      # Tests k6, baseline, actions
```

---

## 🏛️ Architecture (6 documents)

Patterns, décisions architecturales, et guides d'implémentation.

| Document | Description |
|----------|-------------|
| [`ADR-001-Architecture-hexagonale.md`](architecture/ADR-001-Architecture-hexagonale.md) | Architecture Decision Record - Pattern hexagonal |
| [`ARCHITECTURE-PORTS.md`](architecture/ARCHITECTURE-PORTS.md) | Documentation des ports & adapters |
| [`domain-architecture-analysis.md`](architecture/domain-architecture-analysis.md) | Analyse de l'architecture domaine |
| [`domain-glossary.md`](architecture/domain-glossary.md) | Glossaire métier (Ubiquitous Language) |
| [`GUIDE-DEPENDENCY-INJECTION.md`](architecture/GUIDE-DEPENDENCY-INJECTION.md) | Guide d'injection de dépendances |
| [`GUIDE-SIGNUP-USECASE.md`](architecture/GUIDE-SIGNUP-USECASE.md) | Guide détaillé UC-01 (Inscription) |

---

## 📊 Observabilité (3 documents)

Monitoring, logging structuré, et métriques.

| Document | Description |
|----------|-------------|
| [`SERILOG-EXPLICATIONS.md`](observabilite/SERILOG-EXPLICATIONS.md) | Guide complet Serilog (config, enrichers, rotation) |
| [`GRAFANA-ACCESS-GUIDE.md`](observabilite/GRAFANA-ACCESS-GUIDE.md) | Accès et utilisation de Grafana |
| [`VERIFICATION-PROMETHEUS.md`](observabilite/VERIFICATION-PROMETHEUS.md) | Vérification des métriques Prometheus |

**Liens rapides:**
- Prometheus: http://localhost:9090
- Grafana: http://localhost:3000 (admin/admin)

---

## ⚙️ Opérations (3 documents)

Guides opérationnels, démarrage rapide, dépannage.

| Document | Description |
|----------|-------------|
| [`QUICKSTART.md`](operations/QUICKSTART.md) | ⚡ Démarrage en < 2 minutes |
| [`RUNBOOK-OPS.md`](operations/RUNBOOK-OPS.md) | Guide opérationnel complet (monitoring, logs, tests) |
| [`troubleshooting-guide.md`](operations/troubleshooting-guide.md) | Guide de dépannage |

**Commandes essentielles:**
```bash
# Démarrer le système
docker compose up -d

# Voir les logs formatés
./logs-readable.sh

# Tester l'API
curl http://localhost:5000/health
```

---

## 🧪 Tests de Charge (4 documents)

Tests k6, baseline, analyses, et actions prioritaires.

| Document | Description |
|----------|-------------|
| [`baseline-k6.md`](tests-charge/baseline-k6.md) | Résultats baseline (N=1 instance) |
| [`ANALYSE-k6.md`](tests-charge/ANALYSE-k6.md) | Analyse détaillée des résultats (6,992 requêtes) |
| [`RAPPORT-VISUEL-k6.md`](tests-charge/RAPPORT-VISUEL-k6.md) | Graphiques ASCII et visualisations |
| [`TODO-K6-FIXES.md`](tests-charge/TODO-K6-FIXES.md) | 🔴 Actions prioritaires (UC-03, DB, health check) |

**Résumé baseline:**
```
RPS:           22.95 req/s  ❌ (objectif: >30)
Latency P95:   637ms        ❌ (objectif: <500ms)
Error Rate:    12.55%       ❌ (objectif: <5%)
```

**Exécuter tests:**
```bash
# Test complet (5 min)
./run-k6-test.sh baseline scripts/k6/mixed.js

# Tests individuels
k6 run scripts/k6/signup.js   # UC-01 (1 min)
k6 run scripts/k6/login.js    # UC-02 (3 min)
k6 run scripts/k6/mixed.js    # Mixte (5 min)
```

---

## 📈 Rapports (1 document)

Rapports de phase et analyses complètes.

| Document | Description |
|----------|-------------|
| [`PHASE2-ETAPE2a-COMPLETE.md`](rapports/PHASE2-ETAPE2a-COMPLETE.md) | ✅ Résumé complet Phase 2 Étape 2a (Observabilité) |

---

## 🗂️ Autres Documents du Projet

### Documentation Architecture (dossier `docs/`)
- `docs/4+1/` - Vues 4+1 de Kruchten (PlantUML)
- `docs/adr/` - Architecture Decision Records
- `docs/arc42/` - Documentation Arc42
- `docs/views/` - Vues métier et use cases

### Scripts
- `scripts/k6/` - Scripts de tests de charge (signup.js, login.js, deposit.js, mixed.js)
- `logs-readable.sh` - Visualisation logs formatés
- `run-k6-test.sh` - Automatisation tests k6

### Résultats
- `resultats-k6/` - Résultats JSON des tests k6

---

## 🔍 Navigation Rapide par Besoin

| Besoin | Document |
|--------|----------|
| 🚀 **Démarrer le système** | [`operations/QUICKSTART.md`](operations/QUICKSTART.md) |
| 📊 **Voir les logs** | [`operations/RUNBOOK-OPS.md`](operations/RUNBOOK-OPS.md) |
| 🔧 **Problème technique** | [`operations/troubleshooting-guide.md`](operations/troubleshooting-guide.md) |
| 🧪 **Lancer tests k6** | [`tests-charge/baseline-k6.md`](tests-charge/baseline-k6.md) |
| 📈 **Analyser perfs** | [`tests-charge/ANALYSE-k6.md`](tests-charge/ANALYSE-k6.md) |
| 🔴 **Actions prioritaires** | [`tests-charge/TODO-K6-FIXES.md`](tests-charge/TODO-K6-FIXES.md) |
| 🏛️ **Comprendre archi** | [`architecture/ADR-001-Architecture-hexagonale.md`](architecture/ADR-001-Architecture-hexagonale.md) |
| 📝 **Glossaire métier** | [`architecture/domain-glossary.md`](architecture/domain-glossary.md) |
| 📡 **Configurer Serilog** | [`observabilite/SERILOG-EXPLICATIONS.md`](observabilite/SERILOG-EXPLICATIONS.md) |
| 📊 **Configurer Grafana** | [`observabilite/GRAFANA-ACCESS-GUIDE.md`](observabilite/GRAFANA-ACCESS-GUIDE.md) |

---

## 📊 Statistiques

```
Total documents:        17 fichiers Markdown
Total lignes:           ~3,500+ lignes

Par catégorie:
├── Architecture:       6 documents (~1,000 lignes)
├── Observabilité:      3 documents (~500 lignes)
├── Opérations:         3 documents (~400 lignes)
├── Tests de charge:    4 documents (~1,200 lignes)
└── Rapports:           1 document (~400 lignes)
```

---

## 🎯 Parcours Recommandés

### Pour débuter
1. [`operations/QUICKSTART.md`](operations/QUICKSTART.md) - Démarrage rapide
2. [`architecture/domain-glossary.md`](architecture/domain-glossary.md) - Vocabulaire métier
3. [`operations/RUNBOOK-OPS.md`](operations/RUNBOOK-OPS.md) - Guide opérationnel

### Pour développer
1. [`architecture/ADR-001-Architecture-hexagonale.md`](architecture/ADR-001-Architecture-hexagonale.md) - Pattern architectural
2. [`architecture/GUIDE-DEPENDENCY-INJECTION.md`](architecture/GUIDE-DEPENDENCY-INJECTION.md) - DI
3. [`architecture/GUIDE-SIGNUP-USECASE.md`](architecture/GUIDE-SIGNUP-USECASE.md) - Exemple UC-01

### Pour optimiser
1. [`tests-charge/baseline-k6.md`](tests-charge/baseline-k6.md) - État actuel
2. [`tests-charge/ANALYSE-k6.md`](tests-charge/ANALYSE-k6.md) - Problèmes identifiés
3. [`tests-charge/TODO-K6-FIXES.md`](tests-charge/TODO-K6-FIXES.md) - Actions à prendre

### Pour monitorer
1. [`observabilite/VERIFICATION-PROMETHEUS.md`](observabilite/VERIFICATION-PROMETHEUS.md) - Métriques
2. [`observabilite/GRAFANA-ACCESS-GUIDE.md`](observabilite/GRAFANA-ACCESS-GUIDE.md) - Dashboards
3. [`observabilite/SERILOG-EXPLICATIONS.md`](observabilite/SERILOG-EXPLICATIONS.md) - Logs

---

## 📝 Mise à jour

**Dernière mise à jour:** 28 octobre 2025  
**Phase actuelle:** Phase 2 - Étape 2a (Observabilité) ✅  
**Branche Git:** `phase2`

**Prochaines étapes:**
1. Implémenter fixes prioritaires (UC-03, DB optimization)
2. Load balancing NGINX (Phase 2 - Étape 2a suite)
3. API Gateway KrakenD (Phase 2 - Étape 2b)

---

## 🔗 Liens Externes

- **GitHub:** [theobrogrammer/projet-log-430](https://github.com/theobrogrammer/projet-log-430)
- **k6 Docs:** https://k6.io/docs/
- **Grafana Docs:** https://grafana.com/docs/
- **Prometheus Docs:** https://prometheus.io/docs/
- **Serilog Docs:** https://serilog.net/
