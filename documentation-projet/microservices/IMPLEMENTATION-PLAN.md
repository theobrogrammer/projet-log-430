# Plan d'Implémentation Microservices - Phase 2b

## ⚠️ VALIDATION REQUISE AVANT EXÉCUTION

**Lecture obligatoire:** `docs/microservices/BOUNDED-CONTEXTS-ANALYSIS.md`

---

## 🎯 Décision Architecture

### Option Retenue: **Extraction Conservatrice (Option A)**

**Rationnel:**
- Le domaine actuel N'A PAS d'entités `Order`, `Position`, `Trading`
- Portfolio = simple wallet (balance monétaire CAD)
- Extraction réaliste basée sur bounded contexts existants

### Services à Créer

#### 1. Identity Service (port 8081)
**Responsabilité:** Gestion cycle de vie client + authentification
- UC-01: Inscription (signup + OTP + KYC)
- UC-02: Authentification (login + MFA)

**Entités copiées:**
- Client, Compte, DossierKYC, VerifContactOTP
- PolitiqueMFA, DefiMFA, Session

**Endpoints:**
```
POST /api/v1/signup
POST /api/v1/verify-otp
POST /api/v1/login
POST /api/v1/mfa/verify
GET  /api/v1/clients/{id}  (internal)
```

#### 2. Wallet Service (port 8082)
**Responsabilité:** Gestion balance + transactions + ledger
- UC-03: Dépôt (deposit + idempotence)
- Lecture balance
- Historique transactions

**Entités copiées:**
- Portefeuille, TransactionPaiement, EcritureLedger

**Endpoints:**
```
POST /api/v1/accounts/{accountId}/deposit
GET  /api/v1/accounts/{accountId}/balance
GET  /api/v1/accounts/{accountId}/transactions
GET  /api/v1/accounts/{accountId}/ledger
POST /internal/portfolios  (appelé par Identity lors signup)
```

#### 3. Reporting Service (port 8083)
**Responsabilité:** Analytics read-only + exports
- Métriques agrégées
- Exports CSV/JSON

**Entités:** Aucune (read-only queries)

**Endpoints:**
```
GET /api/v1/reports/clients/summary
GET /api/v1/reports/transactions/export?from=...&to=...
GET /api/v1/reports/metrics/deposits
```

---

## 📐 Structure Projets

### Arborescence Cible

```
src/
├── Domain/                    # ⚠️ CONSERVER (shared kernel)
├── Application/               # ⚠️ CONSERVER (monolithe legacy)
├── Infrastructure.Web/        # ⚠️ CONSERVER (API principale - optionnel Phase 2b)
├── Infrastructure.Persistence/# ⚠️ CONSERVER (shared DbContext)
├── Infrastructure.Adapters/   # ⚠️ CONSERVER
│
├── Services.Identity/         # ✅ NOUVEAU
│   ├── Services.Identity.Domain/
│   │   ├── Model/
│   │   │   ├── Client.cs           (copie from Domain/Model/Identite)
│   │   │   ├── Compte.cs
│   │   │   ├── DossierKYC.cs
│   │   │   └── VerifContactOTP.cs
│   │   └── Ports/
│   │       ├── Inbound/
│   │       │   ├── ISignupUseCase.cs
│   │       │   └── IAuthUseCase.cs
│   │       └── Outbound/
│   │           ├── IClientRepository.cs
│   │           └── IOtpPort.cs
│   ├── Services.Identity.Application/
│   │   └── Services/
│   │       ├── SignupService.cs    (copie from Application/Services)
│   │       └── AuthService.cs
│   ├── Services.Identity.Infrastructure.Web/
│   │   ├── Controllers/
│   │   │   ├── SignupController.cs
│   │   │   └── AuthController.cs
│   │   ├── DTOs/
│   │   ├── Program.cs
│   │   └── appsettings.json
│   └── Services.Identity.Infrastructure.Persistence/
│       └── Repositories/
│           └── ClientRepository.cs  (utilise BrokerXDbContext shared)
│
├── Services.Wallet/           # ✅ NOUVEAU
│   ├── Services.Wallet.Domain/
│   │   ├── Model/
│   │   │   ├── Portefeuille.cs     (copie)
│   │   │   ├── TransactionPaiement.cs
│   │   │   └── EcritureLedger.cs
│   │   └── Ports/
│   ├── Services.Wallet.Application/
│   │   └── Services/
│   │       └── WalletService.cs    (copie)
│   ├── Services.Wallet.Infrastructure.Web/
│   │   ├── Controllers/
│   │   │   ├── WalletController.cs
│   │   │   └── InternalPortfolioController.cs  (POST /internal/portfolios)
│   │   ├── Program.cs
│   │   └── appsettings.json
│   └── Services.Wallet.Infrastructure.Persistence/
│       └── Repositories/
│
└── Services.Reporting/        # ✅ NOUVEAU
    ├── Services.Reporting.Application/
    │   └── Queries/
    │       ├── ClientSummaryQuery.cs
    │       └── TransactionExportQuery.cs
    ├── Services.Reporting.Infrastructure.Web/
    │   ├── Controllers/
    │   │   └── ReportsController.cs
    │   ├── Program.cs
    │   └── appsettings.json
    └── Services.Reporting.Infrastructure.Persistence/
        └── Queries/  (Dapper pour queries optimisées)
```

### Projets .csproj à Créer

```bash
# Identity Service
dotnet new webapi -n Services.Identity.Infrastructure.Web -o src/Services.Identity/Services.Identity.Infrastructure.Web
dotnet new classlib -n Services.Identity.Domain -o src/Services.Identity/Services.Identity.Domain
dotnet new classlib -n Services.Identity.Application -o src/Services.Identity/Services.Identity.Application
dotnet new classlib -n Services.Identity.Infrastructure.Persistence -o src/Services.Identity/Services.Identity.Infrastructure.Persistence

# Wallet Service
dotnet new webapi -n Services.Wallet.Infrastructure.Web -o src/Services.Wallet/Services.Wallet.Infrastructure.Web
dotnet new classlib -n Services.Wallet.Domain -o src/Services.Wallet/Services.Wallet.Domain
dotnet new classlib -n Services.Wallet.Application -o src/Services.Wallet/Services.Wallet.Application
dotnet new classlib -n Services.Wallet.Infrastructure.Persistence -o src/Services.Wallet/Services.Wallet.Infrastructure.Persistence

# Reporting Service
dotnet new webapi -n Services.Reporting.Infrastructure.Web -o src/Services.Reporting/Services.Reporting.Infrastructure.Web
dotnet new classlib -n Services.Reporting.Application -o src/Services.Reporting/Services.Reporting.Application
dotnet new classlib -n Services.Reporting.Infrastructure.Persistence -o src/Services.Reporting/Services.Reporting.Infrastructure.Persistence
```

---

## 🔗 Communication Inter-Services

### Stratégie Phase 2b: REST Synchrone + Shared Database

**Principe:**
- Tous les services partagent MySQL:3307
- Communication REST directe (pas de message queue)
- Pas de saga distribuée (transactions locales uniquement)

### Scénario Critique: Signup

**Problème:** SignupService crée Client + Compte + **Portefeuille** atomique

**Solution temporaire Phase 2b:**

1. **Identity Service** (transaction DB locale):
   ```csharp
   // SignupService dans Identity
   var client = Client.Creer(...);
   var compte = client.OuvrirCompte();
   await _clientRepo.AddAsync(client);
   await _accountRepo.AddAsync(compte);
   // ✅ Commit transaction
   
   // ⚠️ Appel REST synchrone vers Wallet Service
   var httpClient = _httpClientFactory.CreateClient("WalletService");
   var response = await httpClient.PostAsync(
       $"/internal/portfolios", 
       new { AccountId = compte.AccountId, Currency = "CAD" }
   );
   
   if (!response.IsSuccessStatusCode)
   {
       // ⚠️ PROBLÈME: Client déjà créé mais Portefeuille échoué
       // Phase 2b: Logger erreur + monitoring manuel
       // Phase 3: Saga distribuée avec compensation
       _logger.LogError($"Wallet creation failed for account {compte.AccountId}");
   }
   ```

2. **Wallet Service** (endpoint interne):
   ```csharp
   // InternalPortfolioController dans Wallet
   [HttpPost("/internal/portfolios")]
   public async Task<IActionResult> CreatePortfolio([FromBody] CreatePortfolioRequest req)
   {
       var portfolio = Portefeuille.Ouvrir(req.AccountId, req.Currency);
       await _portfolioRepo.AddAsync(portfolio);
       return Ok(new { PortfolioId = portfolio.Id });
   }
   ```

**⚠️ Trade-off accepté Phase 2b:**
- Pas de compensation automatique si Wallet échoue
- Monitoring + alertes manuelles
- Phase 3: Implémenter saga (MassTransit/Outbox pattern)

### Configuration HttpClient (Resilience basique)

```csharp
// Program.cs dans Identity Service
builder.Services.AddHttpClient("WalletService", client =>
{
    client.BaseAddress = new Uri("http://wallet:8082");
    client.Timeout = TimeSpan.FromSeconds(5);
});

// ⚠️ Pas de Polly retry/circuit breaker Phase 2b (KrakenD gère ça)
```

---

## 🐳 Containerisation

### Dockerfile par Service

**Template (Identity/Wallet/Reporting similaires):**

```dockerfile
# src/Services.Identity/Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copier TOUS les csproj (dépendances shared)
COPY ["src/Services.Identity/Services.Identity.Infrastructure.Web/Services.Identity.Infrastructure.Web.csproj", "Services.Identity.Infrastructure.Web/"]
COPY ["src/Services.Identity/Services.Identity.Application/Services.Identity.Application.csproj", "Services.Identity.Application/"]
COPY ["src/Services.Identity/Services.Identity.Domain/Services.Identity.Domain.csproj", "Services.Identity.Domain/"]
COPY ["src/Services.Identity/Services.Identity.Infrastructure.Persistence/Services.Identity.Infrastructure.Persistence.csproj", "Services.Identity.Infrastructure.Persistence/"]
COPY ["src/Infrastructure.Persistence/Infrastructure.Persistence.csproj", "Infrastructure.Persistence/"]  # Shared DbContext

RUN dotnet restore "Services.Identity.Infrastructure.Web/Services.Identity.Infrastructure.Web.csproj"

COPY src/ .

WORKDIR "/src/Services.Identity.Infrastructure.Web"
RUN dotnet build -c Release -o /app/build
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Services.Identity.Infrastructure.Web.dll"]
```

### docker-compose.yml (mise à jour)

```yaml
services:
  # ===================================================================
  # API Monolithe (legacy - optionnel Phase 2b, peut être désactivé)
  # ===================================================================
  # api:
  #   container_name: brokerx-api
  #   build: .
  #   ports:
  #     - "5000:5000"
  
  # ===================================================================
  # Identity Service (Microservice 1)
  # ===================================================================
  identity:
    container_name: brokerx-identity
    build:
      context: .
      dockerfile: src/Services.Identity/Dockerfile
    ports:
      - "5001:8081"  # Debug externe
    environment:
      ASPNETCORE_URLS: http://0.0.0.0:8081
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__BrokerX: Server=mysql;Port=3306;Database=brokerx;User Id=brokerx;Password=brokerx
      ServiceUrls__Wallet: http://wallet:8082
    depends_on:
      mysql:
        condition: service_healthy
    networks:
      - brokerx-network
  
  # ===================================================================
  # Wallet Service (Microservice 2)
  # ===================================================================
  wallet:
    container_name: brokerx-wallet
    build:
      context: .
      dockerfile: src/Services.Wallet/Dockerfile
    ports:
      - "5002:8082"  # Debug externe
    environment:
      ASPNETCORE_URLS: http://0.0.0.0:8082
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__BrokerX: Server=mysql;Port=3306;Database=brokerx;User Id=brokerx;Password=brokerx
    depends_on:
      mysql:
        condition: service_healthy
    networks:
      - brokerx-network
  
  # ===================================================================
  # Reporting Service (Microservice 3)
  # ===================================================================
  reporting:
    container_name: brokerx-reporting
    build:
      context: .
      dockerfile: src/Services.Reporting/Dockerfile
    ports:
      - "5003:8083"  # Debug externe
    environment:
      ASPNETCORE_URLS: http://0.0.0.0:8083
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__BrokerX: Server=mysql;Port=3306;Database=brokerx;User Id=brokerx;Password=brokerx
    depends_on:
      mysql:
        condition: service_healthy
    networks:
      - brokerx-network
  
  # ===================================================================
  # KrakenD Gateway (déjà configuré Phase 2b)
  # ===================================================================
  krakend:
    container_name: brokerx-krakend
    image: devopsfaith/krakend:2.7
    ports:
      - "8080:8080"
    volumes:
      - ./krakend.json:/etc/krakend/krakend.json:ro
    depends_on:
      identity:
        condition: service_started
      wallet:
        condition: service_started
      reporting:
        condition: service_started
    networks:
      - brokerx-network
  
  # MySQL, Redis, Prometheus, Grafana, NGINX (inchangés)
```

---

## 🛣️ KrakenD Configuration

### krakend.json (mise à jour)

```json
{
  "version": 3,
  "port": 8080,
  "timeout": "5s",
  "extra_config": {
    "telemetry/opencensus": {
      "exporters": {
        "prometheus": {"port": 9091}
      }
    }
  },
  "endpoints": [
    {
      "endpoint": "/api/v1/signup",
      "method": "POST",
      "backend": [{
        "url_pattern": "/api/v1/signup",
        "host": ["http://identity:8081"],
        "method": "POST"
      }],
      "extra_config": {
        "qos/ratelimit/router": {
          "max_rate": 10
        }
      }
    },
    {
      "endpoint": "/api/v1/verify-otp",
      "method": "POST",
      "backend": [{
        "url_pattern": "/api/v1/verify-otp",
        "host": ["http://identity:8081"]
      }]
    },
    {
      "endpoint": "/api/v1/login",
      "method": "POST",
      "backend": [{
        "url_pattern": "/api/v1/login",
        "host": ["http://identity:8081"]
      }]
    },
    {
      "endpoint": "/api/v1/accounts/{accountId}/deposit",
      "method": "POST",
      "backend": [{
        "url_pattern": "/api/v1/accounts/{accountId}/deposit",
        "host": ["http://wallet:8082"]
      }],
      "extra_config": {
        "qos/circuit-breaker": {
          "max_errors": 5,
          "interval": 10,
          "timeout": 5
        }
      }
    },
    {
      "endpoint": "/api/v1/accounts/{accountId}/balance",
      "method": "GET",
      "backend": [{
        "url_pattern": "/api/v1/accounts/{accountId}/balance",
        "host": ["http://wallet:8082"]
      }],
      "extra_config": {
        "qos/http-cache": {
          "shared": true
        }
      }
    },
    {
      "endpoint": "/api/v1/reports/{type}",
      "method": "GET",
      "backend": [{
        "url_pattern": "/api/v1/reports/{type}",
        "host": ["http://reporting:8083"]
      }]
    }
  ]
}
```

---

## 🧪 Tests

### Test 1: Isolation Services

```bash
# Démarrer tous services
docker-compose up -d

# Test Identity OK
curl http://localhost:8080/api/v1/signup -X POST -H "Content-Type: application/json" -d '{...}'
# ✅ Expect HTTP 201

# Stopper Wallet
docker stop brokerx-wallet

# Test Identity toujours OK
curl http://localhost:8080/api/v1/login -X POST -H "Content-Type: application/json" -d '{...}'
# ✅ Expect HTTP 200 (pas affecté)

# Test Deposit échoue
curl http://localhost:8080/api/v1/accounts/{id}/deposit -X POST -d '{...}'
# ⚠️ Expect HTTP 503 (circuit breaker KrakenD)

# Redémarrer Wallet
docker start brokerx-wallet

# Test Deposit OK
# ✅ Expect HTTP 200 (récupération automatique)
```

### Test 2: Performance Comparison

```bash
# Test monolithe (si conservé)
BASE_URL=http://localhost:5000 k6 run --duration 30s --vus 10 scripts/k6/signup.js

# Test microservices via gateway
BASE_URL=http://localhost:8080 k6 run --duration 30s --vus 10 scripts/k6/signup.js

# Comparer latence P95
# Attendu: +5-10ms overhead microservices (acceptable)
```

### Test 3: Shared Database

```bash
# Vérifier que tous services partagent même DB
docker exec brokerx-identity dotnet ef database list
docker exec brokerx-wallet dotnet ef database list
# ✅ Expect: même database 'brokerx'
```

---

## 📋 Checklist Implémentation

### Étape 1: Création Structure (2h)
- [ ] Créer projets Services.Identity (4 projets)
- [ ] Créer projets Services.Wallet (4 projets)
- [ ] Créer projets Services.Reporting (3 projets)
- [ ] Ajouter tous projets à ProjetLog430.sln

### Étape 2: Copie Domaine (1h)
- [ ] Copier entités Identite vers Services.Identity.Domain
- [ ] Copier entités PortefeuilleReglement vers Services.Wallet.Domain
- [ ] Ajuster namespaces

### Étape 3: Extraction Use Cases (2h)
- [ ] Copier SignupService/AuthService vers Services.Identity.Application
- [ ] Copier WalletService vers Services.Wallet.Application
- [ ] Créer queries Reporting

### Étape 4: Controllers (1h)
- [ ] Implémenter SignupController, AuthController (Identity)
- [ ] Implémenter WalletController, InternalPortfolioController (Wallet)
- [ ] Implémenter ReportsController (Reporting)

### Étape 5: Configuration DI (1h)
- [ ] Program.cs Identity: DI repositories, ports, services
- [ ] Program.cs Wallet: DI repositories, ports, services
- [ ] Program.cs Reporting: DI queries
- [ ] Configuration HttpClient inter-services

### Étape 6: Dockerfiles (30min)
- [ ] Créer Dockerfile Identity
- [ ] Créer Dockerfile Wallet
- [ ] Créer Dockerfile Reporting

### Étape 7: docker-compose (30min)
- [ ] Ajouter services identity, wallet, reporting
- [ ] Configurer environment variables
- [ ] Configurer depends_on

### Étape 8: KrakenD (30min)
- [ ] Mettre à jour krakend.json routing
- [ ] Tester endpoints via gateway

### Étape 9: Tests (1h)
- [ ] Test isolation (kill service)
- [ ] Test performance k6
- [ ] Test communication inter-services

---

## 🚨 Risques et Décisions à Valider

### ❓ Question 1: Conserver API Monolithe?

**Option A:** Désactiver API monolithe, tout passe par microservices
- ✅ Architecture propre
- ⚠️ Refactoring complet

**Option B:** Conserver API monolithe en parallèle (Strangler)
- ✅ Migration progressive
- ⚠️ Duplication endpoints

**→ DÉCISION REQUISE**

### ❓ Question 2: Gestion Transaction Signup?

**Option A:** Accepter inconsistance temporaire Phase 2b
- ✅ Simple
- ⚠️ Risque Portefeuille orphelin si Wallet crash

**Option B:** Implémenter Saga dès Phase 2b (MassTransit)
- ✅ Robuste
- ⚠️ Complexité élevée

**→ DÉCISION REQUISE**

### ❓ Question 3: Duplication Code Domaine?

**Option A:** Copier entités dans chaque service (bounded contexts stricts)
- ✅ Autonomie complète
- ⚠️ Duplication code

**Option B:** Shared kernel (projet Domain partagé)
- ✅ DRY
- ⚠️ Couplage fort

**→ DÉCISION REQUISE**

---

## ⏱️ Estimation Totale

**Temps développement:** 8-10 heures
**Temps tests:** 2-3 heures
**Total Phase 2b microservices:** 10-13 heures

---

## 📝 Next Steps

1. **VALIDATION UTILISATEUR:** Lire BOUNDED-CONTEXTS-ANALYSIS.md
2. **DÉCISIONS:** Répondre aux 3 questions ci-dessus
3. **GO/NO-GO:** Confirmer extraction Option A
4. **EXÉCUTION:** Suivre checklist étape par étape

**Status:** ⏸️ EN ATTENTE VALIDATION UTILISATEUR
