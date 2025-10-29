# 📋 Récapitulatif des Changements — UC-04 et UC-05

**Date:** 2025-10-28  
**Objectif:** Intégrer les nouveaux use cases UC-04 (Abonnement données marché) et UC-05 (Placement d'ordre) dans la documentation des vues 4+1.

---

## ✅ Fichiers Créés

### 1. **`uc04_abonnement_marche.puml`**
**Type:** Diagramme de séquence  
**Contenu:** 
- Flux complet abonnement WebSocket/SSE aux données de marché
- Acteurs: Client, API Gateway, Market Data Service, WebSocket Server, Rate Limiter, Market Data Provider
- Gestion quotas et rate limiting (100 msg/s)
- Streaming temps réel: quotes, trades, book snapshots
- Latence cible: < 200ms
- Protocoles: WebSocket, SSE, Long polling (fallback)

### 2. **`uc05_placement_ordre.puml`**
**Type:** Diagramme de séquence  
**Contenu:**
- Flux complet placement d'ordre avec contrôles pré-trade
- Acteurs: Client, API Gateway, Order Service, Pre-Trade Risk Engine, Account Service, Matching Engine, Position Service
- Validation 8 contrôles pré-trade:
  1. Pouvoir d'achat / marge
  2. Bandes de prix (± 5% last)
  3. Tick size (0.01 pour actions)
  4. Limites utilisateur
  5. Instrument actif
  6. Règles short-sell
  7. Notional max
  8. Sanity checks
- Scénarios: Match trouvé (FILLED) vs Pas de match (WORKING)
- Rejets: insufficient_buying_power, price_out_of_band, etc.
- Événements: OrderCreated, OrderFilled, OrderRejected

### 3. **`order_state_machine.puml`**
**Type:** Diagramme d'états  
**Contenu:**
- Machine à états complète pour un ordre de trading
- États: NEW → VALIDATING → ACCEPTED → WORKING → PARTIALLY_FILLED → FILLED
- États finaux: REJECTED, CANCELLED, EXPIRED
- Transitions avec conditions
- Notes sur chaque état (motifs rejet, modifications possibles, etc.)

### 4. **`architecture_uc04_uc05.puml`**
**Type:** Diagramme d'architecture C4  
**Contenu:**
- Vue architecture système avec nouveaux services:
  - Market Data Service (UC-04)
  - Order Service (UC-05)
  - Matching Engine
  - Position Service
- Relations avec Event Bus (RabbitMQ/Redis)
- Relations avec Fournisseur données marché externe
- Cache Redis pour market data (TTL 5s)
- Notes sur latence et performance

### 5. **`mdd_complete_uc01_05.puml`**
**Type:** Modèle du domaine (MDD)  
**Contenu:**
- Extension du modèle de domaine avec:
  - **Package "Ordres & Trading"**:
    - Ordre (orderId, symbol, side, type, quantity, price, timeInForce, statut)
    - Exécution (executionId, execPrice, execQuantity, commission)
    - Carnet d'Ordres (OrderBook)
  - **Package "Données de Marché"**:
    - Cotation (Quote: symbol, bid, ask, last, volume)
    - Souscription Marché (Subscription)
    - Snapshot Book (BookSnapshot)
  - Position (positionId, symbol, quantity, avgPrice, PnL)
- Mise à jour EcritureLedger.kind: ajout TRADE_FILL, FEE
- Associations:
  - Compte → Ordre (1:N)
  - Ordre → Execution (1:N)
  - Client → Subscription (1:N)
  - Execution → Position (met à jour)
  - Execution → Ledger (génère)

---

## ✏️ Fichiers Modifiés

### 1. **`contexte_metier.puml`**
**Changements:**
- Ajout branche "Abonner aux donnees de marche" dans le menu principal
- Flux complet UC-04:
  - Sélectionner symboles
  - Vérifier quotas + rate-limit
  - Ouvrir canal WebSocket/SSE
  - Recevoir cotations temps réel
  - Journaliser (market_data.subscribed / quota_exceeded)
  
- Ajout branche "Placer un ordre" dans le menu principal
- Flux complet UC-05:
  - Saisir ordre (symbole, sens, type, quantité, prix, durée)
  - Normaliser + horodater (UTC)
  - Exécuter contrôles pré-trade (8 validations)
  - Si OK: Attribuer OrderID, persister, router vers Matching Engine
  - Si match: Exécuter ordre, mettre à jour positions, générer ExecutionReport
  - Si pas de match: Ordre reste Working dans carnet
  - Si contrôles KO: Rejeter ordre avec motif

### 2. **`use_case.puml`**
**Changements:**
- Ajout acteurs externes:
  - "Fournisseur Données Marché" (MDP)
  - "Moteur Appariement" (ME)
  
- Ajout use cases:
  - UC-04 Abonnement Données Marché
  - UC-05 Placement Ordre
  
- Ajout use cases inclus:
  - "Contrôles Pré-Trade" (include dans UC-05)
  - "Vérification Quotas/Rate-Limit" (include dans UC-04)
  
- Relations:
  - Client → UC-04
  - Client → UC-05
  - MDP → UC-04 (fournit cotations)
  - ME → UC-05 (exécute matching)
  
- Notes:
  - UC-04: Latence cible < 200ms, WebSocket/SSE
  - UC-05: Latence ACK < 500ms (Phase 1)

---

## 🆕 Nouveaux Concepts Introduits

### Bounded Contexts Ajoutés

1. **Ordres & Trading**
   - Gestion cycle de vie ordres (NEW → FILLED/CANCELLED/EXPIRED)
   - Matching engine (Price-Time Priority)
   - Types ordres: MARKET, LIMIT
   - Time In Force: DAY, IOC, FOK, GTC
   - Contrôles pré-trade (8 validations)
   - Idempotence via clientOrderId

2. **Données de Marché**
   - Streaming temps réel (WebSocket/SSE)
   - Cotations: bid, ask, last, volume
   - Carnets d'ordres (Order Book snapshots)
   - Trades exécutés
   - Rate limiting (100 msg/s)
   - Quotas par client

3. **Positions**
   - Tracking quantité détenue par symbole
   - Calcul P&L (réalisé/non réalisé)
   - Mise à jour via exécutions

### Nouvelles Entités Métier

| Entité | Attributs Clés | Responsabilité |
|--------|---------------|----------------|
| **Ordre** | orderId, symbol, side, type, qty, price, timeInForce, statut | Représente intention trading client |
| **Exécution** | executionId, execPrice, execQuantity, commission | Enregistre fill partiel/total |
| **Position** | positionId, symbol, quantity, avgPrice, PnL | Agrège positions ouvertes |
| **Quote** | symbol, bid, ask, last, volume, timestamp | Snapshot cotation marché |
| **Subscription** | subscriptionId, symbols, canal, status | Abonnement client aux données |
| **OrderBook** | symbol, bids[], asks[], lastUpdateTime | Carnet d'ordres par instrument |

### Nouveaux Services Microservices

| Service | Port | Responsabilité | Dépendances |
|---------|------|----------------|-------------|
| **Market Data Service** | 8084 | UC-04: Abonnements, streaming WebSocket/SSE | Redis cache, Market Data Provider externe |
| **Order Service** | 8085 | UC-05: Placement, validation pré-trade | Account Service, Matching Engine |
| **Matching Engine** | 8086 | Appariement ordres (Price-Time Priority) | Order Service, Position Service |
| **Position Service** | 8087 | Gestion positions, calcul P&L | Matching Engine, Ledger |

---

## 📊 Impact Architecture

### Phase 2b → Phase 3 Transition

**Avant UC-04/UC-05 (Phase 2b):**
```
3 microservices:
- Identity Service (UC-01, UC-02)
- Wallet Service (UC-03)
- Reporting Service (analytics)
```

**Après UC-04/UC-05 (Phase 3):**
```
7 microservices:
- Identity Service (UC-01, UC-02)
- Wallet Service (UC-03)
- Market Data Service (UC-04) ← NOUVEAU
- Order Service (UC-05) ← NOUVEAU
- Matching Engine ← NOUVEAU
- Position Service ← NOUVEAU
- Reporting Service (analytics)

+ Event Bus (RabbitMQ/Redis) pour événements:
  - OrderCreated
  - OrderFilled
  - OrderRejected
  - PositionUpdated
  - MarketDataSnapshot
```

### Communication Inter-Services

**UC-04 (Market Data):**
- Externe → Market Data Service (streaming feed)
- Market Data Service → Redis Cache (quotes TTL 5s)
- Market Data Service → Client (WebSocket push)

**UC-05 (Orders):**
- Client → Order Service (REST POST /orders)
- Order Service → Account Service (vérif balance)
- Order Service → Matching Engine (route order)
- Matching Engine → Position Service (update on fill)
- Order Service → Event Bus (publish events)
- Event Bus → Client (WebSocket notifications)

---

## 🎯 Objectifs Non-Fonctionnels

### Performance (UC-04)

| Métrique | Phase 1 | Phase 2 | Phase 3 |
|----------|---------|---------|---------|
| **Latence market data** | N/A | < 500ms | < 200ms |
| **Throughput quotes** | N/A | 50 msg/s | 100 msg/s |
| **Connexions WebSocket** | N/A | 100 | 1000+ |

### Performance (UC-05)

| Métrique | Phase 1 | Phase 2 | Phase 3 |
|----------|---------|---------|---------|
| **Latence ordre ACK** | < 500ms | < 250ms | < 100ms |
| **Throughput ordres** | 300/s | 800/s | 1200/s |
| **Latence matching** | N/A | < 100ms | < 50ms |

### Disponibilité

| Service | SLA Cible |
|---------|-----------|
| Order Service | 99.9% (event-driven) |
| Market Data Service | 99.5% (peut tolérer dégradation) |
| Matching Engine | 99.99% (critique) |

---

## 📝 Documentation Complémentaire à Créer

### Technique

- [ ] **ADR-007:** Architecture événementielle (Event Bus, événements métier)
- [ ] **ADR-008:** Stratégie matching engine (Price-Time Priority, algorithmes)
- [ ] **ADR-009:** Streaming market data (WebSocket vs SSE, backpressure)
- [ ] **Guide WebSocket:** Implémentation SignalR/ASP.NET Core
- [ ] **Guide Matching Engine:** Algorithmes appariement, tests performance
- [ ] **Entités C#:** Order.cs, Execution.cs, Position.cs, Quote.cs

### Tests

- [ ] **UC-04 Tests E2E:**
  - Abonnement WebSocket succès
  - Rate limiting (quota dépassé → 429)
  - Déconnexion propre
  - Latence < 200ms

- [ ] **UC-05 Tests E2E:**
  - Placement ordre marché (match immédiat)
  - Placement ordre limite (reste dans carnet)
  - Rejet: pouvoir d'achat insuffisant
  - Rejet: prix hors bandes
  - Idempotence (même clientOrderId → même résultat)
  - Latence ACK < 500ms

- [ ] **Tests Performance k6:**
  - `scripts/k6/market_data_subscribe.js` (1000 clients WebSocket)
  - `scripts/k6/place_order.js` (1200 ordres/s)
  - `scripts/k6/matching_engine_stress.js`

### Opérations

- [ ] **RUNBOOK:** Ajouter sections UC-04/UC-05
  - Démarrer Market Data Service
  - Démarrer Matching Engine
  - Tester WebSocket
  - Tester placement ordre
- [ ] **Monitoring Grafana:** Dashboards UC-04/UC-05
  - Market data latency
  - WebSocket connections actives
  - Order placement rate
  - Matching engine throughput
  - Order book depth

---

## ✅ Validation

### Checklist Complétude

- [x] Diagramme séquence UC-04 créé
- [x] Diagramme séquence UC-05 créé
- [x] Diagramme états Ordre créé
- [x] Architecture mise à jour
- [x] Modèle domaine étendu (MDD complet)
- [x] Contexte métier mis à jour
- [x] Use case diagram mis à jour
- [x] Nouveaux bounded contexts identifiés
- [ ] Implémentation C# (entités Domain)
- [ ] Implémentation services (Market Data, Order)
- [ ] Tests E2E UC-04/UC-05
- [ ] Documentation ADR-007, ADR-008, ADR-009

---

**Status:** 📄 Documentation vues 4+1 complétée pour UC-04 et UC-05  
**Next Steps:** Implémentation Domain models + Services + Tests E2E

---

**Fichiers créés/modifiés:**
1. ✅ `docs/views/contexte_metier.puml` (modifié)
2. ✅ `docs/views/use_case.puml` (modifié)
3. ✅ `docs/views/uc04_abonnement_marche.puml` (créé)
4. ✅ `docs/views/uc05_placement_ordre.puml` (créé)
5. ✅ `docs/views/order_state_machine.puml` (créé)
6. ✅ `docs/views/architecture_uc04_uc05.puml` (créé)
7. ✅ `docs/views/mdd_complete_uc01_05.puml` (créé)
