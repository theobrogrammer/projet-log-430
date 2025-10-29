# 🚨 VALIDATION REQUISE - Extraction Microservices Phase 2b

## TL;DR

**Votre plan initial propose Orders/Portfolio/Reporting, mais votre domaine actuel N'A PAS d'entités Order ni Trading.**

**Je propose:** Identity Service + Wallet Service + Reporting Service (basé sur bounded contexts réels)

---

## ❌ Problème avec le Plan Proposé

### Ce que vous avez demandé:
1. **Orders Service** (ordres trading: CreateOrder, GetOrder, CancelOrder)
2. **Portfolio Service** (positions/balance: GetBalance, GetPositions, GetHistory)
3. **Reporting Service** (analytics: GetMetrics, ExportTransactions)

### Pourquoi ça ne marche pas:

**Analyse du domaine actuel** (`src/Domain/Model/`):
```
✅ Identite/
   - Client.cs (email, password, KYC, OTP)
   - Compte.cs (AccountId, AccountNo)
   - DossierKYC.cs, VerifContactOTP.cs

✅ PortefeuilleReglement/
   - Portefeuille.cs (SoldeMonnaie CAD - simple wallet)
   - TransactionPaiement.cs (deposits)
   - EcritureLedger.cs (append-only ledger)

✅ Securite/
   - PolitiqueMFA.cs, DefiMFA.cs, Session.cs

❌ AUCUNE ENTITÉ:
   - Order (ordre trading)
   - Position (position boursière)
   - Instrument (actions, crypto)
   - Execution (fills)
   - MarketData
```

**Votre projet = Plateforme inscription/auth/wallet, PAS plateforme trading.**

Portfolio ≠ Wallet dans votre domaine:
- **Wallet** = balance monétaire (dépôts CAD)
- **Portfolio** (trading) = positions instruments financiers (n'existe pas ici)

---

## ✅ Proposition: Extraction Réaliste (Option A)

### Service 1: Identity Service (port 8081)
**Bounded Context:** Identite
**Responsabilité:** Gestion cycle de vie client + auth

**Entités:**
- Client, Compte, DossierKYC, VerifContactOTP
- PolitiqueMFA, DefiMFA, Session

**Endpoints:**
```
POST /api/v1/signup
POST /api/v1/verify-otp
POST /api/v1/login
POST /api/v1/mfa/verify
```

**Use Cases:** UC-01 (Inscription), UC-02 (Authentification)

---

### Service 2: Wallet Service (port 8082)
**Bounded Context:** PortefeuilleReglement
**Responsabilité:** Gestion balance + transactions + ledger

**Entités:**
- Portefeuille, TransactionPaiement, EcritureLedger

**Endpoints:**
```
POST /api/v1/accounts/{accountId}/deposit
GET  /api/v1/accounts/{accountId}/balance
GET  /api/v1/accounts/{accountId}/transactions
GET  /api/v1/accounts/{accountId}/ledger
POST /internal/portfolios  (appelé par Identity lors signup)
```

**Use Cases:** UC-03 (Dépôt)

---

### Service 3: Reporting Service (port 8083)
**Bounded Context:** Analytics/Observabilite
**Responsabilité:** Métriques + exports (READ-ONLY)

**Pas d'entités** (queries directes DB)

**Endpoints:**
```
GET /api/v1/reports/clients/summary
GET /api/v1/reports/transactions/export?from=...&to=...
GET /api/v1/reports/metrics/deposits
```

---

## 🔑 3 Décisions Critiques à Valider

### Décision 1: API Monolithe

**Option A:** Désactiver API monolithe (tout via microservices)
- ✅ Architecture propre
- ⚠️ Refactoring complet

**Option B:** Conserver API monolithe en parallèle (Strangler Pattern)
- ✅ Migration progressive
- ⚠️ Duplication endpoints temporaire

**→ VOTRE CHOIX: A ou B?**

---

### Décision 2: Transaction Distribuée (Signup)

**Problème:** SignupService crée **Client + Compte + Portefeuille** dans une transaction atomique actuellement.

**Avec microservices:**
- Identity Service crée Client + Compte (transaction locale)
- Identity appelle REST: `POST http://wallet:8082/internal/portfolios` avec AccountId
- **Risque:** Si Wallet crash → Client existe sans Portefeuille

**Option A:** Accepter risque inconsistance Phase 2b (monitoring + compensation manuelle)
- ✅ Simple
- ⚠️ Inconsistance possible

**Option B:** Implémenter Saga distribuée dès Phase 2b (MassTransit/Outbox)
- ✅ Robuste (compensation automatique)
- ⚠️ Complexité élevée (+20h dev)

**→ VOTRE CHOIX: A ou B?**

---

### Décision 3: Code Domaine

**Option A:** Copier entités domaine dans chaque service (Bounded Contexts stricts)
- ✅ Autonomie totale (chaque service indépendant)
- ⚠️ Duplication code (Client.cs existe 2 fois)

**Option B:** Shared Kernel (projet Domain partagé par tous)
- ✅ DRY (pas de duplication)
- ⚠️ Couplage fort (changement Client impacte tous services)

**→ VOTRE CHOIX: A ou B?**

---

## 🛣️ Architecture Cible Phase 2b

```
Client HTTP
  ↓
KrakenD Gateway :8080
  ↓
  ├─→ Identity Service :8081
  │   └─→ MySQL :3307 (shared DB)
  │
  ├─→ Wallet Service :8082
  │   └─→ MySQL :3307 (shared DB)
  │
  └─→ Reporting Service :8083
      └─→ MySQL :3307 (shared DB - read only)

Services internes:
- NGINX Load Balancer (optionnel Phase 2b)
- Redis Cache (optionnel Phase 2b)
- Prometheus :9090
- Grafana :3000
```

**Shared Database Phase 2b:**
- Accepté temporairement (évite complexité saga)
- Migration vers Database-per-Service en Phase 3

---

## 📋 Checklist Implémentation

**Estimation:** 10-13 heures

1. ✅ Analyse domaine (TERMINÉ)
2. ⏸️ Validation utilisateur (EN COURS)
3. ⏳ Créer 11 projets .NET (Identity, Wallet, Reporting)
4. ⏳ Copier entités domaine
5. ⏳ Extraire use cases
6. ⏳ Implémenter controllers
7. ⏳ Configuration DI + HttpClient
8. ⏳ Dockerfiles (3)
9. ⏳ docker-compose.yml
10. ⏳ KrakenD routing
11. ⏳ Tests isolation + performance
12. ⏳ Documentation ADR-006

---

## 📄 Documents Créés

1. **`docs/microservices/BOUNDED-CONTEXTS-ANALYSIS.md`**
   - Analyse détaillée domaine actuel
   - Comparaison plan proposé vs réalité
   - Dépendances et couplage
   - Stratégie Strangler Pattern

2. **`docs/microservices/IMPLEMENTATION-PLAN.md`**
   - Structure projets complète
   - Dockerfiles templates
   - docker-compose.yml
   - KrakenD configuration
   - Tests détaillés
   - Checklist étape par étape

---

## ⏭️ Actions Requises

### AVANT de continuer:

1. ✅ **Lire** `docs/microservices/BOUNDED-CONTEXTS-ANALYSIS.md` (5 min)
2. ✅ **Valider** extraction Identity/Wallet/Reporting (PAS Orders)
3. ✅ **Décider** 3 questions critiques (Décision 1, 2, 3)
4. ✅ **Confirmer** GO pour implémentation

### APRÈS validation:

5. ⏳ Exécution checklist (10-13h)
6. ⏳ Tests
7. ⏳ Documentation

---

## 🚦 Status Actuel

**Phase 2b - Microservices:** ⏸️ **EN ATTENTE VALIDATION**

**Bloqueurs:**
- Aucune décision prise sur 3 questions critiques
- Pas de confirmation extraction Option A

**Prêt à démarrer dès validation ✅**

---

## 💬 Réponse Attendue

Répondez simplement:

**1. Extraction:** ✅ OK Identity/Wallet/Reporting OU ❌ Je veux vraiment Orders/Trading

**2. API Monolithe:** A (désactiver) OU B (conserver Strangler)

**3. Transaction Signup:** A (risque inconsistance accepté) OU B (Saga distribuée)

**4. Code Domaine:** A (copie bounded contexts) OU B (shared kernel)

**5. GO/NO-GO:** ✅ Commencer implémentation OU ⏸️ Plus de questions

---

**Prochaine étape:** Attente de votre réponse 🎯
