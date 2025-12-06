# UC-05 : Placement d'Ordres - Implémentation Complète

**Date:** 5 décembre 2025  
**Version:** 1.0  
**Statut:** ✅ IMPLÉMENTÉ - Build réussi

## 📋 Table des Matières

1. [Vue d'Ensemble](#vue-densemble)
2. [Architecture Hexagonale](#architecture-hexagonale)
3. [Composants Implémentés](#composants-implémentés)
4. [Contrôles Pré-Trade](#contrôles-pré-trade)
5. [Flux de Données](#flux-de-données)
6. [API Endpoints](#api-endpoints)
7. [Dépendances et Packages](#dépendances-et-packages)
8. [Tests et Validation](#tests-et-validation)
9. [Codes d'Erreur](#codes-derreur)
10. [Prochaines Étapes](#prochaines-étapes)

---

## 🎯 Vue d'Ensemble

### Objectif
Implémenter UC-05 (Placement d'ordres marché/limite) avec contrôles pré-trade complets en respectant strictement l'architecture hexagonale du projet.

### Fonctionnalités Principales
- ✅ Placement d'ordres d'achat/vente (Market/Limit)
- ✅ Contrôles pré-trade (5 validations)
- ✅ Idempotence via `clientOrderId`
- ✅ Consultation d'ordres
- ✅ Annulation d'ordres
- ✅ Simulation de matching engine

---

## 🏗️ Architecture Hexagonale

### Flux Complet
```
HTTP Request (Client)
    ↓
OrderController (Infrastructure.Web) ━━━ Point d'entrée HTTP
    ↓
IOrderUseCase (Domain Port Inbound) ━━━━ Interface métier
    ↓
OrderService (Application) ━━━━━━━━━━━━ Orchestration
    ↓
Ordre (Domain Model) ━━━━━━━━━━━━━━━━━ Logique métier pure
    ↓
IPreTradeCheckPort (Domain Port Outbound) ━ Contrôles
IOrderMatchingPort (Domain Port Outbound) ━ Matching
IOrderRepository (Domain Port Outbound) ━━━ Persistance
    ↓
PreTradeCheckAdapter (Infrastructure.Adapters)
OrderMatchingSimulator (Infrastructure.Adapters)
OrderRepository (Infrastructure.Persistence)
```

### Respect des Principes
- ✅ **Séparation des préoccupations** : Domain, Application, Infrastructure
- ✅ **Inversion de dépendances** : Ports définis dans Domain
- ✅ **Testabilité** : Adapters interchangeables
- ✅ **Pas de dépendances techniques dans le Domain**

---

## 📦 Composants Implémentés

### 1. Domain Layer

#### Model (`src/Domain/Model/Trading/Ordre.cs`)
```csharp
public sealed class Ordre  // Agrégat racine
{
    // Factory Method
    public static Ordre Creer(params...)
    
    // Méthodes métier
    public void Accepter()
    public void PlacerDansCarnet()
    public void Rejeter(string raison)
    public void ExecuterPartiellement(decimal price, decimal qty)
    public void Annuler()
    public bool EstTerminal()
    public decimal ObtenirQuantiteRestante()
}

// Value Object
public sealed class Execution
{
    public Guid ExecutionId { get; }
    public decimal Price { get; }
    public decimal Quantity { get; }
    public decimal Commission { get; }
    public DateTime Timestamp { get; }
}

// Enums
public enum SensOrdre { Buy, Sell }
public enum TypeOrdre { Market, Limit }
public enum DureeValidite { DAY, IOC, FOK, GTC }
public enum StatutOrdre { 
    New, Validating, Accepted, Working, 
    PartiallyFilled, Filled, Cancelled, 
    Rejected, Expired 
}
```

#### Contracts (`src/Domain/Contracts/OrderOperationResult.cs`)
```csharp
// Result pour placement/annulation
public sealed class OrderOperationResult
{
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }
    public Guid? OrderId { get; init; }
    public string? ClientOrderId { get; init; }
    public string? OrderStatus { get; init; }
}

// Result pour consultation
public sealed class OrderQueryResult
{
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }
    public OrderDto? Order { get; init; }
}

// DTOs
public sealed record OrderDto { /* 15 propriétés */ }
public sealed record ExecutionDto { /* 5 propriétés */ }
```

#### Ports Inbound (`src/Domain/Ports.Inbound/IOrderUseCase.cs`)
```csharp
public interface IOrderUseCase
{
    Task<OrderOperationResult> PlaceOrderAsync(
        Guid accountId, string clientOrderId, string symbol,
        string side, string type, decimal quantity, 
        decimal? price, string timeInForce, CancellationToken ct);
    
    Task<OrderQueryResult> GetOrderAsync(Guid orderId, CancellationToken ct);
    
    Task<OrderOperationResult> CancelOrderAsync(Guid orderId, CancellationToken ct);
}
```

#### Ports Outbound (`src/Domain/Ports.Outbound/`)

**IOrderRepository.cs** - Persistance
```csharp
public interface IOrderRepository
{
    Task<Ordre?> GetByIdAsync(Guid orderId, CancellationToken ct);
    Task<Ordre?> GetByClientOrderIdAsync(string clientOrderId, CancellationToken ct);
    Task<List<Ordre>> GetByAccountIdAsync(Guid accountId, CancellationToken ct);
    Task AddAsync(Ordre ordre, CancellationToken ct);
    Task UpdateAsync(Ordre ordre, CancellationToken ct);
    Task<int> SaveChangesAsync(CancellationToken ct);
}
```

**ICompteRepository.cs** - Accès aux comptes
```csharp
public interface ICompteRepository
{
    Task<Compte?> GetByIdAsync(Guid accountId, CancellationToken ct);
    Task<List<Compte>> GetByClientIdAsync(Guid clientId, CancellationToken ct);
}
```

**IPreTradeCheckPort.cs** - Contrôles pré-trade
```csharp
public interface IPreTradeCheckPort
{
    Task<PreTradeCheckResult> CheckInstrumentStatusAsync(string symbol, CancellationToken ct);
    Task<PreTradeCheckResult> CheckPriceBandsAsync(string symbol, decimal? limitPrice, TypeOrdre type, CancellationToken ct);
    Task<PreTradeCheckResult> CheckBuyingPowerAsync(Guid accountId, string symbol, decimal quantity, decimal? limitPrice, TypeOrdre type, SensOrdre side, CancellationToken ct);
    Task<PreTradeCheckResult> CheckShortSellAsync(Guid accountId, string symbol, decimal quantity, CancellationToken ct);
    Task<PreTradeCheckResult> CheckTradingLimitsAsync(string symbol, decimal quantity, decimal? limitPrice, TypeOrdre type, CancellationToken ct);
}
```

**IOrderMatchingPort.cs** - Soumission au moteur
```csharp
public interface IOrderMatchingPort
{
    Task SubmitOrderAsync(Ordre ordre, CancellationToken ct);
}
```

### 2. Application Layer

#### OrderService (`src/Application/Services/OrderService.cs`)
**Responsabilités:**
- Orchestration du workflow de placement d'ordres
- Validation des paramètres
- Idempotence (cache Redis + DB check)
- Contrôles pré-trade (5 validations)
- Création de l'ordre (Domain Model)
- Persistance
- Soumission au matching engine
- Audit logging

**Méthode Principale: PlaceOrderAsync()**
```csharp
1. Validation de base (symbol, clientOrderId, quantity)
2. Idempotence check (Redis cache)
3. Vérification compte existant
4. Parsing des enums (side/type/timeInForce)
5. Contrôles pré-trade:
   - CheckInstrumentStatus
   - CheckPriceBands
   - CheckBuyingPower
   - CheckShortSell (si Sell)
   - CheckTradingLimits
6. Création de l'ordre (Ordre.Creer)
7. Acceptation (ordre.Accepter)
8. Persistance (repository)
9. Placement dans carnet (ordre.PlacerDansCarnet)
10. Soumission au matching engine
11. Update en DB
12. Cache pour idempotence (24h TTL)
13. Audit log
14. Return success result
```

### 3. Infrastructure Layer

#### Adapters

**PreTradeCheckAdapter** (`src/Infrastructure.Adapters/PreTrade/PreTradeCheckAdapter.cs`)
```csharp
public sealed class PreTradeCheckAdapter : IPreTradeCheckPort
{
    // Implémentation des 5 contrôles:
    
    1. CheckInstrumentStatusAsync()
       - Vérifie que le symbole est actif
       - Liste de symboles valides: AAPL, TSLA, GOOGL, AMZN, MSFT
    
    2. CheckPriceBandsAsync()
       - Vérifie prix dans bandes +/- 10% du dernier prix
       - Validate tick size (0.01)
       - Market orders: skip price check
    
    3. CheckBuyingPowerAsync()
       - Récupère le portefeuille (cash balance)
       - Calcule notional required
       - Market orders: +5% marge de sécurité
       - Vérifie cash disponible >= notional
    
    4. CheckShortSellAsync()
       - Simplifié: toujours OK (pas de positions tracking)
       - Future: vérifier position existante
    
    5. CheckTradingLimitsAsync()
       - Max notional per order: $1,000,000
       - Max position size: 10,000 shares
}
```

**OrderMatchingSimulator** (`src/Infrastructure.Adapters/OrderMatching/OrderMatchingSimulator.cs`)
```csharp
public sealed class OrderMatchingSimulator : IOrderMatchingPort
{
    // Simulateur de carnet d'ordres en mémoire
    
    private class OrderBook
    {
        public SortedDictionary<decimal, List<Ordre>> Bids { get; }  // Prix DESC
        public SortedDictionary<decimal, List<Ordre>> Asks { get; }  // Prix ASC
    }
    
    public async Task SubmitOrderAsync(Ordre ordre, CancellationToken ct)
    {
        // 1. Récupérer/créer carnet pour le symbole
        // 2. Ajouter l'ordre dans le carnet (bids/asks)
        // 3. Log placement
        // Future: Matching logic avec exécutions partielles
    }
}
```

#### Persistence

**OrderRepository** (`src/Infrastructure.Persistence/Repositories/OrderRepository.cs`)
```csharp
public sealed class OrderRepository : IOrderRepository
{
    private readonly BrokerXDbContext _context;
    
    // Implémentation EF Core pour:
    // - GetByIdAsync
    // - GetByClientOrderIdAsync
    // - GetByAccountIdAsync
    // - AddAsync
    // - UpdateAsync
    // - SaveChangesAsync
}
```

**BrokerXDbContext** (modifié: `src/Infrastructure.Persistence/Repositories/BrokerXDbContext.cs`)
```csharp
public DbSet<Ordre> Ordres => Set<Ordre>();
public DbSet<Execution> Executions => Set<Execution>();

protected override void OnModelCreating(ModelBuilder mb)
{
    // Configuration Ordre
    mb.Entity<Ordre>(entity =>
    {
        entity.HasKey(o => o.OrderId);
        entity.HasIndex(o => o.ClientOrderId).IsUnique();
        entity.HasIndex(o => o.AccountId);
        entity.HasIndex(o => o.Symbol);
        entity.HasIndex(o => o.CreatedAt);
        
        // Enums as strings
        entity.Property(o => o.Side)
              .HasConversion<string>();
        // ... autres conversions
        
        // Relation 1-to-many avec Executions
        entity.HasMany<Execution>()
              .WithOne()
              .HasForeignKey("OrderId");
    });
    
    // Configuration Execution
    mb.Entity<Execution>(entity =>
    {
        entity.HasKey(e => e.ExecutionId);
        entity.HasIndex(e => new { e.OrderId, e.Timestamp });
    });
}
```

**InMemoryAccountRepository** (modifié pour supporter ICompteRepository)
```csharp
public sealed class InMemoryAccountRepository : IAccountRepository, ICompteRepository
{
    // Implémente les deux interfaces pour réutiliser le repository existant
}
```

#### Web (API Layer)

**OrderController** (`src/Infrastructure.Web/Controllers/OrderController.cs`)
```csharp
[ApiController]
[Route("api/v1/orders")]
public sealed class OrderController : ControllerBase
{
    [HttpPost("{accountId}")]
    public async Task<ActionResult<OrderResponseDto>> PlaceOrder(
        [FromRoute] Guid accountId,
        [FromBody] PlaceOrderRequestDto dto,
        CancellationToken ct)
    
    [HttpGet("{accountId}/orders/{orderId}")]
    public async Task<ActionResult<OrderResponseDto>> GetOrder(
        [FromRoute] Guid accountId,
        [FromRoute] Guid orderId,
        CancellationToken ct)
    
    [HttpDelete("{accountId}/orders/{orderId}")]
    public async Task<ActionResult<OrderResponseDto>> CancelOrder(
        [FromRoute] Guid accountId,
        [FromRoute] Guid orderId,
        CancellationToken ct)
}
```

**DTOs** (`src/Infrastructure.Web/DTOs/`)

*PlaceOrderRequestDto.cs*
```csharp
public sealed class PlaceOrderRequestDto
{
    [Required] public required string ClientOrderId { get; init; }
    [Required] public required string Symbol { get; init; }
    [Required] public required string Side { get; init; }  // "Buy" / "Sell"
    [Required] public required string Type { get; init; }  // "Market" / "Limit"
    [Required] public required int Quantity { get; init; }
    public decimal? Price { get; init; }
    public string? TimeInForce { get; init; }  // "DAY" (default)
}
```

*OrderResponseDto.cs*
```csharp
public sealed class OrderResponseDto
{
    public required Guid OrderId { get; init; }
    public required string ClientOrderId { get; init; }
    public required Guid AccountId { get; init; }
    public required string Symbol { get; init; }
    public required string Side { get; init; }
    public required string Type { get; init; }
    public required int Quantity { get; init; }
    public decimal? Price { get; init; }
    public required string TimeInForce { get; init; }
    public required string Status { get; init; }
    public int FilledQuantity { get; init; }
    public decimal? AveragePrice { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string? RejectionReason { get; init; }
    public List<ExecutionResponseDto>? Executions { get; init; }
}
```

#### Dependency Injection (Program.cs)
```csharp
// UC-05 Order Management
builder.Services.AddScoped<IOrderUseCase, OrderService>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<ICompteRepository>(sp => 
    sp.GetRequiredService<IAccountRepository>() as InMemoryAccountRepository 
    ?? throw new InvalidOperationException("IAccountRepository must implement ICompteRepository"));
builder.Services.AddScoped<IPreTradeCheckPort, PreTradeCheckAdapter>();
builder.Services.AddSingleton<IOrderMatchingPort, OrderMatchingSimulator>();
```

---

## 🔒 Contrôles Pré-Trade

### 1. Instrument Status
**Objectif:** Vérifier que l'instrument est actif et tradable  
**Implémentation:** Liste de symboles valides  
**Erreur:** `INSTRUMENT_NOT_ACTIVE`

### 2. Price Bands
**Objectif:** Prévenir les ordres à prix aberrants  
**Règles:**
- Prix limite doit être dans ±10% du dernier prix
- Tick size: 0.01
- Market orders: skip cette validation

**Erreurs:** 
- `PRICE_OUT_OF_BANDS`
- `INVALID_TICK_SIZE`

### 3. Buying Power
**Objectif:** Vérifier le solde disponible  
**Calcul:**
```
notional = quantity × price (ou lastPrice si Market)
Market orders: notional × 1.05 (marge 5%)
Vérification: cashBalance >= notional
```
**Erreur:** `INSUFFICIENT_FUNDS`

### 4. Short-Sell
**Objectif:** Vérifier position existante avant vente à découvert  
**Implémentation actuelle:** Simplifié (toujours OK)  
**Future:** Vérifier position >= quantity  
**Erreur:** `SHORT_SELL_NOT_ALLOWED`

### 5. Trading Limits
**Objectif:** Limites réglementaires  
**Règles:**
- Max notional per order: $1,000,000
- Max position size: 10,000 shares

**Erreur:** `MAX_NOTIONAL_EXCEEDED`

---

## 🔄 Flux de Données

### Placement d'Ordre (Succès)
```
1. Client → POST /api/v1/orders/{accountId}
2. OrderController → IOrderUseCase.PlaceOrderAsync()
3. OrderService → Validation (symbol, clientOrderId, quantity)
4. OrderService → Cache check (Redis) - idempotence
5. OrderService → ICompteRepository.GetByIdAsync() - vérifier compte
6. OrderService → IPreTradeCheckPort.CheckInstrumentStatusAsync()
7. OrderService → IPreTradeCheckPort.CheckPriceBandsAsync()
8. OrderService → IPreTradeCheckPort.CheckBuyingPowerAsync()
9. OrderService → IPreTradeCheckPort.CheckShortSellAsync() (si Sell)
10. OrderService → IPreTradeCheckPort.CheckTradingLimitsAsync()
11. OrderService → Ordre.Creer() + Accepter()
12. OrderService → IOrderRepository.AddAsync()
13. OrderService → ordre.PlacerDansCarnet()
14. OrderService → IOrderMatchingPort.SubmitOrderAsync()
15. OrderService → IOrderRepository.UpdateAsync()
16. OrderService → Cache set (Redis, 24h TTL)
17. OrderService → IAuditPort.LogAsync()
18. OrderService → Return OrderOperationResult.Ok()
19. OrderController → Return 200 OK + OrderResponseDto
```

### Placement d'Ordre (Échec - Insufficient Funds)
```
1-8. [Même flow jusqu'au CheckBuyingPowerAsync]
9. PreTradeCheckAdapter → cashBalance < notional
10. PreTradeCheckAdapter → Return PreTradeCheckResult.Fail("INSUFFICIENT_FUNDS")
11. OrderService → Return OrderOperationResult.Fail("INSUFFICIENT_FUNDS", message)
12. OrderController → Return 409 Conflict { error, message }
```

### Idempotence (Ordre déjà placé)
```
1-4. [Même flow jusqu'au cache check]
5. OrderService → Cache hit! (clientOrderId existe)
6. OrderService → IOrderRepository.GetByClientOrderIdAsync()
7. OrderService → Return OrderOperationResult.Fail("ORDER_ALREADY_EXISTS", orderId)
8. OrderController → Return 409 Conflict { error, message, orderId }
```

---

## 🌐 API Endpoints

### POST /api/v1/orders/{accountId}
**Description:** Placer un nouvel ordre  
**Auth:** Requis (Bearer token)

**Request:**
```json
{
  "clientOrderId": "ORDER-20251205-001",
  "symbol": "AAPL",
  "side": "Buy",
  "type": "Limit",
  "quantity": 100,
  "price": 195.50,
  "timeInForce": "DAY"
}
```

**Response 200 OK:**
```json
{
  "orderId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "clientOrderId": "ORDER-20251205-001",
  "accountId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "symbol": "AAPL",
  "side": "Buy",
  "type": "Limit",
  "quantity": 100,
  "price": 195.50,
  "timeInForce": "DAY",
  "status": "Working",
  "filledQuantity": 0,
  "averagePrice": null,
  "createdAt": "2025-12-05T14:30:00Z",
  "executions": []
}
```

**Response 400 Bad Request:**
```json
{
  "error": "INVALID_SYMBOL",
  "message": "Symbol 'XYZ' is not valid"
}
```

**Response 409 Conflict:**
```json
{
  "error": "INSUFFICIENT_FUNDS",
  "message": "Insufficient buying power. Required: $19550.00, Available: $10000.00"
}
```

### GET /api/v1/orders/{accountId}/orders/{orderId}
**Description:** Récupérer un ordre  
**Auth:** Requis

**Response 200 OK:** (même structure que POST)

**Response 404 Not Found:**
```json
{
  "error": "ORDER_NOT_FOUND",
  "message": "Order not found"
}
```

### DELETE /api/v1/orders/{accountId}/orders/{orderId}
**Description:** Annuler un ordre  
**Auth:** Requis

**Response 200 OK:**
```json
{
  "orderId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "clientOrderId": "ORDER-20251205-001",
  "status": "Cancelled",
  "...": "..."
}
```

**Response 409 Conflict:**
```json
{
  "error": "ORDER_TERMINAL",
  "message": "Cannot cancel order in terminal state: Filled"
}
```

---

## 📦 Dépendances et Packages

### Packages Utilisés (UC-05)

**Domain Layer:**
- ✅ Aucune dépendance externe (Clean Architecture)

**Application Layer:**
- ✅ `Serilog` (4.3.0) - Logging structuré
- ✅ `Microsoft.Extensions.Logging.Abstractions` (9.0.0)
- ✅ `System.Diagnostics.DiagnosticSource` (9.0.0)

**Infrastructure.Adapters:**
- ✅ `StackExchange.Redis` (2.8.16) - Cache idempotence
- ✅ `Serilog` (4.1.0)
- ✅ `Microsoft.AspNetCore.SignalR` (1.1.0)

**Infrastructure.Persistence:**
- ✅ `Microsoft.EntityFrameworkCore` (9.0.9)
- ✅ `Microsoft.EntityFrameworkCore.InMemory` (9.0.9) - Tests
- ✅ `Pomelo.EntityFrameworkCore.MySql` (9.0.0) - Production

**Infrastructure.Web:**
- ✅ `Microsoft.AspNetCore.OpenApi` (9.0.9)
- ✅ `Swashbuckle.AspNetCore` (9.0.4) - Swagger/OpenAPI
- ✅ `Serilog.AspNetCore` (9.0.0)
- ✅ `System.ComponentModel.Annotations` (implicite) - [Required]

### Références de Projets
```
Infrastructure.Web
  ├─ Application
  │   └─ Domain
  ├─ Infrastructure.Adapters
  │   └─ Domain
  └─ Infrastructure.Persistence
      └─ Domain
```

### Vérification Build
```bash
cd /home/pop28/Documents/github/projet-log-430
dotnet clean
dotnet build

# Résultat: ✅ Build succeeded with 2 warning(s) in 9.6s
# Warnings: Non-bloquants (unused field, nullable reference)
```

---

## 🧪 Tests et Validation

### Tests Unitaires à Créer

**1. OrderServiceTests** (`tests/Application.Tests/OrderServiceTests.cs`)
```csharp
[Fact] public async Task PlaceOrder_ValidMarketBuy_Success()
[Fact] public async Task PlaceOrder_InvalidSymbol_ReturnsError()
[Fact] public async Task PlaceOrder_InsufficientFunds_ReturnsError()
[Fact] public async Task PlaceOrder_Idempotence_ReturnsCachedOrder()
[Fact] public async Task PlaceOrder_LimitOutOfBands_ReturnsError()
[Fact] public async Task PlaceOrder_ShortSellNotAllowed_ReturnsError()
[Fact] public async Task CancelOrder_Success()
[Fact] public async Task CancelOrder_AlreadyFilled_ReturnsError()
```

**2. PreTradeCheckAdapterTests** (`tests/Infrastructure.Tests/PreTradeCheckAdapterTests.cs`)
```csharp
[Fact] public async Task CheckBuyingPower_SufficientFunds_Success()
[Fact] public async Task CheckBuyingPower_InsufficientFunds_Fail()
[Fact] public async Task CheckPriceBands_WithinBands_Success()
[Fact] public async Task CheckPriceBands_OutsideBands_Fail()
[Fact] public async Task CheckInstrumentStatus_ValidSymbol_Success()
```

**3. OrderMatchingSimulatorTests**
```csharp
[Fact] public async Task SubmitOrder_AddsToOrderBook()
[Fact] public async Task SubmitOrder_BidAskSeparation()
```

### Tests d'Intégration

**Test UC-05 E2E** (à créer dans `tests/E2E.Tests/`)
```csharp
1. Signup → Deposit → PlaceOrder → GetOrder → CancelOrder
2. Vérifier idempotence (double submit)
3. Tester tous les codes d'erreur
4. Tester Market vs Limit orders
```

### Tests Manuels (API)

**Test 1: Placement Ordre Limit Buy**
```bash
# 1. Signup
POST http://localhost:8080/api/v1/auth/signup
{
  "email": "trader@test.com",
  "password": "Test1234!",
  "firstName": "John",
  "lastName": "Trader"
}

# 2. Deposit
POST http://localhost:8080/api/v1/wallet/deposit
{
  "accountId": "{accountId}",
  "amount": 50000,
  "currency": "USD"
}

# 3. Place Order
POST http://localhost:8080/api/v1/orders/{accountId}
{
  "clientOrderId": "ORDER-TEST-001",
  "symbol": "AAPL",
  "side": "Buy",
  "type": "Limit",
  "quantity": 100,
  "price": 195.50,
  "timeInForce": "DAY"
}

# 4. Get Order
GET http://localhost:8080/api/v1/orders/{accountId}/orders/{orderId}

# 5. Cancel Order
DELETE http://localhost:8080/api/v1/orders/{accountId}/orders/{orderId}
```

**Test 2: Idempotence**
```bash
# Submit same order twice with same clientOrderId
POST .../orders/{accountId} (1st time) → 200 OK
POST .../orders/{accountId} (2nd time) → 409 Conflict (ORDER_ALREADY_EXISTS)
```

**Test 3: Insufficient Funds**
```bash
# Deposit: $1000
# Try to buy: 100 AAPL @ $195.50 = $19,550
→ 409 Conflict (INSUFFICIENT_FUNDS)
```

---

## ❌ Codes d'Erreur

### Validation Errors (400 Bad Request)
| Code | Description |
|------|-------------|
| `INVALID_SYMBOL` | Symbole invalide ou vide |
| `INVALID_CLIENT_ORDER_ID` | ClientOrderId invalide ou vide |
| `INVALID_QUANTITY` | Quantité ≤ 0 |
| `INVALID_SIDE` | Side != "Buy" \| "Sell" |
| `INVALID_TYPE` | Type != "Market" \| "Limit" |
| `INVALID_TIME_IN_FORCE` | TimeInForce invalide |
| `PRICE_REQUIRED` | Prix requis pour Limit order |
| `INVALID_TICK_SIZE` | Prix ne respecte pas le tick size (0.01) |

### Business Logic Errors (409 Conflict)
| Code | Description |
|------|-------------|
| `ACCOUNT_NOT_FOUND` | Compte inexistant |
| `INSTRUMENT_NOT_ACTIVE` | Instrument non actif |
| `PRICE_OUT_OF_BANDS` | Prix hors des bandes ±10% |
| `INSUFFICIENT_FUNDS` | Pouvoir d'achat insuffisant |
| `SHORT_SELL_NOT_ALLOWED` | Vente à découvert sans position |
| `MAX_NOTIONAL_EXCEEDED` | Notional > $1M |
| `ORDER_ALREADY_EXISTS` | Idempotence: ordre déjà placé |
| `ORDER_TERMINAL` | Ordre dans état terminal (Filled/Cancelled/Rejected) |

### Not Found Errors (404 Not Found)
| Code | Description |
|------|-------------|
| `ORDER_NOT_FOUND` | Ordre inexistant |

### Internal Errors (500 Internal Server Error)
| Code | Description |
|------|-------------|
| `INTERNAL_ERROR` | Erreur système inattendue |

---

## 🚀 Prochaines Étapes

### Phase 1: Tests Unitaires (Priorité HAUTE)
- [ ] Créer `OrderServiceTests.cs` avec 10+ tests
- [ ] Créer `PreTradeCheckAdapterTests.cs` avec 5+ tests
- [ ] Créer `OrderRepositoryTests.cs` avec 5+ tests
- [ ] Coverage cible: >80%

### Phase 2: Tests d'Intégration
- [ ] Créer `UC05_E2E_Tests.cs`
- [ ] Test workflow complet (Signup → Deposit → Order → Cancel)
- [ ] Test idempotence (Redis + DB)
- [ ] Test tous les codes d'erreur

### Phase 3: Matching Engine (Simulation)
- [ ] Implémenter matching logic dans `OrderMatchingSimulator`
- [ ] Price-time priority
- [ ] Exécutions partielles
- [ ] Market orders vs Limit orders
- [ ] Time-in-force (IOC/FOK)

### Phase 4: WebSocket (Ordre Updates)
- [ ] Créer `OrderUpdateHub` (SignalR)
- [ ] Publier events: OrderAccepted, OrderFilled, OrderCancelled
- [ ] Client subscription par accountId

### Phase 5: Migration Base de Données
- [ ] Créer migration EF Core pour tables `Ordre` et `Execution`
- [ ] Script de seed pour données de test
- [ ] Basculer de InMemory vers MySQL

### Phase 6: Documentation API
- [ ] Enrichir Swagger annotations
- [ ] Créer guide utilisateur UC-05
- [ ] Exemples curl/Postman
- [ ] Diagrammes de séquence

### Phase 7: Performance & Observabilité
- [ ] Tests de charge K6 (100+ req/s)
- [ ] Métriques Prometheus (orders_placed_total, pre_trade_check_duration)
- [ ] Dashboard Grafana
- [ ] Logs structurés (Serilog)

### Phase 8: Sécurité
- [ ] Rate limiting (ordres/minute par compte)
- [ ] Authorization (JWT + AccountId ownership)
- [ ] Audit trail complet
- [ ] OWASP compliance

---

## 📊 Métriques de Succès

### Build & Compilation
- ✅ Build succeeded: 9.6s
- ✅ Warnings: 2 (non-bloquants)
- ✅ Errors: 0

### Architecture
- ✅ Séparation Domain/Application/Infrastructure: 100%
- ✅ Ports définis dans Domain: 100%
- ✅ Pas de dépendances techniques dans Domain: ✅
- ✅ Testabilité (mocks possibles): ✅

### Fonctionnalités
- ✅ Placement ordres: Implémenté
- ✅ Contrôles pré-trade (5): Implémentés
- ✅ Idempotence: Implémenté (Redis + DB)
- ✅ Consultation ordres: Implémenté
- ✅ Annulation ordres: Implémenté
- ⏳ Matching engine: Simulé (basic)
- ⏳ Exécutions: Structure prête

### Tests
- ⏳ Tests unitaires: 0% (à créer)
- ⏳ Tests intégration: 0% (à créer)
- ⏳ Tests E2E: 0% (à créer)

---

## 📝 Notes Techniques

### Idempotence Strategy
- **TTL Redis:** 24 heures
- **Key format:** `order:clientOrderId:{value}`
- **Value:** `orderId` (UUID)
- **Fallback:** DB check si cache miss

### Price Bands Algorithm
```csharp
lastPrice = GetLastPrice(symbol);  // Ex: $200.00
lowerBand = lastPrice * 0.90;      // $180.00
upperBand = lastPrice * 1.10;      // $220.00

if (limitPrice < lowerBand || limitPrice > upperBand)
    return Fail("PRICE_OUT_OF_BANDS");
```

### Buying Power Calculation
```csharp
// Limit order
notional = quantity * limitPrice;

// Market order (5% safety margin)
estimatedPrice = GetLastPrice(symbol);
notional = quantity * estimatedPrice * 1.05;

cashBalance = portfolio.CashBalance;
if (cashBalance < notional)
    return Fail("INSUFFICIENT_FUNDS");
```

### EF Core Enum Storage
```csharp
// Stored as VARCHAR in DB (not INT) for readability
entity.Property(o => o.Side)
      .HasConversion<string>();
      
// DB values: "Buy", "Sell" (not 0, 1)
```

---

## 🔗 Références

### Documentation Projet
- `docs/views/use_case.puml` - Diagramme UC-05
- `docs/views/mdd.puml` - Modèle de domaine
- `docs/adr/ADR-001-Architecture-hexagonale.md`

### Code Existant (Patterns à suivre)
- `src/Application/Services/AuthService.cs` - Pattern orchestration
- `src/Infrastructure.Web/Controllers/AuthController.cs` - Pattern controller
- `src/Domain/Model/Identite/Client.cs` - Pattern agrégat

### Standards
- C# 11 (.NET 9.0)
- REST API (RESTful)
- OpenAPI 3.0 (Swagger)
- JSON (snake_case pour enums)

---

## ✅ Checklist Validation

### Domain Layer
- [x] Agrégat `Ordre` avec logique métier
- [x] Value Object `Execution`
- [x] Enums (SensOrdre, TypeOrdre, StatutOrdre, DureeValidite)
- [x] Result objects (OrderOperationResult, OrderQueryResult)
- [x] DTOs (OrderDto, ExecutionDto)
- [x] Port Inbound (IOrderUseCase)
- [x] Ports Outbound (IOrderRepository, ICompteRepository, IPreTradeCheckPort, IOrderMatchingPort)

### Application Layer
- [x] OrderService implémente IOrderUseCase
- [x] PlaceOrderAsync avec workflow complet
- [x] GetOrderAsync avec mapping DTO
- [x] CancelOrderAsync avec vérification terminal state
- [x] Logging structuré (Serilog)
- [x] Idempotence (Redis cache)

### Infrastructure Layer
- [x] PreTradeCheckAdapter (5 contrôles)
- [x] OrderMatchingSimulator (carnet d'ordres basique)
- [x] OrderRepository (EF Core)
- [x] BrokerXDbContext updated (Ordre + Execution entities)
- [x] InMemoryAccountRepository implements ICompteRepository
- [x] OrderController (3 endpoints REST)
- [x] DTOs (PlaceOrderRequestDto, OrderResponseDto, ExecutionResponseDto)
- [x] Program.cs DI registration

### Build & Compilation
- [x] Tous les fichiers UC-05 compilent sans erreur
- [x] Dépendances NuGet résolues
- [x] Références de projets valides
- [x] dotnet build succeeded

---

**Fin du document UC-05 Implementation Complete**  
**Version:** 1.0  
**Date:** 5 décembre 2025  
**Auteur:** GitHub Copilot + Human Collaboration  
**Statut:** ✅ READY FOR TESTING
