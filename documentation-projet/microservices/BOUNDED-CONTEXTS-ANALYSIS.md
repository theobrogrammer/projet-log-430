# Analyse Bounded Contexts - Extraction Microservices Phase 2b

## État Actuel: Architecture Monolithique Hexagonale

### Domaine Existant

#### 1. **Identite** (Core Bounded Context)
**Entités:**
- `Client` (aggregate root)
  - ClientId, Email, Telephone, NomComplet, PasswordHash, Statut
  - Relations: Comptes (1:N), ContactOtps (1:N), DossierKYC (1:1)
- `Compte` (Account)
  - AccountId, ClientId, AccountNo, Statut
- `DossierKYC`
- `VerifContactOTP`

**Use Cases:**
- UC-01: Inscription (SignupService)
- UC-02: Authentification (AuthService)

**Responsabilités:**
- Gestion identité client
- Validation KYC/OTP
- Authentification MFA
- Création de comptes

#### 2. **PortefeuilleReglement** (Wallet & Settlement Context)
**Entités:**
- `Portefeuille` (Portfolio/Wallet)
  - Id, AccountId, Devise, SoldeMonnaie
  - Méthodes: Crediter(), Debiter()
- `TransactionPaiement` (Payment Transaction)
  - PaymentTxId, AccountId, Amount, Currency, IdempotencyKey, Statut
- `EcritureLedger` (Ledger Entry - append-only)
  - LedgerEntryId, AccountId, Amount, Kind, RefType, RefId

**Use Cases:**
- UC-03: Dépôt (WalletService)
- Settlement callback

**Responsabilités:**
- Gestion balance monétaire
- Transactions de paiement (idempotence)
- Historique comptable (ledger)

#### 3. **Securite** (Security Context)
**Entités:**
- `PolitiqueMFA`
- `DefiMFA`
- `Session`

#### 4. **Observabilite** (Cross-cutting)
**Entités:**
- `AuditLog`

---

## Analyse Bounded Contexts pour Microservices

### ❌ PROBLÈME: Le plan proposé ne correspond PAS au domaine existant

**Plan proposé:**
1. Orders (ordres trading) → **N'EXISTE PAS dans le domaine actuel**
2. Portfolio (positions/balance) → **Existe mais couplé à Client/Account**
3. Reporting (analytics) → **N'EXISTE PAS comme contexte distinct**

**Réalité du domaine actuel:**
- Aucune entité `Order` (ordre trading)
- Aucune entité `Position` (position boursière)
- Portfolio = juste un wallet avec balance monétaire (SoldeMonnaie)
- Pas de trading, pas d'ordres, pas de market data

### 🎯 PROPOSITION: Extraction Réaliste Basée sur le Domaine Actuel

#### Option A: Extraction Conservatrice (RECOMMANDÉE pour Phase 2b)

**Microservice 1: Identity Service (port 8081)**
- **Bounded Context:** Identite
- **Entités:** Client, Compte, DossierKYC, VerifContactOTP
- **Endpoints:**
  - POST /api/v1/signup
  - POST /api/v1/verify-otp
  - POST /api/v1/login
  - POST /api/v1/mfa/verify
- **Database:** Shared DB (tables: Clients, Accounts, KycDossiers, ContactOtps)
- **Responsabilité:** Gestion cycle de vie client + auth

**Microservice 2: Wallet Service (port 8082)**
- **Bounded Context:** PortefeuilleReglement
- **Entités:** Portefeuille, TransactionPaiement, EcritureLedger
- **Endpoints:**
  - POST /api/v1/accounts/{accountId}/deposit
  - GET /api/v1/accounts/{accountId}/balance
  - GET /api/v1/accounts/{accountId}/transactions
  - GET /api/v1/accounts/{accountId}/ledger
- **Database:** Shared DB (tables: Portfolios, PayTxs, LedgerEntries)
- **Responsabilité:** Gestion balance + transactions + ledger

**Microservice 3: Reporting Service (port 8083)** *(Read-only)*
- **Bounded Context:** Analytics/Observabilite
- **Entités:** Aucune (read-only queries)
- **Endpoints:**
  - GET /api/v1/reports/clients/summary
  - GET /api/v1/reports/transactions/export
  - GET /api/v1/reports/audit-logs
- **Database:** Shared DB (read-only access)
- **Responsabilité:** Agrégation metrics + exports

**API Gateway (KrakenD - port 8080):**
```
/api/v1/signup          → Identity Service :8081
/api/v1/login           → Identity Service :8081
/api/v1/accounts/*/deposit → Wallet Service :8082
/api/v1/accounts/*/balance → Wallet Service :8082
/api/v1/reports/*       → Reporting Service :8083
```

#### Option B: Extraction Ambitieuse (Phase 3 - Hors scope Phase 2b)

Ajouter de nouveaux bounded contexts (Orders Trading, Market Data, Portfolio Positions) nécessite:
1. Créer nouvelles entités métier (Order, Position, Instrument)
2. Nouveaux use cases (PlaceOrder, CancelOrder, UpdatePosition)
3. Nouvelles règles métier (validation ordres, risk management)
4. Intégration market data externe

**⚠️ HORS SCOPE Phase 2b** - Trop complexe pour extraction microservices

---

## Dépendances et Couplage

### Dépendances Actuelles (Monolithe)

```
SignupService
  → IClientRepository (Client + Compte + DossierKYC)
  → IAccountRepository (Compte)
  → IPortfolioRepository (Portefeuille - création initiale)
  → IOtpPort, IKycPort, IAuditPort

AuthService
  → IClientRepository (Client)
  → IMfaPolicyRepository, IMfaChallengeRepository
  → ISessionRepository (Session)

WalletService
  → IPayTxRepository (TransactionPaiement)
  → IPortfolioRepository (Portefeuille)
  → ILedgerPort (EcritureLedger)
  → IPaymentPort, IAuditPort
```

### Couplage Fort Identifié

1. **Client ↔ Compte ↔ Portefeuille**
   - SignupService crée les 3 agrégats dans une transaction
   - Cohérence transactionnelle forte (ACID)
   - **Risque:** Saga distribuée complexe si séparation

2. **AccountId référencé partout**
   - Portefeuille.AccountId → FK vers Compte
   - TransactionPaiement.AccountId → FK vers Compte
   - **Solution:** Conserver AccountId comme identifiant partagé (pas de FK cross-service)

3. **Shared Database (temporaire)**
   - Phase 2b: Tous services partagent même MySQL
   - Phase 3: Migrer vers Database-per-Service + événements

---

## Stratégie de Découpage Recommandée

### Phase 2b: Strangler Pattern + Shared Database

**Approche:**
1. **Conserver monolithe principal** (Identity Service)
2. **Extraire Wallet Service** (lecture + écriture balance)
3. **Extraire Reporting Service** (lecture seule)
4. **Shared Database** (éviter complexité saga distribuée)
5. **Communication synchrone REST** (pas de message queue Phase 2b)

**Bénéfices:**
- ✅ Isolation déploiement (Wallet peut crash sans impacter Identity)
- ✅ Scaling indépendant (Wallet service peut scaler 4 instances)
- ✅ Résilience testable (circuit breaker KrakenD)
- ✅ Pas de complexité événementielle (Phase 3)

**Trade-offs:**
- ⚠️ Shared DB = couplage persistance (acceptable Phase 2b)
- ⚠️ REST sync = latence réseau (~5-10ms overhead)
- ⚠️ Pas de véritable autonomie (schema migrations coordonnées)

---

## Communication Inter-Services

### Scénario 1: Signup (Identity → Wallet)

**Problème:** SignupService crée Client + Compte + Portefeuille atomique

**Solution Phase 2b:**
1. Identity Service crée Client + Compte (transaction DB)
2. Identity Service **appelle REST** Wallet Service: `POST /internal/portfolios` avec AccountId
3. Si Wallet échoue: **compensation manuelle** (logs + monitoring)
4. ⚠️ Pas de saga automatique Phase 2b

**Alternative Phase 3:**
- Event Sourcing: `ClientCreated` → Wallet consomme événement
- Saga orchestrator (MassTransit, NServiceBus)

### Scénario 2: Deposit (Wallet isolé)

**Aucun problème:** WalletService est autonome
- Reçoit accountId
- Vérifie balance
- Crée TransactionPaiement
- Met à jour Portefeuille
- Écrit EcritureLedger

### Scénario 3: Reporting (Lecture cross-context)

**Solution simple:** Shared Database read-only
- Reporting Service query directement tables Clients, Portfolios, PayTxs
- ⚠️ Couplage schema mais acceptable pour read-only

**Alternative Phase 3:**
- CQRS: Reporting Database séparée (projections événements)

---

## Décision Finale: Option A (Extraction Conservatrice)

**Services à créer:**
1. ✅ **Services.Identity** (signup, login, MFA)
2. ✅ **Services.Wallet** (deposit, balance, transactions)
3. ✅ **Services.Reporting** (metrics, exports)

**Services à NE PAS créer:**
- ❌ Services.Orders (pas d'entités Order dans domaine)
- ❌ Services.Portfolio (Portfolio ≠ Wallet dans ce projet)
- ❌ Services.Trading (hors scope)

**Architecture cible Phase 2b:**
```
Client
  ↓
KrakenD Gateway :8080
  ↓
  ├─→ Identity Service :8081 (signup, login)
  ├─→ Wallet Service :8082 (deposit, balance)
  └─→ Reporting Service :8083 (metrics)
  
Tous partagent MySQL :3307 (Shared Database)
```

**Next Steps:**
1. Créer structure projets Services.Identity/Wallet/Reporting
2. Copier entités domaine pertinentes
3. Extraire use cases dans chaque service
4. Configurer communication REST (sans Polly Phase 2b)
5. Dockeriser + docker-compose
6. Configurer KrakenD routing
7. Tests isolation (kill service, vérifier autres OK)

---

## Risques et Mitigation

| Risque | Impact | Mitigation |
|--------|--------|-----------|
| Transaction distribuée (signup) | ⭐⭐⭐⭐⭐ Critical | Shared DB Phase 2b, Saga Phase 3 |
| Latence réseau inter-services | ⭐⭐⭐ Medium | Acceptable pour Phase 2b (2-5ms) |
| Schema migration coordination | ⭐⭐⭐⭐ High | Versioning API, backward compatibility |
| Debugging complexe (logs distribués) | ⭐⭐⭐ Medium | Correlation ID, Serilog structured logs |
| Duplication code domaine | ⭐⭐ Low | Shared kernel OU copie explicite (bounded contexts) |

---

**Date:** 2025-10-28  
**Auteur:** AI Analysis  
**Statut:** ✅ Analyse terminée, prêt pour implémentation Option A
