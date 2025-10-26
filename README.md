# BrokerX — Plateforme de courtage en ligne

Projet LOG-430 (ÉTS) — Architecture évolutive d'une application de courtage pour investisseurs particuliers.

## 📋 État du projet (Phase 2 en cours)

### ✅ Phase 1 - Monolith REST (Complété)
- **UC-01 Inscription**: SignupController + Domain (Client, Compte, KYC, OTP) ✅
- **UC-02 Authentification**: AuthController + Domain (Session, MFA) ✅
- **UC-03 Approvisionnement**: WalletController + Domain (PayTx, Ledger) 🔄
  - ⚠️ **TODO**: Implémenter idempotency-key pour dépôts

### 🚧 Phase 2 - Observabilité & Performance (En cours)
- [ ] Prometheus + Grafana (4 Golden Signals)
- [ ] Logs structurés (JSON + Serilog)
- [ ] Tests de charge k6 (scénarios signup/auth/deposit)
- [ ] NGINX Load Balancer (1→4 instances)
- [ ] Redis Cache (portfolios, market-data, reports)

### ⏳ Phase 3 - Microservices & Gateway (À venir)
- [ ] KrakenD API Gateway
- [ ] Découpage services (Orders, Portfolio, Reporting)
- [ ] Comparatifs A/B (direct vs gateway)
- [ ] CI/CD complet (GitHub Actions)

---

## 🏗️ Architecture

**Style**: Architecture Hexagonale (Ports & Adapters)  
**Stack**: .NET 9, MySQL, NGINX, Redis, KrakenD, Prometheus, Grafana, k6

### Projets .NET
```
src/
├── Domain/                    # Entités, règles métier, ports
├── Application/               # Services UC, orchestration
├── Infrastructure.Web/        # Controllers REST, Swagger
├── Infrastructure.Persistence/# EF Core repositories, migrations
└── Infrastructure.Adapters/   # Simulateurs (OTP, KYC, Payment)
```

### Diagrammes 4+1
Voir [docs/4+1/](./docs/4+1/) pour les vues architecturales détaillées :
- **Logique**: Classes domaine, bounded contexts
- **Processus**: Composants runtime, flux UC, observabilité
- **Déploiement**: Phases 1→3 (Monolith → LB/Cache → Gateway/µServices)
- **Développement**: Projets .NET, dépendances
- **Scénarios**: UC-01/02/03 détaillés (PUML)

📘 **Plan d'exécution complet**: [docs/4+1/README-execution.md](./docs/4+1/README-execution.md)

---

## 🚀 Quick Start

### Prérequis
- Docker & Docker Compose
- .NET 9 SDK
- k6 (tests de charge)

### Lancer l'application (Phase 1)
```bash
# Cloner le repo
git clone https://github.com/theobrogrammer/projet-log-430.git
cd projet-log-430

# Lancer avec Docker Compose
docker-compose up -d

# Vérifier l'API
curl http://localhost:8080/health
```

### Accéder aux services
- **API Swagger**: http://localhost:8080/swagger
- **Prometheus**: http://localhost:9090 (Phase 2)
- **Grafana**: http://localhost:3000 (Phase 2, admin/admin)
- **API Gateway (KrakenD)**: http://localhost:8000 (Phase 3)

### Exécuter les tests de charge (Phase 2)
```bash
# Test signup
k6 run scripts/k6/signup.js

# Test complet (mixed load)
k6 run scripts/k6/mixed.js
```

---

## 📊 Objectifs NFR (du cahier de charge)

| Métrique | Phase 1 | Phase 2 | Phase 3 |
|----------|---------|---------|---------|
| **Latence P95** | ≤ 500 ms | ≤ 250 ms | ≤ 100 ms |
| **Throughput** | ≥ 300 req/s | ≥ 800 req/s | ≥ 1200 req/s |
| **Disponibilité** | ≥ 90% | ≥ 95.5% | ≥ 99.9% |

---

## 📚 Documentation

- **Cahier de charge**: [docs/LOG430 - 2025.3 - Projet - Cahier de Charge.pdf](./docs/LOG430%20-%202025.3%20-%20Projet%20-%20Cahier%20de%20Charge.pdf)
- **Arc42**: [docs/arc42/arc42.md](./docs/arc42/arc42.md)
- **ADRs**: [docs/adr/](./docs/adr/)
- **Use Cases**: [docs/use-cases.md](./docs/use-cases.md)
- **Glossaire**: [docs/domain-glossary.md](./docs/domain-glossary.md)
- **Troubleshooting**: [docs/troubleshooting-guide.md](./docs/troubleshooting-guide.md)

---

## 🔧 Commandes utiles

```bash
# Build l'application
dotnet build

# Run tests unitaires
dotnet test tests/Domain.Tests

# Run tests E2E
dotnet test tests/E2E.Tests

# Migrations DB
dotnet ef migrations add <MigrationName> --project src/Infrastructure.Persistence
dotnet ef database update --project src/Infrastructure.Persistence

# Scaler l'app avec Docker Compose (Phase 2)
docker-compose up --scale app=4

# Vérifier cache Redis
docker exec -it <redis-container> redis-cli INFO stats

# Logs Prometheus metrics
curl http://localhost:8080/metrics
```

---

## 🎯 Prochaines étapes immédiates

1. ✅ **Finaliser UC-03 idempotency** (1-2h)
   - Ajouter `IdempotencyKey` à `PayTx`
   - Migration + index unique
   - Tests E2E retry idempotent

2. 🚧 **Setup Observabilité** (3-4h)
   - Installer prometheus-net
   - Configurer Prometheus/Grafana (docker-compose)
   - Dashboard 4 Golden Signals

3. 🚧 **Tests k6 baseline** (2-3h)
   - Scénarios signup/auth/deposit
   - Mesurer NFR Phase 1
   - Exporter résultats JSON

4. 🚧 **NGINX Load Balancer** (2-3h)
   - Config nginx.conf
   - Tests scaling 1→4 instances
   - Graphiques comparatifs

👉 Voir checklist complète: [docs/4+1/README-execution.md](./docs/4+1/README-execution.md)

---

## 👥 Équipe

Projet académique LOG-430 — ÉTS Montréal

## 📄 Licence

Projet académique — tous droits réservés.
