# Plan d'exécution — BrokerX Phase 2

## État actuel (Phase 1 complétée)

### Implémenté ✅
1. **UC-01 Inscription**: SignupController → SignupService → Domain (Client, Compte, KYC, OTP)
2. **UC-02 Authentification**: AuthController → AuthService → Domain (Session, MFA)
3. **UC-03 Approvisionnement (partiel)**: WalletController → WalletService → Domain (PayTx, Portefeuille, Ledger)
   - ⚠️ **Manque**: idempotency-key (sera implémenté EN DERNIER)

### Architecture actuelle
- **Style**: Architecture Hexagonale (Ports & Adapters)
- **Stack**: .NET 9, MySQL 8, Docker Compose
- **Projets**:
  - `Domain`: entités, règles métier, ports (interfaces)
  - `Application`: services UC, orchestration
  - `Infrastructure.Web`: controllers REST, DTOs, Swagger
  - `Infrastructure.Persistence`: EF Core repositories, migrations
  - `Infrastructure.Adapters`: simulateurs (OTP, KYC, Payment)
- **API**: REST v1 (`/api/v1/*`), Swagger/OpenAPI
- **Sécurité**: JWT auth, CORS, validation DTOs

---

## Phase 2 — Industrialisation & Microservices

### Objectifs
1. Industrialiser l'observabilité (métriques, logs, dashboards)
2. Optimiser la performance (load balancing, caching)
3. Migrer vers architecture microservices avec API Gateway
4. Garantir l'idempotence pour UC-03 (dépôts)

### Ordre d'implémentation recommandé
1. Observabilité (Prometheus, Grafana, k6)
2. Load Balancing (NGINX)
3. Caching (Redis)
4. API Gateway (KrakenD)
5. Microservices (Orders, Portfolio, Reporting)
6. UC-03 Idempotency (EN DERNIER)
7. CI/CD (GitHub Actions)
8. Documentation (Arc42, ADRs)
9. Livraison (rapport PDF, archive)

---

## 1. Observabilité

### 1.1 Prometheus (métriques)

**Installation**
```yaml
# docker-compose.yml
prometheus:
  image: prom/prometheus:latest
  ports:
    - "9090:9090"
  volumes:
    - ./prometheus.yml:/etc/prometheus/prometheus.yml
    - prometheus-data:/prometheus
```

**Configuration** (`prometheus.yml`)
```yaml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'brokerx-api'
    static_configs:
      - targets: ['app:8080']
    metrics_path: '/metrics'
```

**Intégration .NET**
1. Installer NuGet: `dotnet add package prometheus-net.AspNetCore`
2. Ajouter middleware dans `Program.cs`:
```csharp
app.UseMetrics(); // Expose /metrics endpoint
app.UseHttpMetrics(); // Collect HTTP metrics
```

3. Créer métriques custom:
```csharp
private static readonly Counter SignupRequests = Metrics
    .CreateCounter("signup_requests_total", "Total signup requests");

private static readonly Histogram LoginLatency = Metrics
    .CreateHistogram("login_latency_seconds", "Login latency in seconds");
```

### 1.2 Grafana (dashboards)

**Installation**
```yaml
# docker-compose.yml
grafana:
  image: grafana/grafana:latest
  ports:
    - "3000:3000"
  environment:
    - GF_SECURITY_ADMIN_PASSWORD=admin
  volumes:
    - grafana-data:/var/lib/grafana
    - ./grafana/dashboards:/etc/grafana/provisioning/dashboards
    - ./grafana/datasources:/etc/grafana/provisioning/datasources
```

**Datasource Prometheus** (`grafana/datasources/prometheus.yml`)
```yaml
apiVersion: 1
datasources:
  - name: Prometheus
    type: prometheus
    url: http://prometheus:9090
    isDefault: true
```

**Dashboard 4 Golden Signals** (créer dans Grafana UI):
1. **Latence**: 
   - P50: `histogram_quantile(0.50, http_request_duration_seconds_bucket)`
   - P95: `histogram_quantile(0.95, http_request_duration_seconds_bucket)`
   - P99: `histogram_quantile(0.99, http_request_duration_seconds_bucket)`

2. **Trafic**: 
   - RPS: `rate(http_requests_total[1m])`

3. **Erreurs**: 
   - Taux 4xx: `rate(http_requests_total{code=~"4.."}[1m])`
   - Taux 5xx: `rate(http_requests_total{code=~"5.."}[1m])`

4. **Saturation**: 
   - CPU: `process_cpu_seconds_total`
   - RAM: `process_working_set_bytes`
   - Threads: `threadpool_active_threads`

### 1.3 Logs structurés (JSON)

**Installation Serilog**
```bash
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.File
```

**Configuration** (`Program.cs`)
```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new JsonFormatter())
    .WriteTo.File(
        new JsonFormatter(), 
        "logs/brokerx-.json", 
        rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();
```

**Événements à logger**:
- Signup: `{ "event": "signup_completed", "clientId": "...", "status": "..." }`
- Login: `{ "event": "login_attempt", "clientId": "...", "success": true, "mfaRequired": false }`
- Deposit: `{ "event": "deposit_initiated", "accountId": "...", "amount": 100.00, "idempotencyKey": "..." }`
- Errors: `{ "event": "error", "endpoint": "/api/v1/signup", "statusCode": 500, "exception": "..." }`

### 1.4 Tests de charge (k6)

**Scénario 1: Signup** (`scripts/k6/signup.js`)
```javascript
import http from 'k6/http';
import { check } from 'k6';

export const options = {
  vus: 10,
  duration: '30s',
  thresholds: {
    http_req_duration: ['p(95)<500'],
  },
};

export default function() {
  const url = 'http://localhost:8080/api/v1/signup';
  const payload = JSON.stringify({
    email: `user-${Date.now()}@example.com`,
    password: 'Test1234!',
    firstName: 'John',
    lastName: 'Doe',
  });
  
  const res = http.post(url, payload, {
    headers: { 'Content-Type': 'application/json' },
  });
  
  check(res, {
    'status is 201': (r) => r.status === 201,
    'has clientId': (r) => JSON.parse(r.body).clientId !== undefined,
  });
}
```

**Scénario 2: Login** (`scripts/k6/auth.js`)
```javascript
export default function() {
  const url = 'http://localhost:8080/api/v1/login';
  const payload = JSON.stringify({
    email: 'test@example.com',
    password: 'Test1234!',
  });
  
  const res = http.post(url, payload, {
    headers: { 'Content-Type': 'application/json' },
  });
  
  check(res, {
    'status is 200': (r) => r.status === 200,
    'has JWT token': (r) => JSON.parse(r.body).token !== undefined,
  });
}
```

**Scénario 3: Deposit** (`scripts/k6/deposit.js`)
```javascript
import { uuidv4 } from 'https://jslib.k6.io/k6-utils/1.0.0/index.js';

export default function() {
  const url = 'http://localhost:8080/api/v1/deposits';
  const payload = JSON.stringify({
    accountId: 'acc-123',
    amount: 100.00,
  });
  
  const res = http.post(url, payload, {
    headers: {
      'Content-Type': 'application/json',
      'Authorization': 'Bearer ' + __ENV.JWT_TOKEN,
      'Idempotency-Key': uuidv4(),
    },
  });
  
  check(res, {
    'status is 201': (r) => r.status === 201,
  });
}
```

**Scénario 4: Mixed** (`scripts/k6/mixed.js`)
```javascript
import { randomItem } from 'https://jslib.k6.io/k6-utils/1.0.0/index.js';

export const options = {
  stages: [
    { duration: '2m', target: 100 },  // Ramp up
    { duration: '5m', target: 100 },  // Steady
    { duration: '1m', target: 0 },    // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'],
    http_reqs: ['rate>5'],            // >300 req/s for 100 VU
    http_req_failed: ['rate<0.05'],   // <5% errors
  },
};

export default function() {
  const scenarios = ['signup', 'login', 'deposit'];
  const weights = [10, 30, 60]; // 10% signup, 30% login, 60% deposits
  
  const scenario = randomItem(scenarios, weights);
  
  switch(scenario) {
    case 'signup':
      // Call signup endpoint
      break;
    case 'login':
      // Call login endpoint
      break;
    case 'deposit':
      // Call deposit endpoint
      break;
  }
}
```

**Commandes**
```bash
# Test simple
k6 run scripts/k6/signup.js

# Test avec output Prometheus
k6 run --out experimental-prometheus-rw scripts/k6/mixed.js

# Stress test (recherche seuil)
k6 run --stage '30s:100,30s:200,30s:400' scripts/k6/mixed.js
```

---

## 2. Load Balancing (NGINX)

### 2.1 Configuration

**Fichier** `nginx.conf`
```nginx
upstream brokerx_backend {
    # Round-robin (default)
    server app-1:8080;
    server app-2:8080;
    server app-3:8080;
    server app-4:8080;
}

server {
    listen 80;
    
    location /api/ {
        proxy_pass http://brokerx_backend;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
    
    location /metrics {
        # Bloquer accès externe aux métriques
        deny all;
    }
}
```

### 2.2 Docker Compose

```yaml
nginx:
  image: nginx:alpine
  ports:
    - "80:80"
  volumes:
    - ./nginx.conf:/etc/nginx/nginx.conf
  depends_on:
    - app-1
    - app-2
    - app-3
    - app-4

app-1:
  build: .
  environment:
    - ASPNETCORE_ENVIRONMENT=Production
    - ConnectionStrings__Default=Server=mysql;Database=brokerx;User=root;Password=root;

app-2:
  build: .
  environment:
    - ASPNETCORE_ENVIRONMENT=Production
    - ConnectionStrings__Default=Server=mysql;Database=brokerx;User=root;Password=root;

app-3:
  build: .
  # ... (same as app-2)

app-4:
  build: .
  # ... (same as app-2)
```

### 2.3 Tests de scaling

**Plan de test**:
1. **Baseline (N=1)**: `docker-compose up --scale app=1` → k6 test → noter latence/RPS/erreurs
2. **N=2 instances**: `docker-compose up --scale app=2` → k6 test → noter résultats
3. **N=3 instances**: `docker-compose up --scale app=3` → k6 test → noter résultats
4. **N=4 instances**: `docker-compose up --scale app=4` → k6 test → noter résultats

**Graphique Excel/Grafana**:
- X-axis: Nombre d'instances (1, 2, 3, 4)
- Y-axis 1: Latence P95 (ms)
- Y-axis 2: Throughput (RPS)
- Y-axis 3: Taux d'erreurs (%)

**Test de tolérance aux pannes**:
```bash
# Pendant un test k6, tuer une instance
docker kill brokerx_app-2_1

# Vérifier que NGINX route vers les instances restantes
# Aucune erreur 502/503 attendue (failover automatique)
```

---

## 3. Caching (Redis)

### 3.1 Installation

```yaml
# docker-compose.yml
redis:
  image: redis:7-alpine
  ports:
    - "6379:6379"
  command: redis-server --maxmemory 256mb --maxmemory-policy allkeys-lru
```

```bash
# .NET
dotnet add package StackExchange.Redis
dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis
```

### 3.2 Configuration

**Program.cs**
```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "redis:6379";
    options.InstanceName = "BrokerX_";
});

builder.Services.AddSingleton<IDistributedCache, RedisCache>();
```

### 3.3 Implémentation cache-aside

**Exemple: GET /portfolios/{accountId}**
```csharp
public async Task<IActionResult> GetPortfolio(string accountId)
{
    var cacheKey = $"portfolio:{accountId}";
    
    // 1. Vérifier cache
    var cachedValue = await _cache.GetStringAsync(cacheKey);
    if (cachedValue != null)
    {
        return Ok(JsonSerializer.Deserialize<Portfolio>(cachedValue));
    }
    
    // 2. Cache miss → fetch DB
    var portfolio = await _portfolioRepository.GetByAccountIdAsync(accountId);
    
    // 3. Populate cache (TTL: 30s)
    var options = new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
    };
    await _cache.SetStringAsync(
        cacheKey, 
        JsonSerializer.Serialize(portfolio), 
        options);
    
    return Ok(portfolio);
}
```

### 3.4 Invalidation sur mutation

```csharp
[HttpPost("deposits")]
public async Task<IActionResult> InitiateDeposit(...)
{
    var result = await _depositUseCase.InitiateDepositAsync(...);
    
    if (result.Success)
    {
        // Invalider cache portfolio
        await _cache.RemoveAsync($"portfolio:{request.AccountId}");
    }
    
    return StatusCode(201, result);
}
```

### 3.5 Endpoints à cacher

1. **GET /portfolios/{id}** → TTL: 30s
2. **GET /market-data/***  → TTL: 5s (données volatiles)
3. **GET /reports/sales**  → TTL: 5min

### 3.6 Tests

**Baseline sans cache**:
```bash
k6 run --vus 100 --duration 30s scripts/k6/portfolio-get.js
# Noter: latence P95, RPS
```

**Avec cache**:
```bash
# Activer cache → répéter test
# Vérifier: latence P95 réduite de 50-80%
```

**Cache hit rate**:
```bash
redis-cli INFO stats | grep keyspace_hits
# Ratio hits/(hits+misses) → objectif >80%
```

---

## 4. API Gateway (KrakenD)

### 4.1 Installation

```yaml
# docker-compose.yml
krakend:
  image: devopsfaith/krakend:latest
  ports:
    - "8000:8000"
  volumes:
    - ./krakend.json:/etc/krakend/krakend.json
  command: ["run", "-c", "/etc/krakend/krakend.json"]
```

### 4.2 Configuration

**Fichier** `krakend.json`
```json
{
  "version": 3,
  "port": 8000,
  "endpoints": [
    {
      "endpoint": "/api/v1/orders",
      "method": "POST",
      "backend": [
        {
          "url_pattern": "/orders",
          "host": ["http://orders-service:8080"],
          "method": "POST"
        }
      ],
      "extra_config": {
        "auth/validator": {
          "alg": "RS256",
          "jwk_url": "http://auth-service:8080/.well-known/jwks.json"
        }
      }
    },
    {
      "endpoint": "/api/v1/portfolios/{accountId}",
      "method": "GET",
      "backend": [
        {
          "url_pattern": "/portfolios/{accountId}",
          "host": ["http://portfolio-service:8080"]
        }
      ]
    },
    {
      "endpoint": "/api/v1/reports/sales",
      "method": "GET",
      "backend": [
        {
          "url_pattern": "/reports/sales",
          "host": ["http://reporting-service:8080"]
        }
      ]
    }
  ],
  "extra_config": {
    "router": {
      "return_error_msg": true
    }
  }
}
```

### 4.3 Tests A/B

**Test 1: Appels directs**
```bash
k6 run --env SERVICE_URL=http://orders-service:8080 scripts/k6/orders.js
# Noter: latence P95, RPS
```

**Test 2: Via Gateway**
```bash
k6 run --env SERVICE_URL=http://krakend:8000 scripts/k6/orders.js
# Noter: latence P95, RPS
```

**Analyse overhead**:
- Overhead Gateway = Latence(via Gateway) - Latence(direct)
- Objectif: overhead <10ms P95

---

## 5. Microservices

### 5.1 Services à créer

1. **Orders Service**: gestion ordres, matching
2. **Portfolio Service**: portefeuilles, positions
3. **Reporting Service**: rapports, analytics
4. **Auth Service** (optionnel): authentification centralisée

### 5.2 Structure projet

```
src/
├── OrdersService/
│   ├── OrdersService.csproj
│   ├── Program.cs
│   ├── Controllers/
│   │   └── OrdersController.cs
│   ├── Domain/
│   │   ├── Order.cs
│   │   └── Fill.cs
│   └── Repositories/
│       └── OrderRepository.cs
├── PortfolioService/
│   └── ... (similaire)
└── ReportingService/
    └── ... (similaire)
```

### 5.3 Dockerfile (multi-stage)

```dockerfile
# Étape 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["OrdersService/OrdersService.csproj", "OrdersService/"]
RUN dotnet restore "OrdersService/OrdersService.csproj"
COPY . .
WORKDIR "/src/OrdersService"
RUN dotnet build "OrdersService.csproj" -c Release -o /app/build
RUN dotnet publish "OrdersService.csproj" -c Release -o /app/publish

# Étape 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "OrdersService.dll"]
```

### 5.4 Docker Compose

```yaml
orders-service:
  build: ./src/OrdersService
  ports:
    - "8081:8080"
  environment:
    - ConnectionStrings__Default=Server=mysql-orders;Database=orders;User=root;Password=root;

portfolio-service:
  build: ./src/PortfolioService
  ports:
    - "8082:8080"
  environment:
    - ConnectionStrings__Default=Server=mysql-portfolio;Database=portfolio;User=root;Password=root;

reporting-service:
  build: ./src/ReportingService
  ports:
    - "8083:8080"
  environment:
    - ConnectionStrings__Default=Server=mysql-reporting;Database=reporting;User=root;Password=root;

mysql-orders:
  image: mysql:8
  environment:
    MYSQL_ROOT_PASSWORD: root
    MYSQL_DATABASE: orders

mysql-portfolio:
  image: mysql:8
  environment:
    MYSQL_ROOT_PASSWORD: root
    MYSQL_DATABASE: portfolio

mysql-reporting:
  image: mysql:8
  environment:
    MYSQL_ROOT_PASSWORD: root
    MYSQL_DATABASE: reporting
```

### 5.5 Communication inter-services

**Synchrone (HTTP REST)**:
```csharp
// OrdersService appelle PortfolioService
var httpClient = _httpClientFactory.CreateClient();
var response = await httpClient.GetAsync("http://portfolio-service:8080/portfolios/acc-123");
var portfolio = await response.Content.ReadAsAsync<Portfolio>();
```

**Asynchrone (optionnel - évolution future)**:
- RabbitMQ ou Kafka pour événements
- Exemple: `OrderFilled` → message queue → PortfolioService update

---

## 6. UC-03 Idempotency (EN DERNIER)

### 6.1 Modifier entité PayTx

**Fichier**: `src/Domain/Model/PortefeuilleReglement/PayTx.cs`
```csharp
public class PayTx
{
    public string Id { get; set; }
    public string AccountId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Ajouter pour idempotence
    public string IdempotencyKey { get; set; }
}
```

### 6.2 Migration EF Core

```bash
dotnet ef migrations add AddIdempotencyKeyToPayTx --project src/Infrastructure.Persistence
dotnet ef database update --project src/Infrastructure.Persistence
```

**SQL généré**:
```sql
ALTER TABLE PaymentTransactions 
ADD IdempotencyKey VARCHAR(255) NOT NULL;

CREATE UNIQUE INDEX IX_PayTx_AccountId_IdempotencyKey 
ON PaymentTransactions(AccountId, IdempotencyKey);
```

### 6.3 Modifier WalletService

```csharp
public async Task<DepositResult> InitiateDepositAsync(
    string accountId, 
    decimal amount, 
    string idempotencyKey)
{
    // 1. Vérifier si transaction existe déjà
    var existing = await _payTxRepository
        .GetByAccountAndIdempotencyKeyAsync(accountId, idempotencyKey);
    
    if (existing != null)
    {
        return new DepositResult 
        { 
            Success = true, 
            TransactionId = existing.Id,
            Status = existing.Status,
            Message = "Transaction already processed (idempotent)" 
        };
    }
    
    // 2. Créer nouvelle transaction
    var payTx = new PayTx
    {
        Id = Guid.NewGuid().ToString(),
        AccountId = accountId,
        Amount = amount,
        Status = "Pending",
        IdempotencyKey = idempotencyKey,
        CreatedAt = DateTime.UtcNow
    };
    
    await _payTxRepository.AddAsync(payTx);
    
    // 3. Appeler payment adapter
    await _paymentPort.InitiatePaymentAsync(payTx);
    
    return new DepositResult 
    { 
        Success = true, 
        TransactionId = payTx.Id 
    };
}
```

### 6.4 Modifier WalletController

```csharp
[HttpPost("deposits")]
public async Task<IActionResult> InitiateDeposit(
    [FromBody] DepositRequest request,
    [FromHeader(Name = "Idempotency-Key")] string idempotencyKey)
{
    if (string.IsNullOrWhiteSpace(idempotencyKey))
    {
        return BadRequest(new { error = "Idempotency-Key header required" });
    }
    
    var result = await _depositUseCase.InitiateDepositAsync(
        request.AccountId, 
        request.Amount, 
        idempotencyKey);
    
    if (result.Success)
    {
        // 201 si nouvelle, 200 si idempotente
        var statusCode = result.Message.Contains("idempotent") ? 200 : 201;
        return StatusCode(statusCode, result);
    }
    
    return BadRequest(result);
}
```

### 6.5 Tests E2E

**Fichier**: `tests/E2E.Tests/IdempotencyTests.cs`
```csharp
[Fact]
public async Task Deposit_WithSameIdempotencyKey_ShouldReturnSameTransaction()
{
    // Arrange
    var idempotencyKey = Guid.NewGuid().ToString();
    var request = new { accountId = "acc-123", amount = 100.00 };
    
    // Act 1: première soumission
    var response1 = await _client.PostAsync(
        "/api/v1/deposits", 
        JsonContent.Create(request),
        headers: new Dictionary<string, string> 
        { 
            { "Idempotency-Key", idempotencyKey } 
        });
    
    // Act 2: retry avec même key
    var response2 = await _client.PostAsync(
        "/api/v1/deposits", 
        JsonContent.Create(request),
        headers: new Dictionary<string, string> 
        { 
            { "Idempotency-Key", idempotencyKey } 
        });
    
    // Assert
    Assert.Equal(201, (int)response1.StatusCode); // Created
    Assert.Equal(200, (int)response2.StatusCode); // OK (idempotent)
    
    var tx1 = await response1.Content.ReadFromJsonAsync<DepositResult>();
    var tx2 = await response2.Content.ReadFromJsonAsync<DepositResult>();
    
    Assert.Equal(tx1.TransactionId, tx2.TransactionId);
}
```

### 6.6 Test k6

**Fichier**: `scripts/k6/deposit-idempotency.js`
```javascript
import http from 'k6/http';
import { check } from 'k6';
import { uuidv4 } from 'https://jslib.k6.io/k6-utils/1.0.0/index.js';

export default function() {
    const idempotencyKey = uuidv4();
    const url = 'http://localhost:8080/api/v1/deposits';
    const payload = JSON.stringify({
        accountId: 'acc-123',
        amount: 100.00
    });
    const params = {
        headers: {
            'Content-Type': 'application/json',
            'Authorization': 'Bearer ' + __ENV.JWT_TOKEN,
            'Idempotency-Key': idempotencyKey
        }
    };
    
    // Première soumission
    const res1 = http.post(url, payload, params);
    check(res1, { 'first status 201': (r) => r.status === 201 });
    
    // Retry avec même key
    const res2 = http.post(url, payload, params);
    check(res2, { 
        'retry status 200': (r) => r.status === 200,
        'same transaction ID': (r) => {
            const tx1 = JSON.parse(res1.body);
            const tx2 = JSON.parse(res2.body);
            return tx1.transactionId === tx2.transactionId;
        }
    });
}
```

---

## 7. CI/CD

### 7.1 GitHub Actions

**Fichier**: `.github/workflows/ci.yml`
```yaml
name: CI

on:
  push:
    branches: [main, phase2]
  pull_request:
    branches: [main, phase2]

jobs:
  build:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 9.0.x
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --no-restore
    
    - name: Test
      run: dotnet test --no-build --verbosity normal
    
    - name: Build Docker images
      run: docker-compose build
    
    - name: Run integration tests
      run: |
        docker-compose up -d
        sleep 30
        dotnet test tests/E2E.Tests/E2E.Tests.csproj
        docker-compose down
```

### 7.2 Scripts deploy/rollback

**Fichier**: `scripts/deploy.sh`
```bash
#!/bin/bash
set -e

echo "🚀 Deploying BrokerX Phase 2..."

# Pull latest images
docker-compose pull

# Build if needed
docker-compose build

# Start services
docker-compose up -d

# Wait for health checks
sleep 30

# Verify health
curl -f http://localhost:8080/health || exit 1

echo "✅ Deployment successful"
```

**Fichier**: `scripts/rollback.sh`
```bash
#!/bin/bash
set -e

echo "⏪ Rolling back BrokerX..."

# Stop current version
docker-compose down

# Restore DB backup (if needed)
# mysql -u root -p < backups/brokerx_backup_$(date -d yesterday +%Y%m%d).sql

# Start previous version
git checkout HEAD~1
docker-compose up -d

echo "✅ Rollback successful"
```

---

## 8. Documentation

### 8.1 Arc42

**Fichier**: `docs/arc42/arc42.md`

Sections à mettre à jour:
1. **Introduction & Goals**: objectifs (observabilité, microservices)
2. **Constraints**: technologies (NGINX, Redis, KrakenD, k6)
3. **Context & Scope**: diagramme système (app + observabilité + gateway)
4. **Solution Strategy**: architecture hexagonale + microservices
5. **Building Block View**: vues 4+1 (référence vers `docs/4+1/*.puml`)
6. **Runtime View**: séquences UC-01/02/03 avec observabilité
7. **Deployment View**: référence vers `deploiement.puml`
8. **Concepts**: idempotence, caching, load balancing
9. **Design Decisions**: référence vers ADRs
10. **Quality Requirements**: NFR (latence, throughput, dispo)
11. **Risks**: cache stale data, single point of failure (DB)
12. **Glossary**: termes métier

### 8.2 ADRs (Architecture Decision Records)

**Fichier**: `docs/adr/ADR-004-NGINX-Load-Balancing.md`
```markdown
# ADR-004: NGINX pour Load Balancing

## Statut
Accepté

## Contexte
Besoin de distribuer la charge sur N instances pour atteindre NFR (≥800 req/s).

## Décision
Utiliser NGINX comme reverse proxy et load balancer (algorithme round-robin).

## Conséquences
✅ Simple à configurer
✅ Performant (C, event-driven)
✅ Failover automatique
❌ Pas de load balancing intelligent (pas de least-connections)

## Alternatives considérées
- HAProxy (plus complexe)
- Traefik (moins mature pour .NET)
```

**Fichier**: `docs/adr/ADR-005-KrakenD-API-Gateway.md`
```markdown
# ADR-005: KrakenD comme API Gateway

## Statut
Accepté

## Contexte
Migration vers microservices nécessite un point d'entrée unique (Gateway pattern).

## Décision
Utiliser KrakenD (open-source, config JSON, performant en Go).

## Conséquences
✅ Configuration déclarative (JSON)
✅ Très performant (goroutines)
✅ JWT validation intégrée
❌ Pas de GUI (config manuelle)

## Alternatives considérées
- Ocelot (.NET, mais moins mature)
- Kong (trop complexe pour nos besoins)
```

**Fichier**: `docs/adr/ADR-006-Redis-Caching-Strategy.md`
```markdown
# ADR-006: Redis pour Caching

## Statut
Accepté

## Contexte
Besoin de réduire latence GET endpoints (objectif: P95 ≤250ms).

## Décision
Utiliser Redis avec stratégie cache-aside et TTL court (5s-5min).

## Conséquences
✅ Réduit latence de 50-80%
✅ Réduit charge DB
❌ Risque de données stale (acceptable avec TTL court)
❌ Complexité invalidation

## Alternatives considérées
- Cache in-memory .NET (pas partagé entre instances)
- Memcached (moins de features que Redis)
```

### 8.3 README principal

**Fichier**: `README.md` (sections à ajouter)

```markdown
## Quick Start

### Prérequis
- Docker 24+
- Docker Compose 2.0+
- .NET 9 SDK (pour dev local)

### Lancement
```bash
# Clone repo
git clone https://github.com/theobrogrammer/projet-log-430.git
cd projet-log-430

# Start all services
docker-compose up -d

# Access services
# - App: http://localhost:8080
# - Swagger: http://localhost:8080/swagger
# - Prometheus: http://localhost:9090
# - Grafana: http://localhost:3000 (admin/admin)
# - Gateway: http://localhost:8000
```

### Tests de charge
```bash
# Install k6
brew install k6  # macOS
sudo apt-get install k6  # Ubuntu

# Run load tests
k6 run scripts/k6/mixed.js

# View results in Grafana
open http://localhost:3000
```

## Architecture

- **Style**: Hexagonal (Ports & Adapters)
- **Vues 4+1**: Voir `docs/4+1/*.puml`
- **ADRs**: Voir `docs/adr/ADR-*.md`
- **Arc42**: Voir `docs/arc42/arc42.md`

## NFR (Non-Functional Requirements)

| Métrique | État initial | Après Phase 2 |
|----------|--------------|---------------|
| Latence P95 | ≤ 500ms | ≤ 100ms |
| Throughput | ≥ 300 req/s | ≥ 1200 req/s |
| Disponibilité | ≥ 90.0% | ≥ 99.9% |

## Observabilité

- **Métriques**: Prometheus (`/metrics`)
- **Dashboards**: Grafana (4 Golden Signals)
- **Logs**: JSON structuré (Serilog)
```

### 8.4 Runbook opérationnel

**Fichier**: `docs/runbook.md`

```markdown
# Runbook — BrokerX Phase 2

## Démarrage

```bash
docker-compose up -d
```

## Arrêt

```bash
docker-compose down
```

## Monitoring

### Vérifier état services
```bash
docker-compose ps
```

### Logs
```bash
# Tous services
docker-compose logs -f

# Service spécifique
docker-compose logs -f app-1

# Dernières 100 lignes
docker-compose logs --tail 100 app-1
```

### Métriques
- Prometheus: http://localhost:9090
- Grafana: http://localhost:3000
- Query exemple: `rate(http_requests_total[1m])`

## Pannes courantes

### Service ne démarre pas
```bash
# Vérifier logs
docker-compose logs <service-name>

# Recréer container
docker-compose up -d --force-recreate <service-name>
```

### Base de données inaccessible
```bash
# Vérifier MySQL
docker-compose exec mysql mysql -u root -p -e "SHOW DATABASES;"

# Recréer DB
docker-compose down -v
docker-compose up -d
```

### Latence élevée
1. Vérifier Grafana dashboard (P95 latency)
2. Vérifier Redis cache hit rate: `redis-cli INFO stats`
3. Vérifier load balancer: logs NGINX

### Mémoire saturée
```bash
# Vérifier utilisation
docker stats

# Redémarrer services
docker-compose restart
```

## Backup & Restore

### Backup DB
```bash
docker-compose exec mysql mysqldump -u root -p brokerx > backups/brokerx_$(date +%Y%m%d).sql
```

### Restore DB
```bash
docker-compose exec -T mysql mysql -u root -p brokerx < backups/brokerx_20251026.sql
```

## Scaling

### Augmenter instances app
```bash
docker-compose up -d --scale app=4
```

### Réduire instances app
```bash
docker-compose up -d --scale app=2
```
```

---

## 9. Livraison

### 9.1 Checklist détaillée (Phase 2)

#### Étape 2a: Observabilité & Performance

**Observabilité - Prometheus**
1. Installer `prometheus-net` et `prometheus-net.AspNetCore` dans `Infrastructure.Web.csproj`
2. Ajouter `app.UseHttpMetrics()` et `endpoints.MapMetrics()` dans `Program.cs`
3. Créer `prometheus.yml` avec configuration scrape (target: `api:8080/metrics`, interval: 15s)
4. Ajouter service Prometheus dans `docker-compose.yml` (image: `prom/prometheus:latest`, port: 9090)
5. Vérifier métriques exposées: `curl http://localhost:8080/metrics`
6. Accéder interface Prometheus: http://localhost:9090 et tester requête `http_requests_received_total`

**Observabilité - Grafana**
1. Ajouter service Grafana dans `docker-compose.yml` (image: `grafana/grafana:latest`, port: 3000)
2. Créer volume `grafana/provisioning/datasources/prometheus.yml` pour datasource auto
3. Créer dashboard 4 Golden Signals: Latency (histogram_quantile), Traffic (rate), Errors (rate), Saturation (process metrics)
4. Importer dashboard: Grafana UI → Create → Import → ID 10427 (ASP.NET Core)
5. Personnaliser panels: P50/P95/P99 latency, RPS par endpoint, error rate %
6. Exporter dashboard JSON dans `grafana/dashboards/4-golden-signals.json`

**Observabilité - Logs structurés**
1. Installer `Serilog.AspNetCore`, `Serilog.Sinks.Console`, `Serilog.Formatting.Compact`
2. Configurer Serilog dans `Program.cs`: `UseSerilog()` avec `CompactJsonFormatter`
3. Ajouter enrichers: `WithThreadId()`, `WithMachineName()`, `WithExceptionDetails()`
4. Logger événements métier: `Log.Information("UC01_SIGNUP_SUCCESS", new { ClientId, Email })`
5. Tester format JSON: `docker-compose logs api | jq .`
6. Configurer niveaux par namespace: `Application.*: Information`, `Microsoft.*: Warning`

**Observabilité - k6 (tests de charge)**
1. Installer k6: `sudo apt-get install k6` ou télécharger binaire
2. Créer `scripts/k6/signup.js`: scenario constant VUs (10 VUs, 1min, endpoint POST /api/v1/signup)
3. Créer `scripts/k6/login.js`: scenario ramping VUs (0→50→0, 3min, endpoint POST /api/v1/auth/login)
4. Créer `scripts/k6/deposit.js`: scenario idempotency (20 VUs, répéter même key, vérifier déduplication)
5. Créer `scripts/k6/mixed.js`: scenario mixte 60% read / 40% write
6. Ajouter thresholds k6: `http_req_duration: ['p(95)<500']`, `http_req_failed: ['rate<0.05']`
7. Exécuter baseline: `k6 run --out json=results/baseline.json scripts/k6/mixed.js`
8. Sauvegarder résultats dans `resultats-k6/baseline/` avec screenshots Grafana

**Performance - NGINX Load Balancer**
1. Créer `nginx.conf` avec upstream `backend` (4 serveurs: api_1, api_2, api_3, api_4 sur port 8080)
2. Configurer algorithme: `least_conn` avec `max_fails=3` et `fail_timeout=30s`
3. Ajouter `proxy_pass http://backend`, `proxy_set_header X-Real-IP`, `X-Forwarded-For`, `Host`
4. Activer keepalive: `keepalive 32` dans upstream
5. Ajouter service NGINX dans `docker-compose.yml` (port: 80, volume: nginx.conf)
6. Modifier `docker-compose.yml`: déployer 4 réplicas API (`deploy.replicas: 4` ou 4 services distincts)
7. Tester LB: `for i in {1..10}; do curl -s http://localhost/health | jq .hostname; done` (doit alterner)
8. Tester failover: stopper 1 instance API, vérifier requêtes toujours routées
9. **Tests k6 comparatifs**: répéter tests pour N = 1, 2, 3, 4 instances avec `scripts/k6/mixed.js`
10. Créer graphiques comparatifs (X=instances, Y=latence/RPS/erreurs/saturation)
11. Tester tolérance aux pannes: `docker stop brokerx-api-2` pendant test k6, vérifier dégradation gracieuse

**Performance - Redis Cache**
1. Ajouter service Redis dans `docker-compose.yml` (image: `redis:7-alpine`, port: 6379)
2. Installer `StackExchange.Redis` dans `Infrastructure.Web.csproj`
3. Créer `ICachePort` dans `Domain/Ports.Outbound` avec `GetAsync`, `SetAsync`, `RemoveAsync`
4. Implémenter `RedisCacheAdapter` dans `Infrastructure.Adapters/Cache/`
5. Injecter dans `AuthService`: cache token validation, MFA policies (TTL: 5min)
6. Injecter dans `WalletService`: cache account balance (TTL: 1min)
7. Ajouter logs: `Log.Information("CACHE_HIT")` et `Log.Information("CACHE_MISS")`
8. Configurer eviction policy dans docker-compose: `command: redis-server --maxmemory 256mb --maxmemory-policy allkeys-lru`
9. Monitorer hit rate: `redis-cli info stats | grep keyspace_hits` (cible: >80%)

**Performance - Tests Scaling**
1. Créer script `scripts/test-scaling.sh`: boucle N=1,2,3,4 instances
2. Pour chaque N: `docker-compose up --scale api=N`, attendre 30s, lancer k6 mixed 100 VUs 2min
3. Capturer métriques: latency P95, RPS total, error rate, CPU/RAM par instance
4. Générer graphiques: `latency_vs_instances.png`, `rps_vs_instances.png`, `errors_vs_instances.png`
5. Sauvegarder résultats dans `resultats-k6/scaling/N1/`, `N2/`, `N3/`, `N4/`
6. Analyser: identifier point optimal (ex: N=3 si N=4 n'améliore pas P95)

#### Étape 2b: API Gateway & Microservices

**API Gateway - KrakenD**
1. Créer `krakend.json`: configuration gateway (port: 8000, timeout: 5s, cache: true)
2. Définir endpoints: `/api/v1/signup` → backend `http://api:8080/api/v1/signup`
3. Configurer rate limiting: `max_rate: 100` (100 req/s par client)
4. Activer circuit breaker: `max_errors: 5`, `interval: 10s`, `timeout: 5s`, `max_connections: 100`
5. Ajouter service KrakenD dans `docker-compose.yml` (image: `devopsfaith/krakend:latest`, port: 8000)
6. Tester routing: `curl http://localhost:8000/api/v1/signup` doit appeler backend
7. Monitorer: KrakenD expose métriques Prometheus sur `/metrics`

**Microservices - Extraction Services**
1. Identifier bounded contexts: **Orders** (ordres trading), **Portfolio** (positions/balance), **Reporting** (analytics)
2. Créer projet `src/Services.Orders/`: controllers `OrdersController` (CreateOrder, GetOrder, CancelOrder)
3. Créer projet `src/Services.Portfolio/`: controllers `PortfolioController` (GetBalance, GetPositions, GetHistory)
4. Créer projet `src/Services.Reporting/`: controllers `ReportingController` (GetMetrics, ExportTransactions)
5. Extraire logique domaine: déplacer entités `Order`, `Position` vers services respectifs
6. Configurer communication: REST synchrone ou message queue (RabbitMQ/Redis Pub/Sub) pour événements
7. Containeriser chaque service: Dockerfile + ajout dans docker-compose.yml (ports: 8081, 8082, 8083)
8. Configurer KrakenD: router `/orders/*` → Orders, `/portfolio/*` → Portfolio, `/reports/*` → Reporting
9. Tester isolation: stopper service Orders, vérifier Portfolio toujours accessible

**Microservices - Tests A/B**
1. Créer script k6 `scripts/k6/direct-vs-gateway.js`: scénario split 50/50 calls directs vs via gateway
2. Mesurer overhead latency: `latency_gateway - latency_direct` (attendre <50ms)
3. Mesurer throughput: comparer RPS max direct vs gateway sous 200 VUs
4. Mesurer error handling: injecter 500 errors backend, vérifier circuit breaker gateway
5. Générer rapport: `comparison-direct-vs-gateway.md` avec métriques + graphiques
6. Sauvegarder résultats dans `resultats-k6/gateway-comparison/`

#### Étape 2c: Idempotency UC-03 (EN DERNIER)

**UC-03 - Idempotency Implementation**
1. Ajouter propriété `IdempotencyKey` (string, 36 chars) dans entité `PayTx` (Domain/Model)
2. Créer migration EF Core: `dotnet ef migrations add AddIdempotencyKeyToPayTx`
3. Ajouter contrainte unique: `builder.HasIndex(p => new { p.AccountId, p.IdempotencyKey }).IsUnique()`
4. Modifier `IDepositUseCase.RequestAsync()`: ajouter paramètre `string idempotencyKey`
5. Implémenter logique dans `WalletService`: 
   - Chercher transaction existante: `_repo.GetByAccountAndIdempotencyKeyAsync(accountId, key)`
   - Si trouvée: retourner résultat existant (`DepositResult` avec status/txId)
   - Si non trouvée: créer nouvelle transaction avec `IdempotencyKey = key`
6. Modifier `WalletController.Deposit()`: déjà présent `[FromHeader(Name = "Idempotency-Key")]`, juste passer au service
7. Valider: générer GUID si header absent, rejeter si format invalide (400 Bad Request)

**UC-03 - Tests Idempotency**
1. Créer test E2E `IdempotencyTests.cs`:
   - Test 1: Même key 2x → même txId retourné, balance incrémenté 1x seulement
   - Test 2: Keys différentes → 2 txId distincts, balance incrémenté 2x
   - Test 3: Key absente → 400 Bad Request (si obligatoire) ou génération auto GUID
2. Créer test k6 `scripts/k6/idempotency-stress.js`: 50 VUs, même key 1000x, vérifier 1 seul crédit
3. Vérifier logs: `DUPLICATE_IDEMPOTENCY_KEY` loggé quand détecté
4. Tester retry: simuler timeout 1ère requête, retry avec même key doit réussir

#### Étape 2d: CI/CD

**GitHub Actions - Pipeline**
1. Créer `.github/workflows/ci.yml`: trigger sur `push` et `pull_request` branches `main`, `phase2`
2. Job `build`: steps `actions/checkout`, `actions/setup-dotnet@v3` (dotnet 9.0.x), `dotnet restore`, `dotnet build`
3. Job `test-unit`: `dotnet test tests/Domain.Tests --no-build`
4. Job `test-e2e`: `dotnet test tests/E2E.Tests --no-build` (nécessite docker-compose up en background)
5. Job `lint`: `dotnet format --verify-no-changes` (install dotnet-format si absent)
6. Ajouter badge dans `README.md`: `![Build Status](https://github.com/theobrogrammer/projet-log-430/workflows/CI/badge.svg)`
7. Configurer secrets: `DOCKER_USERNAME`, `DOCKER_PASSWORD` si push images registry

**Scripts Deploy/Rollback**
1. Créer `scripts/deploy.sh`: 
   - Pull latest code: `git pull origin phase2`
   - Build images: `docker-compose build --no-cache`
   - Tag version: `docker tag brokerx-api:latest brokerx-api:v2.0`
   - Deploy: `docker-compose up -d`
   - Health check: `curl --retry 5 --retry-delay 10 http://localhost:8080/health`
2. Créer `scripts/rollback.sh`:
   - Stop current: `docker-compose down`
   - Restore previous version: `docker tag brokerx-api:v1.0 brokerx-api:latest`
   - Start: `docker-compose up -d`
   - Verify: `curl http://localhost:8080/health`
3. Tester rollback: déployer version cassée, rollback, vérifier service opérationnel
4. Ajouter logs: `echo "[$(date)] Deployment started"` dans chaque script

#### Étape 2e: Documentation

**Arc42 - Mise à jour**
1. Section 1 (Exigences): ajouter UC-03 complet avec idempotency
2. Section 2 (Contraintes): ajouter contraintes NFR (P95 <100ms, RPS >1200, uptime 99.9%)
3. Section 3 (Contexte): ajouter diagramme microservices (Orders, Portfolio, Reporting)
4. Section 5 (Vue ensemble): mettre à jour diagrammes 4+1 avec NGINX, Redis, KrakenD
5. Section 6 (Vue implémentation): ajouter projets Services.*, structure microservices
6. Section 7 (Vue déploiement): mettre à jour docker-compose avec tous services Phase 2
7. Section 8 (Concepts transversaux): ajouter sections Observabilité, Caching, Rate Limiting, Circuit Breaking
8. Générer PDF/HTML: convertir `arc42.md` avec Pandoc ou export depuis outil

**ADRs - Nouvelles décisions**
1. Créer `docs/adr/ADR-004-NGINX-LoadBalancer.md`:
   - Contexte: besoin scaling horizontal, failover
   - Décision: NGINX least_conn avec 3 replicas
   - Alternatives considérées: Traefik, HAProxy
   - Conséquences: +complexité config, +résilience, +throughput
2. Créer `docs/adr/ADR-005-KrakenD-Gateway.md`:
   - Contexte: besoin API Gateway pour microservices
   - Décision: KrakenD pour performance, rate limiting, circuit breaker
   - Alternatives: Kong, Ocelot, Spring Cloud Gateway
   - Conséquences: Single point of failure (mitigé par replicas), overhead latency <50ms
3. Créer `docs/adr/ADR-006-Redis-Cache.md`:
   - Contexte: requêtes répétées (validation token, balance checks)
   - Décision: Redis in-memory cache, TTL 1-5min
   - Alternatives: Memcached, in-process cache (IMemoryCache)
   - Conséquences: +latency read queries, -load DB, invalidation cache complexe

**README & Runbook**
1. Mettre à jour `README.md` principal:
   - Section Architecture: diagramme Phase 2 avec tous composants
   - Section Quickstart: `docker-compose up -d` avec attente 3min (tous services)
   - Section Endpoints: documenter nouveaux microservices `/orders`, `/portfolio`, `/reports`
   - Section Monitoring: accès Prometheus (9090), Grafana (3000), KrakenD metrics
2. Créer `docs/runbook.md`:
   - Procédure démarrage: ordre services (DB → Redis → API → NGINX → KrakenD)
   - Procédure arrêt: `docker-compose down -v` (attention: -v efface volumes)
   - Troubleshooting: connexions refused → vérifier ordre démarrage, port conflicts
   - Monitoring: dashboards Grafana à surveiller, alertes Prometheus (error rate >1%)
   - Backup: sauvegarder volumes MySQL, Redis snapshots
   - Rollback: procédure complète avec scripts

#### Étape 2f: Livraison finale

**Tests & Validation**
1. Exécuter `dotnet test` sur tous projets: Domain.Tests, Application.Tests, Infrastructure.Tests, E2E.Tests (100% pass)
2. Exécuter tous scripts k6: signup, login, deposit, mixed, scaling, idempotency, gateway-comparison (thresholds verts)
3. Vérifier Prometheus collecte: accéder http://localhost:9090/targets → tous endpoints UP
4. Vérifier Grafana dashboard: 4 Golden Signals affichent données temps réel
5. Vérifier logs JSON: `docker-compose logs api | jq .timestamp` affiche timestamps structurés
6. Vérifier NGINX LB: `curl http://localhost/health` alterne entre instances
7. Vérifier Redis cache: `redis-cli info stats | grep keyspace_hits` >80%
8. Vérifier KrakenD: `curl http://localhost:8000/api/v1/signup` route vers backend
9. Vérifier microservices: accès http://localhost:8081/orders/health, 8082/portfolio/health, 8083/reports/health
10. Vérifier idempotency: test manuel 2 POST même key → 1 seul débit
11. Vérifier pipeline CI: tous jobs verts sur GitHub Actions

**Code & Commits**
1. Commit atomiques par feature: "feat: add Prometheus metrics", "feat: implement Redis cache", etc.
2. Tags Git: `git tag v2.0-phase2 && git push origin v2.0-phase2`
3. Branch protection: PR required pour merge sur `main`, 1 reviewer minimum
4. Code review: vérifier conventions C# (.editorconfig), naming, commentaires XML
5. Push final: `git push origin phase2` avec tous commits squashés si nécessaire

**Rapport PDF**
1. Rédiger section 1 Introduction: contexte BrokerX, objectifs Phase 2, architecture cible
2. Rédiger section 2 Vues 4+1: inclure 5 diagrammes PlantUML (exports PNG haute résolution)
3. Rédiger section 3 Implémentation: décrire UC-01, UC-02, UC-03 avec extraits code clés
4. Rédiger section 4 Observabilité: screenshots Prometheus queries, Grafana dashboards, logs JSON
5. Rédiger section 5 Performance: graphiques k6 (baseline, scaling N=1..4, comparaison avant/après cache)
6. Rédiger section 6 Microservices: architecture diagram, KrakenD config, tests A/B direct vs gateway
7. Rédiger section 7 CI/CD: pipeline GitHub Actions, scripts deploy/rollback, procédure rollback
8. Rédiger section 8 Conclusion: NFR atteints (tableau métrique avant/après), leçons apprises, améliorations futures
9. Export PDF: nom fichier `rapport-phase2-brokerx-2025.pdf`, <20 pages

**Archive livrable**
1. Créer structure: `brokerx-phase2/` avec sous-dossiers src, tests, scripts, docs, configs
2. Inclure README.md avec instructions démarrage rapide (<5min pour build & run)
3. Inclure docker-compose.yml complet (tous services Phase 2)
4. Inclure tous scripts k6 dans `scripts/k6/`
5. Inclure configs: `nginx.conf`, `krakend.json`, `prometheus.yml`, dashboards Grafana JSON
6. Inclure résultats k6: `resultats-k6/` avec tous runs sauvegardés (JSON + screenshots)
7. Inclure documentation: Arc42 (MD+HTML), ADRs, runbook
8. Inclure rapport PDF final
9. Compresser: `zip -r brokerx-phase2.zip brokerx-phase2/` (exclure bin/, obj/, node_modules/)
10. Vérifier taille: <50MB (exclure logs volumineux, images Docker)

**Test reproductibilité**
1. VM vierge Ubuntu 24.04: installer Docker, Docker Compose, Git uniquement
2. Chronomètre: démarrer timer
3. Clone repo: `git clone https://github.com/theobrogrammer/projet-log-430.git && cd projet-log-430`
4. Checkout branch: `git checkout phase2`
5. Build & Start: `docker-compose up -d --build`
6. Attente: `sleep 180` (3min pour tous services ready)
7. Health check: `curl http://localhost:8080/health` → 200 OK
8. Smoke test: `k6 run --vus 10 --duration 30s scripts/k6/mixed.js` → thresholds pass
9. Vérifier accès: Swagger (http://localhost:8080/swagger), Prometheus (9090), Grafana (3000)
10. Chronomètre: arrêter timer → **objectif: <30min total**
11. Documenter: ajouter section "Installation" dans README avec temps mesuré

### 9.2 Rapport PDF

**Structure**:
1. **Introduction** (1 page) — Objectifs, architecture cible
2. **Vues 4+1** (5 pages) — Logique, Processus, Déploiement, Développement, Scénarios
3. **Implémentation** (5 pages) — UC-01, UC-02, UC-03 + idempotency
4. **Observabilité** (3 pages) — Prometheus, Grafana, Logs JSON
5. **Performance** (5 pages) — k6, NGINX, Redis, graphiques scaling
6. **Microservices** (4 pages) — Architecture, KrakenD, tests A/B
7. **CI/CD** (2 pages) — GitHub Actions, deploy/rollback
8. **Conclusion** (1 page) — NFR atteints, leçons, améliorations

### 9.3 Archive livrable

**Fichier**: `brokerx-phase2.zip`

```
brokerx-phase2/
├── README.md
├── docker-compose.yml
├── src/
├── tests/
├── scripts/
│   ├── k6/
│   ├── deploy.sh
│   └── rollback.sh
├── docs/
│   ├── 4+1/
│   ├── adr/
│   ├── arc42/
│   └── runbook.md
├── nginx.conf
├── krakend.json
├── prometheus.yml
├── grafana/
├── rapport-phase2.pdf
└── resultats-k6/
```

### 9.4 Test de reproductibilité

**Objectif**: VM vierge → code fonctionnel en <30min

```bash
# 1. Clone (2 min)
git clone https://github.com/theobrogrammer/projet-log-430.git
cd projet-log-430

# 2. Start (7 min)
docker-compose up -d

# 3. Wait (3 min)
sleep 180

# 4. Verify (1 min)
curl http://localhost:8080/health

# 5. Smoke test (2 min)
k6 run --vus 10 --duration 30s scripts/k6/mixed.js

# TOTAL: ~15 min ✅
```

---

## Annexe: NFR Cibles

| Métrique | État initial | Après Phase 2 |
|----------|--------------|---------------|
| **Latence P95** | ≤ 500 ms | ≤ 100 ms |
| **Throughput** | ≥ 300 req/s | ≥ 1200 req/s |
| **Disponibilité** | ≥ 90.0% | ≥ 99.9% |
| **Cache hit rate** | — | ≥ 80% |
| **Error rate** | <5% | <1% |

---

## Annexe: Vues 4+1

| Vue | Fichier | Description |
|-----|---------|-------------|
| **Logique** | `docs/4+1/logique.puml` | Classes domaine, bounded contexts |
| **Processus** | `docs/4+1/processus.puml` | Threads, IPC, concurrency |
| **Déploiement** | `docs/4+1/deploiement.puml` | Docker Compose, 3 états |
| **Développement** | `docs/4+1/developpement.puml` | Projets .NET, dépendances |
| **Scénarios** | `docs/4+1/scénarios/*.puml` | UC-01, UC-02, UC-03 |

---

**Dernière mise à jour**: 26 octobre 2025  
**Statut**: Phase 1 ✅ | Phase 2 en cours ⏳
