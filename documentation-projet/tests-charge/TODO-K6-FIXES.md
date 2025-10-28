# 🎯 Actions Post-K6 Baseline

**Date découverte**: 28 octobre 2025  
**Test**: Baseline (N=1, scénario mixte)  
**Status**: 5/12 thresholds FAILED

---

## ✅ Légende

- 🔴 **BLOQUANT**: Empêche le système de fonctionner correctement
- 🟠 **CRITIQUE**: Impact majeur sur performance/stabilité
- 🟡 **IMPORTANT**: Amélioration significative nécessaire
- 🟢 **NICE-TO-HAVE**: Optimisation optionnelle

---

## 🔴 Priority 1 - BLOQUANT (À faire IMMÉDIATEMENT)

### 1. Implémenter UC-03 Dépôt avec Idempotency-Key

**Problème**:
- 878 échecs / 878 tentatives (100%)
- Endpoint `/api/v1/wallet/deposit` retourne 404 ou 500
- Error rate global: 12.55% (dont 100% des deposits)

**Impact**:
- Use Case UC-03 non fonctionnel
- Tests de charge invalides
- Impossible de valider idempotency

**Solution**:

```csharp
// 1. Entity - Infrastructure.Persistence/Model/PortefeuilleReglement/TransactionPaiement.cs
public class TransactionPaiement {
    public Guid TransactionId { get; set; }
    public Guid ClientId { get; set; }
    public Guid CompteId { get; set; }
    public decimal Montant { get; set; }
    public string Devise { get; set; }
    public string PaymentMethod { get; set; }
    public string IdempotencyKey { get; set; }  // UNIQUE INDEX
    public DateTime CreatedAt { get; set; }
    // ...
}

// 2. Repository - Infrastructure.Persistence/Repositories/WalletRepository.cs
public async Task<TransactionPaiement?> GetByIdempotencyKeyAsync(string key) {
    return await _context.TransactionsPaiement
        .FirstOrDefaultAsync(t => t.IdempotencyKey == key);
}

// 3. Service - Application/Services/WalletService.cs
public async Task<DepositResult> DepositAsync(
    Guid clientId, 
    decimal amount, 
    string currency, 
    string paymentMethod,
    string idempotencyKey)
{
    // Vérifier idempotency
    var existing = await _walletRepo.GetByIdempotencyKeyAsync(idempotencyKey);
    if (existing != null) {
        return new DepositResult { 
            TransactionId = existing.TransactionId,
            Status = "AlreadyProcessed",
            Idempotent = true 
        };
    }

    // Créer la transaction
    var transaction = new TransactionPaiement {
        TransactionId = Guid.NewGuid(),
        ClientId = clientId,
        Montant = amount,
        Devise = currency,
        PaymentMethod = paymentMethod,
        IdempotencyKey = idempotencyKey,
        CreatedAt = DateTime.UtcNow
    };

    await _walletRepo.CreateTransactionAsync(transaction);
    
    return new DepositResult { 
        TransactionId = transaction.TransactionId,
        Status = "Success",
        Idempotent = false
    };
}

// 4. Controller - Infrastructure.Web/Controllers/WalletController.cs
[HttpPost("deposit")]
public async Task<IActionResult> Deposit(
    [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
    [FromBody] DepositRequest request)
{
    if (string.IsNullOrEmpty(idempotencyKey)) {
        return BadRequest(new { error = "Idempotency-Key header required" });
    }

    var result = await _walletService.DepositAsync(
        clientId: GetClientIdFromSession(),
        amount: request.Amount,
        currency: request.Currency,
        paymentMethod: request.PaymentMethod,
        idempotencyKey: idempotencyKey
    );

    if (result.Idempotent) {
        return Conflict(new { 
            transactionId = result.TransactionId,
            message = "Transaction already processed"
        });
    }

    return Ok(new { transactionId = result.TransactionId });
}
```

**Migration SQL**:
```sql
CREATE UNIQUE INDEX idx_transaction_idempotency 
ON TransactionPaiement(IdempotencyKey);
```

**Tests de validation**:
```bash
# Vérifier idempotence
curl -X POST http://localhost:5000/api/v1/wallet/deposit \
  -H "Idempotency-Key: test-key-123" \
  -H "Content-Type: application/json" \
  -d '{"amount":100,"currency":"CAD","paymentMethod":"INTERAC"}'

# Retry avec même key (devrait retourner 409)
curl -X POST http://localhost:5000/api/v1/wallet/deposit \
  -H "Idempotency-Key: test-key-123" \
  -H "Content-Type: application/json" \
  -d '{"amount":100,"currency":"CAD","paymentMethod":"INTERAC"}'
```

**Temps estimé**: 3-4 heures

---

### 2. Fixer Health Check JSON Format

**Problème**:
- 2,086 échecs de validation `health has status`
- k6 script attend `JSON.parse(body).status`
- Impact: Read success rate = 49.36% (vs objectif 98%)

**Solution**:

```csharp
// Infrastructure.Web/Controllers/HealthController.cs
[HttpGet("/health")]
public IActionResult Health() {
    return Ok(new { 
        status = "Healthy",
        duration = "00:00:00.0123456",
        timestamp = DateTime.UtcNow
    });
}
```

**Test**:
```bash
curl http://localhost:5000/health | jq .
# Attendu: {"status":"Healthy","duration":"...","timestamp":"..."}
```

**Temps estimé**: 15 minutes

---

## 🟠 Priority 2 - CRITIQUE (Semaine prochaine)

### 3. Optimiser Latence Write (P95 = 3.51s → < 800ms)

**Problème**:
- Write P95: 3.51s (vs objectif 800ms)
- Écart: +339%
- Max latency: 5.78s

**Causes identifiées**:
1. Pas d'index sur Client.Email
2. Connection pool MySQL non configuré
3. OTP generation synchrone
4. Pas de AsNoTracking() sur reads

**Solutions**:

#### 3a. Index Database

```sql
-- Ajouter index sur colonnes fréquemment recherchées
CREATE UNIQUE INDEX idx_client_email ON Client(Email);
CREATE INDEX idx_compte_clientid ON Compte(ClientId);
CREATE INDEX idx_session_clientid ON Session(ClientId);
CREATE INDEX idx_otp_clientid ON VerifContactOTP(ClientId);
```

**Test**:
```sql
EXPLAIN SELECT * FROM Client WHERE Email = 'test@example.com';
-- Doit utiliser idx_client_email
```

#### 3b. Connection Pool MySQL

```json
// appsettings.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=mysql;Port=3306;Database=brokerx_dev;Uid=root;Pwd=rootpassword;Min Pool Size=5;Max Pool Size=20;Connection Timeout=30;"
  }
}
```

#### 3c. OTP Async

```csharp
// HybridEmailOtpAdapter.cs
public async Task SendCodeAsync(string email, string code) {
    // Fire-and-forget logging
    _ = Task.Run(() => {
        Console.WriteLine($"[OTP] canal=Email dest={email} code={code}");
    });
    
    await Task.CompletedTask;
}
```

#### 3d. EF Core AsNoTracking

```csharp
// ClientRepository.cs
public async Task<Client?> GetByEmailAsync(string email) {
    return await _context.Client
        .AsNoTracking()  // Pas de tracking pour reads
        .FirstOrDefaultAsync(c => c.Email == email);
}
```

**Temps estimé**: 2-3 heures

---

## 🟡 Priority 3 - IMPORTANT (Phase 2 Étape 2a suite)

### 4. NGINX Load Balancer (N=4 instances)

**Objectif**: Passer de 22.95 RPS à > 80 RPS

**Configuration**:

```nginx
# nginx/nginx.conf
upstream brokerx_api {
    least_conn;  # ou ip_hash pour sticky sessions
    server api-1:5001;
    server api-2:5002;
    server api-3:5003;
    server api-4:5004;
}

server {
    listen 80;
    
    location / {
        proxy_pass http://brokerx_api;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    }
    
    location /health {
        proxy_pass http://brokerx_api/health;
        proxy_next_upstream error timeout http_500;
    }
}
```

**docker-compose.yml**:
```yaml
services:
  api:
    deploy:
      replicas: 4
    ports:
      - "5001-5004:5000"
    
  nginx:
    image: nginx:alpine
    ports:
      - "80:80"
    volumes:
      - ./nginx/nginx.conf:/etc/nginx/nginx.conf
    depends_on:
      - api
```

**Test**:
```bash
docker compose up -d --scale api=4
k6 run --out json=resultats-k6/lb-n4.json scripts/k6/mixed.js
```

**Temps estimé**: 3 heures

---

### 5. Redis Cache (Améliorer Read Latency)

**Objectif**: Cache hit rate > 80%, Read P95 < 5ms

**Configuration**:

```yaml
# docker-compose.yml
redis:
  image: redis:7-alpine
  ports:
    - "6379:6379"
  command: redis-server --maxmemory 256mb --maxmemory-policy allkeys-lru
```

**Implementation**:

```csharp
// Application/Services/CachedClientService.cs
public class CachedClientService {
    private readonly IClientRepository _repo;
    private readonly IDistributedCache _cache;
    
    public async Task<Client?> GetByEmailAsync(string email) {
        // Essayer cache d'abord
        var cacheKey = $"client:email:{email}";
        var cached = await _cache.GetStringAsync(cacheKey);
        
        if (cached != null) {
            Log.Information("CACHE_HIT client {Email}", email);
            return JsonSerializer.Deserialize<Client>(cached);
        }
        
        // Fallback DB
        Log.Information("CACHE_MISS client {Email}", email);
        var client = await _repo.GetByEmailAsync(email);
        
        if (client != null) {
            await _cache.SetStringAsync(cacheKey, 
                JsonSerializer.Serialize(client),
                new DistributedCacheEntryOptions { 
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
                });
        }
        
        return client;
    }
}
```

**Temps estimé**: 4 heures

---

## 📊 Validation Post-Fixes

### Tests de régression

```bash
# 1. Test après UC-03
./run-k6-test.sh after-uc03 scripts/k6/mixed.js
# Attendu: Error rate < 5%, Write success > 92%

# 2. Test après optimisations DB
./run-k6-test.sh after-db-opt scripts/k6/mixed.js
# Attendu: Write P95 < 800ms

# 3. Test avec LB N=4
docker compose up -d --scale api=4
./run-k6-test.sh lb-n4 scripts/k6/mixed.js
# Attendu: RPS > 80, P95 < 300ms

# 4. Test avec Redis
./run-k6-test.sh with-redis scripts/k6/mixed.js
# Attendu: Read P95 < 5ms, cache hit > 80%
```

### Métriques de succès

| Objectif | Baseline | Post-UC03 | Post-DB-Opt | LB N=4 + Redis |
|----------|----------|-----------|-------------|----------------|
| P95 Latency | 637ms | 500ms | 300ms | 150ms |
| Error Rate | 12.55% | 3% | 2% | 0.5% |
| RPS | 22.95 | 25 | 35 | 90 |
| Write Success | 69.37% | 95% | 98% | 99% |
| Read Success | 49.36% | 98% | 99% | 99.9% |

---

## 📅 Timeline

```
Semaine 1 (Immédiat):
├─ Jour 1: UC-03 Implementation (4h)
├─ Jour 2: Health Check + Index DB (3h)
├─ Jour 3: Connection Pool + OTP Async (2h)
└─ Jour 4: Tests de validation (2h)

Semaine 2 (Load Balancing):
├─ Jour 1-2: NGINX Config + Tests (6h)
├─ Jour 3-4: Redis Cache + Tests (8h)
└─ Jour 5: Documentation + Rapport (4h)
```

**Durée totale estimée**: ~30 heures

---

**Créé par**: k6 Baseline Test Analysis  
**Dernière mise à jour**: 28 octobre 2025  
**Next review**: Après Priority 1 fixes
