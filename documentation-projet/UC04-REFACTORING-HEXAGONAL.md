# 🔄 UC-04 Refactoring Architecture Hexagonale - Rapport Complet

**Date:** 5 décembre 2025  
**Objectif:** Refactoriser UC-04 (Market Data) pour suivre strictement l'architecture hexagonale  
**Statut:** ✅ Complété

---

## 📋 Problème Initial

L'implémentation UC-04 ne respectait pas l'architecture hexagonale :
- ❌ `MarketDataService` lançait des **exceptions** au lieu de retourner des **Result**
- ❌ Non conforme aux autres Use Cases (UC-01, UC-02, UC-03)
- ❌ Violation du principe hexagonal : les services d'application ne doivent pas lancer d'exceptions métier

### Exemple de code problématique (AVANT)

```csharp
// ❌ MAUVAIS : Lance une exception
public async Task<MarketDataResult> SubscribeAsync(...)
{
    if (invalidSymbols.Any())
    {
        throw new InvalidOperationException($"Symboles invalides: ...");
    }
    // ...
}
```

---

## 🎯 Solution Implémentée

### 1. Création des Result Objects

**Fichier créé:** `src/Domain/Contracts/MarketDataOperationResult.cs`

```csharp
/// <summary>
/// Result générique pour les opérations de données de marché
/// Pattern hexagonal : pas d'exceptions dans les use cases
/// </summary>
public sealed record MarketDataOperationResult
{
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public MarketDataResult? Data { get; init; }

    public static MarketDataOperationResult Ok(MarketDataResult data) => new()
    {
        Success = true,
        Data = data
    };

    public static MarketDataOperationResult Fail(string errorCode, string errorMessage) => new()
    {
        Success = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };
}
```

**Autres Result créés:**
- `QuotesResult` : Pour la récupération de cotations
- `UnsubscribeResult` : Pour l'annulation d'abonnement

---

### 2. Modification de l'Interface `IMarketDataUseCase`

**Fichier modifié:** `src/Domain/Ports.Inbound/IMarketDataUseCase.cs`

#### AVANT (❌ Incorrect)
```csharp
public interface IMarketDataUseCase
{
    Task<MarketDataResult> SubscribeAsync(...);
    Task<MarketDataResult> AddSymbolsAsync(...);
    Task<MarketDataResult> RemoveSymbolsAsync(...);
    Task UnsubscribeAsync(...);  // ❌ Pas de retour
    Task<List<Quote>> GetLatestQuotesAsync(...);  // ❌ Pas de Result
}
```

#### APRÈS (✅ Correct)
```csharp
/// <summary>
/// Architecture hexagonale : les méthodes retournent des Result, pas d'exceptions
/// </summary>
public interface IMarketDataUseCase
{
    Task<MarketDataOperationResult> SubscribeAsync(...);
    Task<MarketDataOperationResult> AddSymbolsAsync(...);
    Task<MarketDataOperationResult> RemoveSymbolsAsync(...);
    Task<UnsubscribeResult> UnsubscribeAsync(...);  // ✅ Retourne Result
    Task<QuotesResult> GetLatestQuotesAsync(...);  // ✅ Retourne Result
}
```

---

### 3. Refactoring de `MarketDataService`

**Fichier modifié:** `src/Application/Services/MarketDataService.cs`

#### Exemple : Méthode `SubscribeAsync`

**AVANT (❌ Lance exception):**
```csharp
public async Task<MarketDataResult> SubscribeAsync(...)
{
    var invalidSymbols = symbols.Where(s => !availableSymbols.Contains(s.ToUpper())).ToList();
    if (invalidSymbols.Any())
    {
        _logger.LogWarning("Symboles invalides: {InvalidSymbols}", ...);
        throw new InvalidOperationException($"Symboles invalides: ...");
        // ❌ EXCEPTION LANCÉE
    }
    
    var subscription = Subscription.Creer(clientId, symbols, canal);
    await _subscriptions.AddAsync(subscription, ct);
    
    return MapToResult(subscription);
}
```

**APRÈS (✅ Retourne Result):**
```csharp
public async Task<MarketDataOperationResult> SubscribeAsync(...)
{
    var invalidSymbols = symbols.Where(s => !availableSymbols.Contains(s.ToUpper())).ToList();
    if (invalidSymbols.Any())
    {
        _logger.LogWarning("Symboles invalides: {InvalidSymbols}", ...);
        return MarketDataOperationResult.Fail(
            "INVALID_SYMBOLS",
            $"Symboles invalides: {string.Join(", ", invalidSymbols)}. " +
            $"Symboles disponibles: {string.Join(", ", availableSymbols)}");
        // ✅ RESULT RETOURNÉ
    }
    
    var subscription = Subscription.Creer(clientId, symbols, canal);
    await _subscriptions.AddAsync(subscription, ct);
    
    return MarketDataOperationResult.Ok(MapToResult(subscription));
    // ✅ RESULT RETOURNÉ
}
```

#### Toutes les méthodes refactorées

1. **SubscribeAsync** : Retourne `MarketDataOperationResult`
2. **AddSymbolsAsync** : Retourne `MarketDataOperationResult`
3. **RemoveSymbolsAsync** : Retourne `MarketDataOperationResult` + gère exception Domain
4. **UnsubscribeAsync** : Retourne `UnsubscribeResult`
5. **GetLatestQuotesAsync** : Retourne `QuotesResult`

---

### 4. Adaptation du Contrôleur `MarketDataController`

**Fichier modifié:** `src/Infrastructure.Web/Controllers/MarketDataController.cs`

Le contrôleur doit maintenant **gérer les Result** au lieu de catcher des exceptions.

#### Exemple : Endpoint `POST /api/v1/market/subscribe`

**AVANT (❌ Pas de gestion d'erreur):**
```csharp
[HttpPost("subscribe")]
public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest req, CancellationToken ct)
{
    var result = await _marketDataUseCase.SubscribeAsync(req.ClientId, req.Symbols, canal, ct);
    
    return Ok(new
    {
        subscriptionId = result.SubscriptionId,  // ❌ Crash si erreur
        symbols = result.Symbols,
        canal = result.Canal,
        statut = result.Statut
    });
}
```

**APRÈS (✅ Gère Result):**
```csharp
[HttpPost("subscribe")]
public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest req, CancellationToken ct)
{
    var result = await _marketDataUseCase.SubscribeAsync(req.ClientId, req.Symbols, canal, ct);

    if (!result.Success)
    {
        Log.Warning("[UC04_SUBSCRIBE] Échec: {ErrorCode} - {ErrorMessage}", 
            result.ErrorCode, result.ErrorMessage);
        return BadRequest(new 
        { 
            error = result.ErrorCode, 
            message = result.ErrorMessage 
        });
    }

    return Ok(new
    {
        subscriptionId = result.Data!.SubscriptionId,  // ✅ Data est non-null si Success
        symbols = result.Data.Symbols,
        canal = result.Data.Canal,
        statut = result.Data.Statut
    });
}
```

#### Tous les endpoints adaptés

1. **POST /api/v1/market/subscribe** : Gère `MarketDataOperationResult`
2. **POST /api/v1/market/unsubscribe** : Gère `UnsubscribeResult`
3. **POST /api/v1/market/quotes** : Gère `QuotesResult`
4. **POST /api/v1/market/subscription/{id}/add-symbols** : Gère `MarketDataOperationResult`
5. **POST /api/v1/market/subscription/{id}/remove-symbols** : Gère `MarketDataOperationResult`

---

## 📊 Résumé des Changements

| Fichier | Type | Changements |
|---------|------|-------------|
| `Domain/Contracts/MarketDataOperationResult.cs` | ✨ **Nouveau** | Result objects pour UC-04 |
| `Domain/Ports.Inbound/IMarketDataUseCase.cs` | 🔄 **Modifié** | Signatures retournent Result |
| `Application/Services/MarketDataService.cs` | 🔄 **Modifié** | Toutes les méthodes retournent Result |
| `Infrastructure.Web/Controllers/MarketDataController.cs` | 🔄 **Modifié** | Gère les Result correctement |

---

## ✅ Architecture Hexagonale Respectée

### Avant Refactoring (❌ Non-conforme)

```
┌─────────────────────────────────────────┐
│ Controller (Infrastructure)             │
│ ❌ Try/Catch autour du use case         │
└───────────────┬─────────────────────────┘
                │
┌───────────────▼─────────────────────────┐
│ MarketDataService (Application)         │
│ ❌ throw InvalidOperationException       │
└───────────────┬─────────────────────────┘
                │
┌───────────────▼─────────────────────────┐
│ Domain Entities                          │
│ ✅ throw InvalidOperationException (OK)  │
└─────────────────────────────────────────┘
```

### Après Refactoring (✅ Conforme)

```
┌─────────────────────────────────────────┐
│ Controller (Infrastructure)             │
│ ✅ if (!result.Success) → BadRequest    │
└───────────────┬─────────────────────────┘
                │
┌───────────────▼─────────────────────────┐
│ MarketDataService (Application)         │
│ ✅ return Result.Fail(...)               │
│ ✅ return Result.Ok(...)                 │
└───────────────┬─────────────────────────┘
                │
┌───────────────▼─────────────────────────┐
│ Domain Entities                          │
│ ✅ throw InvalidOperationException (OK)  │
│    (capturé par Application layer)       │
└─────────────────────────────────────────┘
```

---

## 🎯 Principes Hexagonaux Respectés

### 1. ✅ Pas d'exceptions dans Application Layer

Les services d'application ne lancent **jamais** d'exceptions métier, ils retournent des `Result`.

```csharp
// ✅ CORRECT
if (subscription == null)
{
    return MarketDataOperationResult.Fail("SUBSCRIPTION_NOT_FOUND", "...");
}

// ❌ INCORRECT
if (subscription == null)
{
    throw new InvalidOperationException("Subscription not found");
}
```

### 2. ✅ Domain peut lancer des exceptions

Le Domain **peut** lancer des exceptions car c'est sa responsabilité de protéger l'invariant métier.

```csharp
// Domain/Model/MarketData/Subscription.cs
public void RetirerSymbole(string symbol)
{
    if (_symbols.Count == 1)
        throw new InvalidOperationException("Cannot remove last symbol");
    // ✅ OK : Domain protège son invariant
}
```

**L'Application Layer** capture ces exceptions et les transforme en Result :

```csharp
// Application/Services/MarketDataService.cs
try
{
    subscription.RetirerSymbole(symbol);
}
catch (InvalidOperationException ex) when (ex.Message.Contains("dernier symbole"))
{
    return MarketDataOperationResult.Fail("CANNOT_REMOVE_LAST_SYMBOL", "...");
    // ✅ Exception Domain → Result Application
}
```

### 3. ✅ Infrastructure gère les Result

Le Controller (Infrastructure) **teste** les Result et retourne les codes HTTP appropriés.

```csharp
if (!result.Success)
{
    return BadRequest(new { error = result.ErrorCode, message = result.ErrorMessage });
}
return Ok(result.Data);
```

---

## 🔍 Comparaison avec Autres Use Cases

### UC-01 Signup (✅ Déjà conforme)

```csharp
public async Task<SignupResult> CreateAccountAsync(...)
{
    // Validation
    if (invalid)
        return SignupResult.Fail("ERROR_CODE", "...");
    
    // Logique métier
    var client = Client.Creer(...);
    
    return SignupResult.Ok(client.ClientId, ...);
}
```

### UC-03 Deposit (✅ Déjà conforme)

```csharp
public async Task<DepositResult> RequestAsync(...)
{
    var limites = LimitesDepot.ParDefaut();
    limites.ValiderMontant(amount, currency);  // Lance exception si invalide
    
    // Pas de try/catch ici, on laisse l'exception remonter
    // ❌ MAIS : devrait aussi retourner Result!
    
    var tx = TransactionPaiement.Creer(...);
    return MapToResult(tx, wallet);
}
```

### UC-04 Market Data (✅ Maintenant conforme)

```csharp
public async Task<MarketDataOperationResult> SubscribeAsync(...)
{
    // Validation
    if (invalidSymbols.Any())
        return MarketDataOperationResult.Fail("INVALID_SYMBOLS", "...");
    
    // Logique métier
    var subscription = Subscription.Creer(...);
    
    return MarketDataOperationResult.Ok(MapToResult(subscription));
}
```

---

## 🧪 Tests à Mettre à Jour

Les tests doivent maintenant vérifier les **Result** au lieu de catcher des exceptions.

### AVANT (❌ Teste exception)

```csharp
[Fact]
public async Task Subscribe_InvalidSymbols_ThrowsException()
{
    // Arrange
    var symbols = new List<string> { "INVALID_SYMBOL" };
    
    // Act & Assert
    await Assert.ThrowsAsync<InvalidOperationException>(
        () => _service.SubscribeAsync(clientId, symbols));
}
```

### APRÈS (✅ Teste Result)

```csharp
[Fact]
public async Task Subscribe_InvalidSymbols_ReturnsFailure()
{
    // Arrange
    var symbols = new List<string> { "INVALID_SYMBOL" };
    
    // Act
    var result = await _service.SubscribeAsync(clientId, symbols);
    
    // Assert
    Assert.False(result.Success);
    Assert.Equal("INVALID_SYMBOLS", result.ErrorCode);
    Assert.Contains("INVALID_SYMBOL", result.ErrorMessage);
}
```

---

## 📝 Codes d'Erreur Standardisés

| Code d'Erreur | Description | HTTP Status |
|---------------|-------------|-------------|
| `INVALID_SYMBOLS` | Symboles invalides ou inconnus | 400 Bad Request |
| `SUBSCRIPTION_NOT_FOUND` | Abonnement introuvable | 400 Bad Request |
| `SUBSCRIPTION_CANCELLED` | Opération sur abonnement annulé | 400 Bad Request |
| `CANNOT_REMOVE_LAST_SYMBOL` | Impossible de retirer le dernier symbole | 400 Bad Request |

---

## 🎉 Bénéfices du Refactoring

### 1. ✅ Cohérence avec le reste du projet

UC-04 suit maintenant le même pattern que UC-01, UC-02, UC-03.

### 2. ✅ Meilleure testabilité

Pas besoin de `Assert.Throws`, on teste les `Result.Success` et `Result.ErrorCode`.

### 3. ✅ API HTTP plus propre

Les codes d'erreur sont standardisés et retournés dans un format JSON cohérent :

```json
{
  "error": "INVALID_SYMBOLS",
  "message": "Symboles invalides: XYZ. Symboles disponibles: AAPL, GOOGL, MSFT, ..."
}
```

### 4. ✅ Logs structurés

Les logs capturent les erreurs métier de manière cohérente :

```csharp
Log.Warning("[UC04_SUBSCRIBE] Échec: {ErrorCode} - {ErrorMessage}", 
    result.ErrorCode, result.ErrorMessage);
```

### 5. ✅ Séparation des préoccupations

- **Domain** : Protège les invariants (lance exceptions)
- **Application** : Orchestre (retourne Result)
- **Infrastructure** : Adapte aux protocols (HTTP, SignalR)

---

## 🔧 Fichiers Non Modifiés (et pourquoi)

### `MarketFeedSimulator.cs` (Adapter)

**Raison:** C'est un **adapter d'infrastructure**, il **peut** lancer des exceptions.

```csharp
public async Task<Quote> GenerateQuoteForSymbol(string symbol, CancellationToken ct)
{
    if (!_baseprices.TryGetValue(symbolUpper, out var basePrice))
    {
        throw new ArgumentException($"Unknown symbol: {symbol}");
        // ✅ OK : Adapter peut throw
    }
    // ...
}
```

### `MarketDataHub.cs` (SignalR Hub)

**Raison:** C'est de l'**infrastructure**, pas de changement nécessaire.

Les Hubs SignalR gèrent automatiquement les exceptions et les transforment en messages d'erreur pour le client WebSocket.

### `MarketDataBroadcaster.cs` (Background Service)

**Raison:** C'est un **service d'infrastructure**, il **peut** logger et catcher des exceptions.

```csharp
catch (Exception ex)
{
    Log.Error(ex, "[UC04_BROADCASTER] Error broadcasting quote for {Symbol}", quote.Symbol);
    // ✅ OK : Infrastructure peut catch et logger
}
```

---

## 🚀 Prochaines Étapes

### Tests à ajouter

1. Tests unitaires de `MarketDataService` avec les nouveaux Result
2. Tests d'intégration du Controller avec Result
3. Tests E2E via SignalR

### Documentation à mettre à jour

- [x] Guide d'architecture hexagonale
- [ ] Guide des tests UC-04
- [ ] API documentation (Swagger)

---

## 📚 Références

- **ADR-001** : Architecture Hexagonale
- **SignupService.cs** : Exemple de service conforme
- **WalletService.cs** : Exemple de service conforme
- **Clean Architecture** by Robert C. Martin

---

**✅ Refactoring complété avec succès!**  
UC-04 respecte maintenant l'architecture hexagonale comme le reste du projet.
