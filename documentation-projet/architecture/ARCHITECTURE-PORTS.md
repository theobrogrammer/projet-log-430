# 🔌 Architecture des Ports - BrokerX

**Date** : 28 octobre 2025  
**Projet** : BrokerX - Plateforme de courtage

---

## 📋 Vue d'ensemble de l'évolution des ports

### Phase 2a - Observabilité + Load Balancing (État actuel)

**Mode Normal:**
```
┌─────────────┐
│   Client    │
└──────┬──────┘
       │
       │ HTTP :5000
       ▼
┌─────────────────┐
│  BrokerX API    │
│   (ASP.NET)     │◄──── Prometheus scrape :5000/metrics
│   Port: 5000    │
└────────┬────────┘
         │
         ▼
    ┌─────────┐
    │  MySQL  │
    │  :3307  │
    └─────────┘
```

**Mode Load Balancing:**
```
┌─────────────┐
│   Client    │
└──────┬──────┘
       │
       │ HTTP :8090
       ▼
┌────────────────────┐
│   NGINX LB         │
│   Port: 8090       │
│   (least_conn)     │
└──────┬─────────────┘
       │
       ├────────┬────────┬────────┐
       │        │        │        │
       ▼        ▼        ▼        ▼
   ┌─────┐  ┌─────┐  ┌─────┐  ┌─────┐
   │API-1│  │API-2│  │API-3│  │API-4│
   │:8080│  │:8080│  │:8080│  │:8080│ ◄─ Prometheus scrape
   └──┬──┘  └──┬──┘  └──┬──┘  └──┬──┘
      │        │        │        │
      └────────┴────────┴────────┘
                 │
                 ▼
            ┌─────────┐
            │  MySQL  │
            │  :3307  │
            └─────────┘
```

### Phase 2b - API Gateway (Futur)

```
┌─────────────┐
│   Client    │
└──────┬──────┘
       │
       │ HTTP :8080
       ▼
┌────────────────────┐
│ KrakenD Gateway    │ ◄─ Prometheus scrape
│   Port: 8080       │
│  (rate limit,      │
│   auth, routing)   │
└──────┬─────────────┘
       │
       │ HTTP :8090
       ▼
┌────────────────────┐
│   NGINX LB         │
│   Port: 8090       │
│   (least_conn)     │
└──────┬─────────────┘
       │
       ├────────┬────────┬────────┐
       ▼        ▼        ▼        ▼
   ┌─────┐  ┌─────┐  ┌─────┐  ┌─────┐
   │API-1│  │API-2│  │API-3│  │API-4│
   │:8080│  │:8080│  │:8080│  │:8080│
   └──┬──┘  └──┬──┘  └──┬──┘  └──┬──┘
      └────────┴────────┴────────┘
                 │
                 ▼
            ┌─────────┐
            │  MySQL  │
            │  :3307  │
            └─────────┘
```

---

## 🎯 Répartition des ports

### Phase 2a (Actuelle)

| Service | Port externe | Port interne | Accès | Statut |
|---------|--------------|--------------|-------|--------|
| **Apache (système)** | 80 | - | Système | ✅ Existant |
| **API BrokerX** | 5000 | 5000 | Public | ✅ Actif |
| **API Instance 1** | 5001 | 8080 | Debug | ✅ Actif (mode LB) |
| **API Instance 2** | 5002 | 8080 | Debug | ✅ Actif (mode LB) |
| **API Instance 3** | 5003 | 8080 | Debug | ✅ Actif (mode LB) |
| **API Instance 4** | 5004 | 8080 | Debug | ✅ Actif (mode LB) |
| **NGINX LB** | 8090 | 80 | Public (mode LB) | ✅ Actif |
| **MySQL** | 3307 | 3306 | Interne | ✅ Actif |
| **Prometheus** | 9090 | 9090 | Admin | ✅ Actif |
| **Grafana** | 3000 | 3000 | Admin | ✅ Actif |

**Notes:**
- Port 80 occupé par Apache (système)
- Port 8080 **réservé** pour KrakenD (Phase 2b)
- NGINX utilise port 8090 pour éviter conflit
- API instances exposent 5001-5004 uniquement pour debug

### Phase 2b (Future)

| Service | Port externe | Port interne | Accès | Statut |
|---------|--------------|--------------|-------|--------|
| **KrakenD Gateway** | 8080 | 8080 | Public | ⏳ Phase 2b |
| **NGINX LB** | 8090 | 80 | Via gateway | ✅ Actif |
| **API Instance 1** | 5001 | 8080 | Debug | ✅ Actif |
| **API Instance 2** | 5002 | 8080 | Debug | ✅ Actif |
| **API Instance 3** | 5003 | 8080 | Debug | ✅ Actif |
| **API Instance 4** | 5004 | 8080 | Debug | ✅ Actif |
| **Redis Cache** | 6379 | 6379 | Interne | ⏳ Phase 2b |
| **MySQL** | 3307 | 3306 | Interne | ✅ Actif |
| **Prometheus** | 9090 | 9090 | Admin | ✅ Actif |
| **Grafana** | 3000 | 3000 | Admin | ✅ Actif |

**Notes:**
- KrakenD sera le point d'entrée public unique
- NGINX reste en backend pour load balancing
- Redis pour caching (sessions, token validation)

---

## 🔍 Détails par service

### 1. API BrokerX (ASP.NET Core 9.0)

**Phase 2a - Mode Normal** :
- **Port** : 5000
- **Accès** : http://localhost:5000
- **Usage** : Tests et développement sans load balancing

**Phase 2a - Mode Load Balancing** :
- **Ports internes** : 8080 (non exposés directement)
- **Ports debug** : 5001-5004 (exposition externe pour debug uniquement)
- **Accès public** : http://localhost:8090 (via NGINX)
- **Algorithme LB** : least_conn (connexions actives les plus faibles)

**Endpoints** :
  - `/health` - Health check
  - `/nginx-health` - Health check NGINX
  - `/metrics` - Métriques Prometheus
  - `/api/v1/signup` - Inscription (UC-01)
  - `/api/v1/auth/login` - Authentification (UC-02)
  - `/api/v1/wallet/deposit` - Dépôt (UC-03)

**Phase 2b (futur)** :
- **Accès** : Uniquement via KrakenD Gateway (8080)
- **Bénéfices** :
  - Scaling horizontal (N=1,2,3,4 instances)
  - Load balancing automatique
  - Isolation des instances
  - Rate limiting au niveau gateway

---

### 2. NGINX Load Balancer

**Phase 2a (actuel)** :
- **Port** : 8090
- **Raison** : Port 80 occupé par Apache, 8080 réservé pour KrakenD
- **Rôle** :
  - Répartir la charge entre 4 instances API
  - Health checks automatiques (max_fails=3, fail_timeout=30s)
  - Connection pooling (keepalive 32)
- **Algorithme** : `least_conn` (minimise connexions actives)

**Configuration nginx.conf** :
```nginx
upstream backend {
    least_conn;
    
    server brokerx-api-1:8080 max_fails=3 fail_timeout=30s;
    server brokerx-api-2:8080 max_fails=3 fail_timeout=30s;
    server brokerx-api-3:8080 max_fails=3 fail_timeout=30s;
    server brokerx-api-4:8080 max_fails=3 fail_timeout=30s;
    
    keepalive 32;
}
```

**Tests:**
```bash
# Démarrer avec LB
docker compose -f docker-compose.yml -f docker-compose.lb.yml up -d

# Tester distribution
for i in {1..10}; do curl -s http://localhost:8090/health; echo; done
```

---

### 3. KrakenD API Gateway

**Phase 2b (futur)** :
- **Port** : 8080
- **Raison** : Port standard pour API Gateway (convention)
- **Rôle** :
  - Point d'entrée unique pour les clients
  - Load balancing entre les instances API
  - Rate limiting
  - Agrégation de réponses (si microservices)
  - Circuit breaker
  - Métriques centralisées

**Configuration** :
```json
{
  "endpoints": [
    {
      "endpoint": "/api/v1/signup",
      "method": "POST",
      "backend": [
        {
          "url_pattern": "/api/v1/signup",
          "host": ["http://api:5001", "http://api:5002", "http://api:5003"]
        }
      ]
    }
  ]
}
```

---

### 3. NGINX Load Balancer (optionnel)

**Alternative à KrakenD** :
- **Port** : 5000 (ou 8080 si pas de KrakenD)
- **Rôle** : Répartir la charge entre les instances API
- **Algorithmes** : Round-robin, least connections, IP hash

**Configuration nginx.conf** :
```nginx
upstream brokerx_api {
    least_conn;
    server api1:5001;
    server api2:5002;
    server api3:5003;
    server api4:5004;
}

server {
    listen 5000;
    location / {
        proxy_pass http://brokerx_api;
    }
}
```

---

### 4. Prometheus (Monitoring)

**Port** : 9090
- **Raison** : Port par défaut Prometheus
- **Accès** : http://localhost:9090
- **Scraping** :
  - Phase 2a : `api:5000/metrics`
  - Phase 2b : `krakend:8080/metrics` + `api1:5001/metrics`, `api2:5002/metrics`, etc.

---

### 5. Grafana (Dashboards)

**Port** : 3000
- **Raison** : Port par défaut Grafana
- **Accès** : http://localhost:3000
- **Datasource** : Prometheus (http://prometheus:9090)
- **Dashboards** :
  - 4 Golden Signals (Latence, Trafic, Erreurs, Saturation)
  - Métriques par endpoint
  - Comparaison avant/après load balancing

---

### 6. MySQL (Base de données)

**Port externe** : 3307  
**Port interne** : 3306
- **Raison port 3307** : Éviter conflit avec MySQL local
- **Accès** :
  - Via API : `mysql:3306` (réseau Docker interne)
  - Via client externe : `localhost:3307`

---

### 7. Redis (Cache) - Phase 2b

**Port** : 6379
- **Raison** : Port par défaut Redis
- **Accès** : Interne uniquement (via réseau Docker)
- **Usage** :
  - Cache des sessions JWT
  - Cache des données de marché
  - Rate limiting distributé

---

## ⚠️ Erreur corrigée : Pourquoi on a changé de 8080 à 5000

### Problème initial

Dans un premier temps, on avait migré l'API vers le port 8080, mais c'était **prématuré** :

```
❌ MAUVAIS (avant correction)
Client → API:8080 → MySQL
```

### Pourquoi c'était incorrect ?

1. **Le port 8080 est conventionnellement réservé aux API Gateways** (KrakenD, Kong, Zuul)
2. **ASP.NET Core utilise 5000 par défaut** (standard .NET)
3. **On n'a pas encore de gateway** (Phase 2b)
4. **Confusion architecturale** : L'API ne devrait pas être sur le port gateway

### Solution appliquée

**Phase 2a** (maintenant) :
```
✅ CORRECT
Client → API:5000 → MySQL
         ↓
     Prometheus scrape
```

**Phase 2b** (futur) :
```
✅ CORRECT
Client → KrakenD:8080 → [API:5001, API:5002, API:5003] → MySQL
                  ↓
              Prometheus scrape
```

---

## 📊 Labels Prometheus par endpoint

### Problème : Identifier l'origine des requêtes

**Question** : "Ne serait-il pas utile de savoir dans Prometheus les requêtes viennent de quel appel API ?"

**Réponse** : ✅ **C'est déjà implémenté !**

### Labels automatiques (prometheus-net.AspNetCore)

Chaque métrique HTTP inclut automatiquement :

| Label | Description | Exemple |
|-------|-------------|---------|
| `code` | Code HTTP | `200`, `400`, `404`, `500` |
| `method` | Méthode HTTP | `GET`, `POST`, `PUT`, `DELETE` |
| `controller` | Controller ASP.NET | `Signup`, `Auth`, `Wallet` |
| `action` | Action du controller | `Signup`, `Login`, `Deposit` |
| `endpoint` | Route complète | `api/v1/signup`, `/health` |

### Exemple de métrique complète

```
http_requests_received_total{
  code="400",
  method="POST",
  controller="Signup",
  action="Signup",
  endpoint="api/v1/signup"
} = 3
```

**Cela signifie** : 3 requêtes POST vers `/api/v1/signup` ont retourné une erreur 400 (validation failed)

---

## 🎯 Requêtes PromQL utiles par endpoint

### 1. Toutes les requêtes vers l'inscription
```promql
http_requests_received_total{controller="Signup"}
```

### 2. Toutes les requêtes vers l'authentification
```promql
http_requests_received_total{controller="Auth"}
```

### 3. Taux d'erreurs sur le dépôt
```promql
rate(http_requests_received_total{controller="Wallet", code=~"4..|5.."}[1m])
```

### 4. Latence P95 par endpoint
```promql
histogram_quantile(0.95, 
  sum by (endpoint, le) (
    rate(http_request_duration_seconds_bucket[5m])
  )
)
```

### 5. Top 3 endpoints les plus lents
```promql
topk(3, 
  histogram_quantile(0.95, 
    sum by (endpoint, le) (
      rate(http_request_duration_seconds_bucket[5m])
    )
  )
)
```

### 6. Requêtes/seconde par endpoint
```promql
sum by (endpoint) (rate(http_requests_received_total[1m]))
```

### 7. Pourcentage d'erreurs par endpoint
```promql
sum by (endpoint) (rate(http_requests_received_total{code=~"4..|5.."}[1m]))
/
sum by (endpoint) (rate(http_requests_received_total[1m]))
* 100
```

---

## ✅ Checklist de migration Phase 2a → Phase 2b

### Étape 1 : Vérifier l'état actuel
- [x] API sur port 5000 ✅
- [x] Prometheus scrape `api:5000/metrics` ✅
- [x] Labels controller/action fonctionnels ✅
- [ ] Grafana installé
- [ ] Dashboards 4 Golden Signals créés

### Étape 2 : Ajouter KrakenD Gateway
- [ ] Créer `krakend.json` (configuration endpoints)
- [ ] Ajouter service `krakend` dans `docker-compose.yml` (port 8080)
- [ ] Configurer backend vers `api:5000`
- [ ] Tester : `curl http://localhost:8080/api/v1/health`

### Étape 3 : Scaler les instances API
- [ ] Modifier `docker-compose.yml` : `api` → `api1`, `api2`, `api3`, `api4`
- [ ] Changer ports : 5001, 5002, 5003, 5004
- [ ] Mettre à jour KrakenD backends : `["api1:5001", "api2:5002", ...]`
- [ ] Mettre à jour Prometheus scrape targets

### Étape 4 : Ajouter NGINX Load Balancer (optionnel)
- [ ] Créer `nginx.conf` avec upstream
- [ ] Ajouter service `nginx` dans `docker-compose.yml`
- [ ] Configurer load balancing vers instances API

### Étape 5 : Ajouter Redis Cache
- [ ] Ajouter service `redis` dans `docker-compose.yml`
- [ ] Installer `StackExchange.Redis` dans API
- [ ] Implémenter cache-aside pattern
- [ ] Tester amélioration performance

### Étape 6 : Tests de charge comparatifs
- [ ] Installer k6
- [ ] Écrire scénarios : `signup-load.js`, `auth-load.js`, `deposit-load.js`
- [ ] Tester N=1 instance : mesurer latence P95, RPS, erreurs
- [ ] Tester N=2 instances : comparer résultats
- [ ] Tester N=4 instances : graphiques comparatifs dans Grafana

---

## 📝 Commandes utiles

### Vérifier les ports utilisés
```bash
docker compose ps
```

### Tester un endpoint avec curl
```bash
# Health check
curl http://localhost:5000/health

# Métriques Prometheus
curl http://localhost:5000/metrics

# Signup (erreur validation volontaire)
curl -X POST http://localhost:5000/api/v1/signup \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com"}'
```

### Vérifier les métriques dans Prometheus
```bash
# Via API
curl 'http://localhost:9090/api/v1/query?query=http_requests_received_total{controller="Signup"}'

# Via UI
# http://localhost:9090 → Graph → Requête PromQL
```

### Reconstruire après changement de port
```bash
docker compose down
docker compose build
docker compose up -d
```

---

## 🎓 Résumé

| Aspect | Phase 2a (actuelle) | Phase 2b (future) |
|--------|---------------------|-------------------|
| **Point d'entrée** | API directe (5000) | KrakenD Gateway (8080) |
| **Instances API** | 1 instance (5000) | 4 instances (5001-5004) |
| **Load Balancing** | Non | Oui (KrakenD ou NGINX) |
| **Caching** | Non | Oui (Redis 6379) |
| **Monitoring** | Prometheus + Grafana | Prometheus + Grafana |
| **Labels métriques** | ✅ controller, action, endpoint | ✅ + instance_id |

---

**✅ Configuration actuelle validée** : API sur port 5000, labels Prometheus fonctionnels

**🎯 Prochaine étape** : Installer Grafana (port 3000) et créer dashboards 4 Golden Signals
