# UC-05 : Guide de Test Rapide

## ✅ Statut Implémentation
- ✅ **Build:** Successful (9.6s)
- ✅ **Warnings:** 2 (non-bloquants)
- ✅ **Errors:** 0
- ✅ **Architecture Hexagonale:** Respectée à 100%

## 🚀 Démarrage Rapide

### 1. Build du Projet
```bash
cd /home/pop28/Documents/github/projet-log-430
dotnet build
```

### 2. Lancer l'Application
```bash
cd src/Infrastructure.Web
dotnet run
```

L'API sera disponible sur: **http://localhost:8080**

### 3. Swagger UI
Ouvrir: **http://localhost:8080/swagger**

## 📋 Tests Manuels

### Test 1: Placement Ordre Limit Buy (Succès)

#### Étape 1: Créer un compte
```bash
curl -X POST http://localhost:8080/api/v1/auth/signup \
  -H "Content-Type: application/json" \
  -d '{
    "email": "trader@test.com",
    "password": "Test1234!",
    "firstName": "John",
    "lastName": "Trader"
  }'
```

**Réponse attendue:** Récupérer `accountId`

#### Étape 2: Déposer des fonds
```bash
curl -X POST http://localhost:8080/api/v1/wallet/deposit \
  -H "Content-Type: application/json" \
  -d '{
    "accountId": "{VOTRE_ACCOUNT_ID}",
    "amount": 50000,
    "currency": "USD"
  }'
```

#### Étape 3: Placer un ordre
```bash
curl -X POST http://localhost:8080/api/v1/orders/{VOTRE_ACCOUNT_ID} \
  -H "Content-Type: application/json" \
  -d '{
    "clientOrderId": "ORDER-TEST-001",
    "symbol": "AAPL",
    "side": "Buy",
    "type": "Limit",
    "quantity": 100,
    "price": 195.50,
    "timeInForce": "DAY"
  }'
```

**Réponse attendue (200 OK):**
```json
{
  "orderId": "3fa85f64-...",
  "clientOrderId": "ORDER-TEST-001",
  "accountId": "...",
  "symbol": "AAPL",
  "side": "Buy",
  "type": "Limit",
  "quantity": 100,
  "price": 195.50,
  "status": "Working",
  "filledQuantity": 0,
  "createdAt": "2025-12-05T..."
}
```

### Test 2: Idempotence

Soumettre **exactement le même ordre** une 2ème fois:

```bash
curl -X POST http://localhost:8080/api/v1/orders/{VOTRE_ACCOUNT_ID} \
  -H "Content-Type: application/json" \
  -d '{
    "clientOrderId": "ORDER-TEST-001",
    "symbol": "AAPL",
    "side": "Buy",
    "type": "Limit",
    "quantity": 100,
    "price": 195.50
  }'
```

**Réponse attendue (409 Conflict):**
```json
{
  "error": "ORDER_ALREADY_EXISTS",
  "message": "Order with clientOrderId 'ORDER-TEST-001' already exists",
  "orderId": "3fa85f64-..."
}
```

### Test 3: Insufficient Funds

Placer un ordre avec montant > cash disponible:

```bash
curl -X POST http://localhost:8080/api/v1/orders/{VOTRE_ACCOUNT_ID} \
  -H "Content-Type: application/json" \
  -d '{
    "clientOrderId": "ORDER-TEST-002",
    "symbol": "AAPL",
    "side": "Buy",
    "type": "Limit",
    "quantity": 1000,
    "price": 195.50
  }'
```

**Réponse attendue (409 Conflict):**
```json
{
  "error": "INSUFFICIENT_FUNDS",
  "message": "Insufficient buying power. Required: $195,500.00, Available: $30,450.00"
}
```

### Test 4: Prix Hors Bandes

Placer un ordre avec prix > 10% au-dessus du marché:

```bash
curl -X POST http://localhost:8080/api/v1/orders/{VOTRE_ACCOUNT_ID} \
  -H "Content-Type: application/json" \
  -d '{
    "clientOrderId": "ORDER-TEST-003",
    "symbol": "AAPL",
    "side": "Buy",
    "type": "Limit",
    "quantity": 10,
    "price": 250.00
  }'
```

**Réponse attendue (409 Conflict):**
```json
{
  "error": "PRICE_OUT_OF_BANDS",
  "message": "Price $250.00 is outside allowed bands [$180.00 - $220.00]"
}
```

### Test 5: Symbole Invalide

```bash
curl -X POST http://localhost:8080/api/v1/orders/{VOTRE_ACCOUNT_ID} \
  -H "Content-Type: application/json" \
  -d '{
    "clientOrderId": "ORDER-TEST-004",
    "symbol": "INVALID",
    "side": "Buy",
    "type": "Market",
    "quantity": 10
  }'
```

**Réponse attendue (409 Conflict):**
```json
{
  "error": "INSTRUMENT_NOT_ACTIVE",
  "message": "Instrument 'INVALID' is not active or does not exist"
}
```

### Test 6: Consulter un Ordre

```bash
curl -X GET http://localhost:8080/api/v1/orders/{ACCOUNT_ID}/orders/{ORDER_ID}
```

**Réponse attendue (200 OK):** Détails complets de l'ordre

### Test 7: Annuler un Ordre

```bash
curl -X DELETE http://localhost:8080/api/v1/orders/{ACCOUNT_ID}/orders/{ORDER_ID}
```

**Réponse attendue (200 OK):**
```json
{
  "orderId": "...",
  "status": "Cancelled",
  ...
}
```

### Test 8: Annuler un Ordre Déjà Annulé

Tenter d'annuler le même ordre à nouveau:

```bash
curl -X DELETE http://localhost:8080/api/v1/orders/{ACCOUNT_ID}/orders/{ORDER_ID}
```

**Réponse attendue (409 Conflict):**
```json
{
  "error": "ORDER_TERMINAL",
  "message": "Cannot cancel order in terminal state: Cancelled"
}
```

## 🧪 Tests Unitaires (À Créer)

### Créer le fichier de tests
```bash
touch tests/Application.Tests/OrderServiceTests.cs
```

### Tests Minimaux à Implémenter
```csharp
[Fact] public async Task PlaceOrder_ValidMarketBuy_Success()
[Fact] public async Task PlaceOrder_InvalidSymbol_ReturnsError()
[Fact] public async Task PlaceOrder_InsufficientFunds_ReturnsError()
[Fact] public async Task PlaceOrder_Idempotence_ReturnsCachedOrder()
[Fact] public async Task CancelOrder_Success()
[Fact] public async Task CancelOrder_AlreadyCancelled_ReturnsError()
```

### Lancer les tests
```bash
cd /home/pop28/Documents/github/projet-log-430
dotnet test tests/Application.Tests/
```

## 📊 Vérifications

### ✅ Checklist Fonctionnelle
- [ ] Placement ordre Market Buy réussit
- [ ] Placement ordre Limit Buy réussit
- [ ] Placement ordre Sell réussit
- [ ] Idempotence fonctionne (double submit)
- [ ] Contrôle buying power fonctionne
- [ ] Contrôle price bands fonctionne
- [ ] Contrôle instrument status fonctionne
- [ ] Contrôle trading limits fonctionne
- [ ] Consultation ordre fonctionne
- [ ] Annulation ordre fonctionne
- [ ] Annulation ordre terminal échoue
- [ ] Symbole invalide retourne erreur

### ✅ Checklist Technique
- [ ] Build sans erreurs
- [ ] Swagger accessible
- [ ] Endpoints retournent JSON valide
- [ ] Codes HTTP corrects (200/400/404/409/500)
- [ ] Logs structurés visibles
- [ ] Cache Redis fonctionne
- [ ] DB InMemory persiste les ordres
- [ ] Mapping DTO correct

## 🐛 Troubleshooting

### Erreur: Port 8080 déjà utilisé
```bash
# Tuer le processus
lsof -ti:8080 | xargs kill -9

# Ou changer le port
export ASPNETCORE_URLS="http://localhost:5000"
dotnet run
```

### Erreur: Redis connection failed
```bash
# Vérifier Redis
docker ps | grep redis

# Si absent, démarrer Redis
docker run -d -p 6379:6379 redis:latest
```

### Erreur: Database connection
```bash
# Projet utilise InMemoryDatabase par défaut
# Pas de MySQL requis pour les tests
```

## 📈 Prochaines Étapes

1. ✅ **Implémentation complète** - FAIT
2. ⏳ **Tests manuels** - À FAIRE (ce guide)
3. ⏳ **Tests unitaires** - À CRÉER
4. ⏳ **Tests d'intégration** - À CRÉER
5. ⏳ **Matching engine** - À AMÉLIORER
6. ⏳ **WebSocket updates** - À CRÉER
7. ⏳ **Migration MySQL** - À FAIRE

## 📞 Support

### Fichiers Clés
- **Controller:** `src/Infrastructure.Web/Controllers/OrderController.cs`
- **Service:** `src/Application/Services/OrderService.cs`
- **Domain Model:** `src/Domain/Model/Trading/Ordre.cs`
- **Pre-Trade Checks:** `src/Infrastructure.Adapters/PreTrade/PreTradeCheckAdapter.cs`

### Documentation
- **Implémentation complète:** `documentation-projet/UC05-IMPLEMENTATION-COMPLETE.md`
- **Use Cases:** `docs/views/use_case.puml`
- **ADR Architecture:** `docs/adr/ADR-001-Architecture-hexagonale.md`

---

**Version:** 1.0  
**Date:** 5 décembre 2025  
**Statut:** ✅ READY FOR MANUAL TESTING
