# 📘 Runbook Opérationnel - BrokerX

**Guide rapide pour démarrer, tester et monitorer la plateforme.**

---

## 🚀 Démarrage Rapide

```bash
# Démarrer tout
docker compose up -d

# Vérifier que ça fonctionne
curl http://localhost:5000/health
```

**✅ Services actifs :**
- **API** : http://localhost:5000
- **Grafana** : http://localhost:3000 (admin/admin)
- **Prometheus** : http://localhost:9090

⏱️ **Temps de démarrage** : ~10-15 secondes

---

## 📊 Monitoring

### Voir les logs

```bash
# Logs formatés (lisibles)
./tests/scripts/logs-readable.sh

# Récupérer code OTP pour tests
docker logs brokerx-api 2>&1 | grep "\[OTP\]" | tail -1 | grep -oP 'code=\K\d+'
```

### Dashboard Grafana (recommandé)

**Ouvrir** : http://localhost:3000 (admin/admin)

**Métriques visibles :**
- ⏱️ **Latence** P95 (objectif: < 500ms)
- 📈 **Trafic** (requêtes/seconde)  
- ❌ **Erreurs** (objectif: < 5%)
- 💾 **Cache** hit rate (> 80%)

### Métriques brutes

```bash
# Voir toutes les métriques
curl http://localhost:5000/metrics

# Métriques cache uniquement  
curl http://localhost:5000/metrics | grep cache_
```

---

## 🧪 Tests de Performance

### Test rapide (1 min)

```bash
# Test simple signup
k6 run scripts/k6/signup.js
```

### Tests Phase 2a : Load Balancing + Redis

```bash
# Test complet scaling (N=1,2,3,4 instances) - 30 min
./tests/scripts/test-scaling.sh comparison

# Test Redis cache - 2 min
./tests/scripts/test-redis-metrics.sh
```

**📊 Résultats attendus :**
- Load Balancing: 22-23 RPS constant (N=1 à 4)
- Redis Cache: Hit rate > 0%, latence < 5ms
- Tous les résultats dans `tests/resultats-k6/` et `tests/resultats-redis/`

---

## 🔧 Commandes Utiles

```bash
# Redémarrer l'API
docker compose restart api

# Voir les logs
docker compose logs -f api

# Accès MySQL
docker exec -it brokerx-mysql mysql -uroot -proot123 brokerx

# Accès Redis
docker exec -it brokerx-redis redis-cli

# Vérifier clés cache
docker exec brokerx-redis redis-cli keys "*"
```

---

## 🚨 Problèmes Courants

**API ne démarre pas** :
```bash
docker compose logs api --tail 20
docker compose down && docker compose up -d
```

**Redis non connecté** :
```bash
# Vérifier Redis actif
docker ps | grep redis

# Arrêter autres Redis en conflit
docker stop $(docker ps -q --filter "name=redis" --filter "name!=brokerx")

# Rebuild API
docker compose build api && docker compose up -d api
```

**Grafana pas de données** :
```bash
# Vérifier Prometheus scrape les 5 instances
curl http://localhost:9090/api/v1/targets | grep "brokerx-api"
```

---

## 📚 Documentation Détaillée

- **Tests Load Balancing** : `tests/resultats-k6/scaling-comparison/`
- **Tests Redis Cache** : `tests/resultats-redis/VALIDATION-REDIS-CACHE.md`
- **Scripts** : `tests/scripts/`
