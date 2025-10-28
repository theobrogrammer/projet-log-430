# 📊 Guide Interactif - Observabilité Phase 2 (Étape 2a)

**Projet** : BrokerX  
**Date** : 27 octobre 2025  
**Objectif** : Maîtriser Prometheus, Grafana et k6 via leurs interfaces web

---

## 🎯 Vue d'ensemble

Ce guide te permet d'explorer **manuellement** les outils d'observabilité :

| Outil | Rôle | Interface | Statut |
|-------|------|-----------|--------|
| **Prometheus** | Collecte et stockage des métriques | http://localhost:9090 | ✅ Installé |
| **Grafana** | Visualisation des métriques (dashboards) | http://localhost:3000 | ⏳ À installer |
| **k6** | Tests de charge et performance | CLI + Grafana Cloud (optionnel) | ⏳ À installer |

---

## 📋 Table des matières

1. [Prometheus - Exploration manuelle](#partie-1-prometheus)
2. [Grafana - Création de dashboards](#partie-2-grafana) ⏳
3. [k6 - Tests de charge](#partie-3-k6) ⏳

---

# Partie 1 : Prometheus - Exploration manuelle

## 🚀 Installation (complétée)

### Étape 1 : Packages .NET installés
```bash
dotnet add package prometheus-net --version 8.2.1
dotnet add package prometheus-net.AspNetCore --version 8.2.1
```

**Fichier modifié** : `src/Infrastructure.Web/Infrastructure.Web.csproj`

---

### Étape 2 : Configuration dans Program.cs

**Ajouts effectués** :
```csharp
using Prometheus;

// Middleware pour collecter les métriques HTTP
app.UseHttpMetrics();

// Endpoint pour exposer les métriques
app.MapMetrics();
```

**Fichier modifié** : `src/Infrastructure.Web/Program.cs`

---

### Étape 3 : Fichier prometheus.yml créé

**Configuration** :
- Job : `brokerx-api`
- Cible : `api:8080/metrics`
- Intervalle de scraping : 15 secondes
- Timeout : 10 secondes

**Fichier créé** : `prometheus.yml` (racine du projet)

---

### Étape 4 : Service Prometheus ajouté dans docker-compose.yml

**Service ajouté** :
```yaml
prometheus:
  container_name: brokerx-prometheus
  image: prom/prometheus:latest
  ports:
    - "9090:9090"
  volumes:
    - ./prometheus.yml:/etc/prometheus/prometheus.yml:ro
    - prometheus_data:/prometheus
```

**Changements additionnels** :
- Port API : 5000 → 8080 (standardisation)
- Ajout du réseau `brokerx-network`
- Volume persistant `prometheus_data`

---

## 🌐 Exploration de l'interface Prometheus

### 📍 Étape 1 : Accède à Prometheus

**Ouvre ton navigateur** et va sur :
```
http://localhost:9090
```

**✅ Ce que tu devrais voir** :
- Une barre de recherche en haut (pour les requêtes PromQL)
- Des onglets : **Graph**, **Alerts**, **Status**
- Un champ de texte avec placeholder "Expression (press Shift+Enter for newlines)"

---

### 📍 Étape 2 : Vérifie les cibles (Targets)

1. **Clique sur** : `Status` (menu du haut)
2. **Clique sur** : `Targets`

**✅ Ce que tu devrais voir** :

```
Endpoint: brokerx-api (1/1 up)
┌─────────────────────────────────────────────────────────┐
│ State:  UP (en vert)                                     │
│ Labels: instance="api:8080"                             │
│         job="brokerx-api"                                │
│ Last Scrape: 0.010s ago                                  │
│ Scrape Duration: 0.010533s                               │
│ Error: (vide)                                            │
└─────────────────────────────────────────────────────────┘
```

**💡 Interprétation** :
- **UP (vert)** = Prometheus collecte avec succès les métriques
- **Last Scrape** = Dernière collecte (mise à jour toutes les 15s)
- **Scrape Duration** = Temps pour récupérer les métriques (~10ms = très rapide)
- **Error vide** = Aucun problème de connexion

---

### 📍 Étape 3 : Explore les métriques disponibles

1. **Retourne sur** : `Graph` (onglet en haut)
2. **Dans le champ de recherche, commence à taper** : `http_`

**✅ Ce que tu devrais voir (autocomplétion)** :
```
http_request_duration_seconds
http_request_duration_seconds_bucket
http_request_duration_seconds_count
http_request_duration_seconds_sum
http_requests_in_progress
http_requests_received_total
```

**💡 Explication des métriques** :

| Métrique | Type | Description |
|----------|------|-------------|
| `http_requests_received_total` | Counter | Nombre total de requêtes HTTP reçues |
| `http_request_duration_seconds` | Histogram | Distribution des temps de réponse |
| `http_requests_in_progress` | Gauge | Nombre de requêtes en cours d'exécution |

---

### 📍 Étape 4 : Première requête PromQL - Compteur de requêtes

1. **Efface le champ de recherche**
2. **Tape** : `http_requests_received_total`
3. **Clique sur** : `Execute` (ou appuie sur **Enter**)
4. **Clique sur l'onglet** : `Graph` (à côté de "Table")

**✅ Ce que tu devrais voir** :

**Vue Table** :
```
Element                                                              Value
http_requests_received_total{code="200", method="GET", ...}        28
http_requests_received_total{code="404", method="GET", ...}        1
http_requests_received_total{code="200", method="GET", endpoint="/health"}  17
```

**Vue Graph** :
- Des courbes colorées montrant l'évolution dans le temps
- Chaque ligne = une série avec labels différents

**💡 Lecture du graphique** :
- **Axe X** : Temps (dernière heure par défaut)
- **Axe Y** : Nombre total de requêtes (compteur cumulatif)
- **Légendes colorées** : Chaque combinaison de labels (code + method + endpoint)

**🔍 IMPORTANT : Labels disponibles**

Chaque métrique inclut automatiquement ces labels :
- `code` : Code HTTP (200, 400, 404, 500, etc.)
- `method` : Méthode HTTP (GET, POST, PUT, DELETE)
- `controller` : Nom du controller ASP.NET (ex: "Signup", "Auth", "Wallet")
- `action` : Nom de l'action du controller (ex: "Signup", "Login", "Deposit")
- `endpoint` : Route complète (ex: "api/v1/signup", "/health")

**Exemple de métrique complète** :
```
http_requests_received_total{
  code="400",
  method="POST",
  controller="Signup",
  action="Signup",
  endpoint="api/v1/signup"
} = 3
```

💡 **Cela permet de filtrer par endpoint spécifique** :
```promql
# Toutes les requêtes vers l'inscription
http_requests_received_total{controller="Signup"}

# Toutes les requêtes vers l'authentification
http_requests_received_total{controller="Auth"}

# Taux d'erreurs sur le dépôt
rate(http_requests_received_total{controller="Wallet", code=~"4..|5.."}[1m])
```

---

### 📍 Étape 5 : Génère du trafic pour voir les métriques évoluer

**Ouvre un terminal** et exécute :
```bash
# Génère 10 requêtes vers /health (1 par seconde)
for i in {1..10}; do 
  curl -s http://localhost:8080/health > /dev/null
  echo "✓ Requête $i envoyée"
  sleep 1
done
```

**Retourne dans Prometheus** :
1. **Attends 15-20 secondes** (pour que Prometheus collecte les nouvelles métriques)
2. **Clique à nouveau sur** : `Execute`
3. **Observe** : La courbe monte ! 📈

**✅ Résultat attendu** :
- Le compteur `http_requests_received_total{code="200"}` a augmenté de **+10**
- Le graphique montre une montée en escalier

---

### 📍 Étape 6 : Filtre par endpoint API spécifique

**Maintenant qu'on comprend les labels, explorons par endpoint !**

**Tape** :
```promql
http_requests_received_total{controller="Signup"}
```

**Clique sur** : `Execute`

**✅ Ce que tu devrais voir** :
```
http_requests_received_total{code="400", method="POST", controller="Signup", action="Signup", endpoint="api/v1/signup"} = 1
```

**💡 Interprétation** :
- On voit **uniquement** les requêtes vers le controller Signup
- Ici : 1 requête avec erreur 400 (validation failed)

---

**Teste d'autres endpoints** :

**1. Requêtes d'authentification** :
```promql
http_requests_received_total{controller="Auth"}
```

**2. Requêtes vers le wallet** :
```promql
http_requests_received_total{controller="Wallet"}
```

**3. Health check (pas de controller)** :
```promql
http_requests_received_total{endpoint="/health"}
```

**4. Toutes les erreurs 4xx/5xx** :
```promql
http_requests_received_total{code=~"4..|5.."}
```

---

### 📍 Étape 7 : Calcule le taux de requêtes par seconde

**Efface la requête précédente et tape** :
```promql
rate(http_requests_received_total[1m])
```

**Clique sur** : `Execute`

**✅ Ce que tu devrais voir** :
```
Element                                              Value
rate(http_requests_received_total{code="200", ...})  0.166
```

**💡 Interprétation** :
- `0.166` requêtes/seconde = environ **1 requête toutes les 6 secondes**
- `rate()` calcule la **vitesse** (dérivée) sur la période `[1m]` (1 minute glissante)
- Plus tu génères de trafic, plus cette valeur augmente

**🔍 Différence avec `http_requests_received_total` ?**
| Requête | Type | Utilité |
|---------|------|---------|
| `http_requests_received_total` | Compteur absolu | Total depuis le démarrage (toujours croissant) |
| `rate(...[1m])` | Vitesse | Requêtes/seconde **en ce moment** (dernière minute) |

---

### 📍 Étape 8 : Calcule la latence moyenne des requêtes

**Tape cette requête** :
```promql
rate(http_request_duration_seconds_sum[1m]) / rate(http_request_duration_seconds_count[1m])
```

**Clique sur** : `Execute`

**✅ Ce que tu devrais voir** :
```
Value: 0.005
```

**💡 Interprétation** :
- `0.005` secondes = **5 millisecondes** (très rapide !)
- Cette requête calcule : temps total ÷ nombre de requêtes = **latence moyenne**

**🎯 Benchmarks de référence** :
| Latence | Évaluation | Exemple |
|---------|------------|---------|
| < 10ms | ⚡ Excellent | API REST simple, cache |
| 10-50ms | ✅ Bon | Requête DB simple |
| 50-200ms | ⚠️ Acceptable | Requête DB complexe |
| > 200ms | ❌ Lent | Appels externes, timeouts |

---

### 📍 Étape 9 : Calcule la latence P95 (95e percentile)

**Tape cette requête** :
```promql
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))
```

**Clique sur** : `Execute`

**✅ Ce que tu devrais voir** :
```
Value: 0.020
```

**💡 Interprétation** :
- `0.020` = **20 millisecondes**
- **95% des requêtes** sont plus rapides que 20ms
- Seules 5% des requêtes dépassent cette valeur

**🔍 Pourquoi P95 au lieu de moyenne ?**
| Métrique | Avantage | Inconvénient |
|----------|----------|--------------|
| **Moyenne** | Simple à comprendre | Cachée par les valeurs extrêmes |
| **P50 (Médiane)** | Représente l'expérience typique | Ignore 50% des utilisateurs |
| **P95** | ✅ Montre les "pires cas" (sans extrêmes) | Plus complexe à calculer |
| **P99** | Montre vraiment les cas problématiques | Très sensible aux outliers |

---

### 📍 Étape 10 : Explore les métriques système - Mémoire

**Tape** :
```promql
process_working_set_bytes / 1024 / 1024
```

**Clique sur** : `Execute`

**✅ Ce que tu devrais voir** :
```
Value: 119
```

**💡 Interprétation** :
- **119 MB** = Mémoire RAM utilisée par l'application .NET
- Cette valeur évolue avec le GC (Garbage Collector)

**Génère du trafic et observe** :
```bash
# Dans un terminal
for i in {1..100}; do curl -s http://localhost:8080/health > /dev/null; done
```

**Rafraîchis Prometheus** : La mémoire peut augmenter temporairement, puis redescendre après un GC.

---

### 📍 Étape 11 : Explore les métriques système - CPU

**Tape** :
```promql
rate(process_cpu_seconds_total[1m])
```

**Clique sur** : `Execute`

**✅ Ce que tu devrais voir** :
```
Value: 0.05
```

**💡 Interprétation** :
- `0.05` = **5% d'utilisation CPU**
- `1.0` = 100% (1 cœur complet)
- `2.0` = 200% (2 cœurs complets)

**🔥 Génère une charge CPU** :
```bash
# Dans un terminal - 500 requêtes rapides
for i in {1..500}; do curl -s http://localhost:8080/health > /dev/null; done &
```

**Observe dans Prometheus** : Le CPU va monter temporairement (ex: 0.30 = 30%)

---

### 📍 Étape 12 : Garbage Collector .NET - Collections GC

**Tape** :
```promql
rate(dotnet_collection_count_total{generation="0"}[1m])
```

**Clique sur** : `Execute`

**✅ Résultat attendu** :
```
Value: 0.016
```

**💡 Interprétation** :
- `0.016` collections/seconde = environ **1 collection GC gen0 par minute**
- Génération 0 = objets éphémères (nettoyage rapide)

**🔍 Compare les générations** :
```promql
dotnet_collection_count_total
```

**Ce que tu verras** :
```
dotnet_collection_count_total{generation="0"}  15  ← Fréquent
dotnet_collection_count_total{generation="1"}   3  ← Moins fréquent
dotnet_collection_count_total{generation="2"}   0  ← Rare (complet)
```

**💡 Règle d'or** :
- Gen 0 fréquent = **normal** (objets temporaires)
- Gen 2 fréquent = ⚠️ **problème** (fuites mémoire, objets long-lived mal utilisés)

---

### 📍 Étape 13 : Change la période d'observation

**En haut à droite de l'interface, tu vois** :
```
[ - 5m ] [ Graph ] [ Evaluation time: ... ]
```

**Clique sur `-` ou change la période** :
- `5m` = 5 minutes
- `15m` = 15 minutes
- `1h` = 1 heure
- `3h` = 3 heures
- `Custom` = Personnalisé

**Teste avec** : `1h` (1 heure)

**✅ Résultat** : Tu vois l'historique sur la dernière heure (utile pour détecter des pics)

---

## 🎯 Exercices pratiques interactifs

### Exercice 1 : Génère du trafic et observe en temps réel

**Préparation** :
1. Ouvre Prometheus : `http://localhost:9090`
2. Onglet **Graph**
3. Requête : `rate(http_requests_received_total[1m])`
4. Change le range : **5m** (5 minutes)
5. Active le **refresh automatique** (en haut à droite, clique sur l'icône refresh)

**Génère du trafic** (dans un terminal) :
```bash
while true; do 
  curl -s http://localhost:5000/health > /dev/null
  sleep 2
done
```

**✅ Observe** :
- La courbe monte progressivement
- Le taux se stabilise autour de `0.5` req/s (1 requête toutes les 2 secondes)

**Arrête** : `Ctrl+C`

**✅ Observe** :
- La courbe redescend progressivement vers 0 (fenêtre glissante de 1 minute)

---

### Exercice 2 : Analyse par endpoint API

**Objectif** : Comprendre le trafic par endpoint

**Dans un terminal, génère des requêtes variées** :
```bash
# 5 requêtes signup (avec erreur validation volontaire)
for i in {1..5}; do
  curl -s -X POST http://localhost:5000/api/v1/signup \
    -H "Content-Type: application/json" \
    -d '{"email":"test@example.com"}' > /dev/null
  echo "✓ Signup request $i"
done

# 10 requêtes health check
for i in {1..10}; do
  curl -s http://localhost:5000/health > /dev/null
  echo "✓ Health check $i"
done
```

**Dans Prometheus, analyse** :

**1. Vue globale** :
```promql
http_requests_received_total
```
**✅ Tu verras** : Plusieurs séries (health, signup, etc.)

**2. Uniquement signup** :
```promql
http_requests_received_total{controller="Signup"}
```
**✅ Résultat** : 5 requêtes avec code 400

**3. Uniquement health** :
```promql
http_requests_received_total{endpoint="/health"}
```
**✅ Résultat** : 10 requêtes avec code 200

**4. Taux de requêtes par endpoint** :
```promql
sum by (endpoint) (rate(http_requests_received_total[1m]))
```
**✅ Résultat** : Requêtes/sec groupées par endpoint

**5. Top 3 endpoints les plus utilisés** :
```promql
topk(3, sum by (endpoint) (http_requests_received_total))
```

---

### Exercice 3 : Détecte les erreurs par endpoint

### Exercice 3 : Détecte les erreurs par endpoint

**Objectif** : Identifier quel endpoint a le plus d'erreurs

**Dans un terminal, génère des erreurs variées** :
```bash
# Erreurs 404 (endpoint inexistant)
for i in {1..5}; do
  curl -s http://localhost:5000/api/inexistant > /dev/null
  echo "✓ Erreur 404 générée ($i/5)"
done

# Erreurs 400 (validation signup)
for i in {1..3}; do
  curl -s -X POST http://localhost:5000/api/v1/signup \
    -H "Content-Type: application/json" \
    -d '{"invalid":"data"}' > /dev/null
  echo "✓ Erreur 400 générée ($i/3)"
done
```

**Dans Prometheus, analyse** :

**1. Toutes les erreurs 4xx/5xx** :
```promql
http_requests_received_total{code=~"4..|5.."}
```

**2. Taux d'erreurs par endpoint** :
```promql
sum by (endpoint, code) (rate(http_requests_received_total{code=~"4.."}[1m]))
```

**3. Pourcentage d'erreurs** :
```promql
sum(rate(http_requests_received_total{code=~"4..|5.."}[1m])) 
/ 
sum(rate(http_requests_received_total[1m])) 
* 100
```
**✅ Résultat** : Pourcentage d'erreurs (ex: 15 = 15% d'erreurs)

**4. Endpoint avec le plus d'erreurs** :
```promql
topk(1, sum by (endpoint) (http_requests_received_total{code=~"4..|5.."}))
```

---

### Exercice 4 : Simule une charge importante

**Terminal 1 - Génère 200 requêtes rapides** :
```bash
for i in {1..200}; do 
  curl -s http://localhost:5000/health > /dev/null
done
echo "✓ 200 requêtes envoyées"
```

**Dans Prometheus, observe simultanément** :

**1. Taux de requêtes** :
```promql
rate(http_requests_received_total[1m])
```
**✅ Tu devrais voir un pic**

**2. Latence P95** :
```promql
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[1m]))
```
**✅ La latence peut augmenter légèrement sous charge**

**3. CPU** :
```promql
rate(process_cpu_seconds_total[1m])
```
**✅ Pic temporaire d'utilisation CPU**

**4. Mémoire** :
```promql
process_working_set_bytes / 1024 / 1024
```
**✅ Peut augmenter puis redescendre après GC**

---

## 📚 Concepts clés Prometheus

### Types de métriques


### Types de métriques

| Type | Comportement | Cas d'usage | Exemple |
|------|--------------|-------------|---------|
| **Counter** | ✅ Ne peut qu'augmenter | Compteurs (requêtes, erreurs) | `http_requests_received_total` |
| **Gauge** | ↕️ Peut monter/descendre | Valeurs instantanées (mémoire, CPU) | `http_requests_in_progress` |
| **Histogram** | 📊 Distribution en buckets | Latences, tailles de réponses | `http_request_duration_seconds` |
| **Summary** | 📈 Quantiles précalculés | Alternative à histogram | Moins courant |

---

### Fonctions PromQL essentielles

| Fonction | Usage | Exemple | Résultat |
|----------|-------|---------|----------|
| `rate()` | Vitesse de changement (par seconde) | `rate(http_requests_total[1m])` | Requêtes/sec |
| `increase()` | Augmentation totale | `increase(http_requests_total[5m])` | +120 requêtes |
| `histogram_quantile()` | Calcule percentiles | `histogram_quantile(0.95, ...)` | P95 latence |
| `sum()` | Agrège plusieurs séries | `sum(http_requests_total)` | Total global |
| `avg()` | Moyenne | `avg(http_request_duration)` | Latence moy |
| `max()` | Maximum | `max(process_working_set_bytes)` | Pic mémoire |
| `by()` | Groupe par labels | `sum by (code) (http_requests_total)` | Total par code HTTP |

---

### Sélecteurs de labels

**Syntaxe** :
```promql
metric_name{label1="value1", label2="value2"}
```

**Opérateurs** :
- `=` : Égalité exacte
- `!=` : Différent de
- `=~` : Regex match
- `!~` : Regex not match

**Exemples** :
```promql
# Requêtes réussies seulement
http_requests_received_total{code="200"}

# Toutes les erreurs 4xx et 5xx
http_requests_received_total{code=~"4..|5.."}

# Tout sauf GET
http_requests_received_total{method!="GET"}

# Plusieurs conditions
http_requests_received_total{code="200", method="POST"}
```

---

## 📊 Métriques disponibles dans BrokerX

### Métriques HTTP (automatiques via prometheus-net.AspNetCore)

| Métrique | Type | Description | Labels |
|----------|------|-------------|--------|
| `http_requests_received_total` | Counter | Nombre total de requêtes HTTP | `code`, `method`, `controller`, `action` |
| `http_request_duration_seconds` | Histogram | Durée des requêtes HTTP | `code`, `method`, `controller`, `action` |
| `http_requests_in_progress` | Gauge | Requêtes en cours d'exécution | `method`, `controller`, `action` |

**Buckets de latence configurés** :
```
0.001s, 0.005s, 0.01s, 0.05s, 0.1s, 0.5s, 1s, 5s, 10s
```

---

### Métriques système .NET

| Métrique | Type | Description |
|----------|------|-------------|
| `dotnet_collection_count_total` | Counter | Nombre de GC par génération (0, 1, 2) |
| `process_cpu_seconds_total` | Counter | Temps CPU total (user + system) |
| `process_working_set_bytes` | Gauge | Mémoire utilisée (working set) |
| `process_private_memory_bytes` | Gauge | Mémoire privée du processus |
| `process_virtual_memory_bytes` | Gauge | Mémoire virtuelle |
| `process_num_threads` | Gauge | Nombre de threads |
| `process_open_handles` | Gauge | Nombre de handles ouverts (Windows/Linux) |
| `process_start_time_seconds` | Gauge | Timestamp de démarrage |

---

## 🎓 Quiz de validation Prometheus

**Réponds à ces questions en utilisant uniquement Prometheus** :

### Question 1 : Combien de requêtes ont été traitées depuis le démarrage ?
```promql
http_requests_received_total
```
**Réponse attendue** : Un nombre absolu (ex: 248)

---

### Question 2 : Combien de requêtes/seconde en ce moment ?
```promql
rate(http_requests_received_total[1m])
```
**Réponse attendue** : Un nombre décimal (ex: 0.166 = ~1 req toutes les 6s)

---

### Question 3 : Quelle est la latence moyenne actuelle ?
```promql
rate(http_request_duration_seconds_sum[1m]) / rate(http_request_duration_seconds_count[1m])
```
**Réponse attendue** : En secondes (ex: 0.005 = 5ms)

---

### Question 4 : Quel est le taux d'erreurs 404 ?
```promql
rate(http_requests_received_total{code="404"}[1m])
```
**Réponse attendue** : Req/sec (ex: 0.033 = 2 erreurs/min)

---

### Question 5 : Combien de mémoire utilise l'application ?
```promql
process_working_set_bytes / 1024 / 1024
```
**Réponse attendue** : En MB (ex: 119)

---

### Question 6 : Quelle est la latence P99 (99e percentile) ?
```promql
histogram_quantile(0.99, rate(http_request_duration_seconds_bucket[5m]))
```
**Réponse attendue** : En secondes (ex: 0.025 = 25ms)

---

## ✅ Validation finale Prometheus

| Critère | Comment vérifier | Statut |
|---------|------------------|--------|
| Interface accessible | http://localhost:9090 | ✅ |
| Target UP | Status → Targets → brokerx-api (vert) | ✅ |
| Métriques collectées | Graph → `http_requests_received_total` | ✅ |
| PromQL fonctionne | Execute une requête | ✅ |
| Graphiques s'affichent | Onglet Graph | ✅ |
| Trafic détecté | Génère requêtes → compteur augmente | ✅ |

---

# Partie 2 : Grafana - Création de dashboards

## ✅ Installation (complétée)

### Étape 1 : Service Grafana ajouté dans docker-compose.yml

**Configuration** :
```yaml
grafana:
  container_name: brokerx-grafana
  image: grafana/grafana:latest
  ports:
    - "3000:3000"
  volumes:
    - grafana_data:/var/lib/grafana
    - ./grafana/provisioning:/etc/grafana/provisioning
  environment:
    - GF_SECURITY_ADMIN_USER=admin
    - GF_SECURITY_ADMIN_PASSWORD=admin
```

**Fichier modifié** : `docker-compose.yml`

---

### Étape 2 : Datasource Prometheus configuré automatiquement

**Fichier créé** : `grafana/provisioning/datasources/prometheus.yml`

**Configuration** :
- URL : `http://prometheus:9090`
- Access : proxy (via réseau Docker)
- Intervalle : 15s
- Default datasource : Oui

---

### Étape 3 : Grafana démarré

**Commande** :
```bash
docker compose up -d grafana
```

**Vérification** :
```bash
docker compose ps
# brokerx-grafana UP sur port 3000 ✅
```

---

## 🌐 Exploration de l'interface Grafana

### 📍 Étape 1 : Première connexion à Grafana

**Ouvre ton navigateur** et va sur :
```
http://localhost:3000
```

**Page de login** :
- **Username** : `admin`
- **Password** : `admin`

**Clique sur** : `Log in`

**⚠️ Changement de mot de passe** :
- Grafana te demandera de changer le mot de passe
- Tu peux cliquer sur **"Skip"** pour garder `admin/admin` (environnement dev)

**✅ Ce que tu devrais voir** :
- Page d'accueil Grafana avec "Welcome to Grafana"
- Menu latéral gauche avec icônes (Home, Search, Dashboards, etc.)

---

### 📍 Étape 2 : Vérifie le datasource Prometheus

1. **Clique sur** : L'icône **⚙️ Configuration** (roue dentée) dans le menu gauche
2. **Clique sur** : `Data sources`

**✅ Ce que tu devrais voir** :
```
┌────────────────────────────────────────────┐
│ Prometheus                                  │
│ Type: Prometheus                            │
│ URL: http://prometheus:9090                 │
│ Default: ✓                                  │
└────────────────────────────────────────────┘
```

3. **Clique sur** : `Prometheus` (pour voir les détails)
4. **Scroll en bas et clique sur** : `Save & test`

**✅ Résultat attendu** :
```
✓ Successfully queried the Prometheus API.
✓ Prometheus version: 2.54.1
```

**💡 Interprétation** :
- Grafana peut communiquer avec Prometheus
- Le datasource est prêt à être utilisé

---

### 📍 Étape 3 : Explore les métriques avec l'explorateur

1. **Clique sur** : L'icône **🧭 Explore** (boussole) dans le menu gauche

**✅ Ce que tu devrais voir** :
- Un éditeur de requête PromQL
- Un sélecteur de métriques en haut
- Des options de visualisation (Graph, Table, Logs, etc.)

---

### 📍 Étape 4 : Première requête - Compteur de requêtes HTTP

**Dans le query builder** :
1. **Clique sur** : `Metrics browser` (ou le champ de sélection)
2. **Tape** : `http_requests_received_total`
3. **Sélectionne** : `http_requests_received_total`
4. **Clique sur** : `Run query` (bouton bleu en haut à droite)

**✅ Ce que tu devrais voir** :
- Un graphique avec plusieurs courbes colorées
- Légende en bas montrant les labels (code, method, endpoint)
- Valeurs actuelles sur le côté droit

**💡 Astuce** :
- Change la période en haut à droite : `Last 5 minutes`, `Last 15 minutes`, etc.
- Active le refresh automatique : Clique sur l'icône refresh → `5s`, `10s`, etc.

---

### 📍 Étape 5 : Visualise le taux de requêtes/seconde

**Efface la requête précédente et tape** :
```promql
rate(http_requests_received_total[1m])
```

**Clique sur** : `Run query`

**✅ Ce que tu devrais voir** :
- Des courbes montrant la **vitesse** (requêtes/seconde)
- Valeurs décimales (ex: 0.166 = 1 requête toutes les 6 secondes)

**Génère du trafic** (dans un terminal) :
```bash
while true; do curl -s http://localhost:5000/health > /dev/null; sleep 2; done
```

**Observe dans Grafana** :
- Les courbes montent en temps réel ! 📈
- Le taux se stabilise autour de 0.5 req/s

**Arrête** : `Ctrl+C` dans le terminal

---

### 📍 Étape 6 : Filtre par endpoint spécifique

**Tape cette requête** :
```promql
rate(http_requests_received_total{endpoint="/health"}[1m])
```

**✅ Résultat** : Uniquement les requêtes vers `/health`

**Teste d'autres filtres** :

**1. Uniquement signup** :
```promql
rate(http_requests_received_total{controller="Signup"}[1m])
```

**2. Uniquement erreurs 4xx/5xx** :
```promql
rate(http_requests_received_total{code=~"4..|5.."}[1m])
```

**3. Toutes les requêtes POST** :
```promql
rate(http_requests_received_total{method="POST"}[1m])
```

---

### 📍 Étape 7 : Visualise la latence P95

**Tape** :
```promql
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))
```

**Clique sur** : `Run query`

**✅ Ce que tu devrais voir** :
- Une courbe montrant la latence du 95e percentile
- Valeur en secondes (ex: 0.020 = 20ms)

**💡 Interprétation** :
- 95% des requêtes sont plus rapides que cette valeur
- Si ça monte soudainement → problème de performance

---

### 📍 Étape 8 : Change le type de visualisation

**En haut de l'interface Explore** :
1. **Clique sur** : `Table` (à côté de Graph)

**✅ Résultat** : Vue tableau avec valeurs exactes

**Autres visualisations disponibles** :
- **Graph** : Courbes temporelles (par défaut)
- **Table** : Tableau de valeurs
- **Stat** : Valeur unique (idéal pour dashboards)
- **Gauge** : Jauge visuelle

---

### 📍 Étape 9 : Crée ton premier dashboard

1. **Clique sur** : L'icône **📊 Dashboards** dans le menu gauche
2. **Clique sur** : `New` → `New Dashboard`
3. **Clique sur** : `Add visualization`
4. **Sélectionne** : `Prometheus` (datasource)

**Dans l'éditeur de panel** :
1. **Tape la requête** :
   ```promql
   rate(http_requests_received_total[1m])
   ```
2. **Change le titre** (en haut) : "Requêtes HTTP/sec"
3. **Clique sur** : `Apply` (en haut à droite)

**✅ Tu viens de créer ton premier panel !**

---

### 📍 Étape 10 : Ajoute un deuxième panel - Latence P95

1. **Clique sur** : `Add` → `Visualization` (en haut à droite)
2. **Requête** :
   ```promql
   histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))
   ```
3. **Titre** : "Latence P95"
4. **Dans les options à droite** :
   - **Unit** : `s` (secondes) ou `ms` (millisecondes)
   - **Decimals** : `2`
5. **Clique sur** : `Apply`

---

### 📍 Étape 11 : Ajoute un panel - Mémoire utilisée

1. **Add** → **Visualization**
2. **Requête** :
   ```promql
   process_working_set_bytes / 1024 / 1024
   ```
3. **Titre** : "Mémoire RAM (MB)"
4. **Type de visualisation** : Change de `Time series` à `Stat`
5. **Unit** : `none` (déjà en MB)
6. **Clique sur** : `Apply`

---

### 📍 Étape 12 : Sauvegarde ton dashboard

1. **Clique sur** : L'icône **� Save dashboard** (en haut à droite)
2. **Dashboard name** : `BrokerX - Monitoring`
3. **Folder** : Laisse `General`
4. **Clique sur** : `Save`

**✅ Ton dashboard est sauvegardé !**

**Pour le retrouver** :
- Menu gauche → **Dashboards** → **Browse**
- Ou va directement sur `http://localhost:3000/dashboards`

---

## 🎯 Exercice pratique : Dashboard "4 Golden Signals"

**Les 4 Golden Signals** (Google SRE) :
1. **Latency** : Temps de réponse
2. **Traffic** : Volume de requêtes
3. **Errors** : Taux d'erreurs
4. **Saturation** : Utilisation ressources

### Créons un dashboard complet

**1. Nouveau dashboard** :
- Dashboards → New → New Dashboard

**2. Panel 1 : Traffic (Requêtes/sec)** :
```promql
sum(rate(http_requests_received_total[1m]))
```
- Type : **Time series**
- Titre : "1. Traffic - Requêtes/sec"

**3. Panel 2 : Latency (P50, P95, P99)** :
```promql
# P50 (médiane)
histogram_quantile(0.50, rate(http_request_duration_seconds_bucket[5m]))

# P95
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))

# P99
histogram_quantile(0.99, rate(http_request_duration_seconds_bucket[5m]))
```
- Type : **Time series**
- Titre : "2. Latency - P50/P95/P99"
- Unit : `s` (secondes)
- **💡 Astuce** : Ajoute les 3 requêtes dans le même panel (bouton `+ Query`)

**4. Panel 3 : Errors (Taux d'erreurs 4xx/5xx)** :
```promql
# Taux d'erreurs 4xx
sum(rate(http_requests_received_total{code=~"4.."}[1m]))

# Taux d'erreurs 5xx
sum(rate(http_requests_received_total{code=~"5.."}[1m]))

# Pourcentage d'erreurs total
sum(rate(http_requests_received_total{code=~"4..|5.."}[1m])) 
/ 
sum(rate(http_requests_received_total[1m])) 
* 100
```
- Type : **Time series**
- Titre : "3. Errors - Taux 4xx/5xx"
- Unit : `percentunit` (pour le pourcentage)

**5. Panel 4 : Saturation (CPU, RAM, Threads)** :
```promql
# CPU (0-1 = 0-100%)
rate(process_cpu_seconds_total[1m])

# RAM en MB
process_working_set_bytes / 1024 / 1024

# Nombre de threads
process_num_threads
```
- Type : **Time series**
- Titre : "4. Saturation - Ressources"
- **💡 Astuce** : Utilise 2 axes Y (un pour CPU/threads, un pour RAM)

**6. Sauvegarde** : `BrokerX - 4 Golden Signals`

---

## 📊 Panels supplémentaires utiles

### Panel : Requêtes par endpoint (Top 5)

```promql
topk(5, sum by (endpoint) (rate(http_requests_received_total[1m])))
```

**Configuration** :
- Type : **Bar chart** ou **Table**
- Titre : "Top 5 endpoints les plus utilisés"

---

### Panel : Latence par endpoint

```promql
histogram_quantile(0.95, 
  sum by (endpoint, le) (
    rate(http_request_duration_seconds_bucket[5m])
  )
)
```

**Configuration** :
- Type : **Table**
- Titre : "Latence P95 par endpoint"
- Tri : Par valeur (descending)

---

### Panel : Taux d'erreurs par endpoint

```promql
sum by (endpoint, code) (rate(http_requests_received_total{code=~"4..|5.."}[1m]))
```

**Configuration** :
- Type : **Heatmap** ou **Time series**
- Titre : "Erreurs par endpoint"

---

### Panel : GC Collections (Garbage Collector)

```promql
rate(dotnet_collection_count_total{generation="0"}[1m])
rate(dotnet_collection_count_total{generation="1"}[1m])
rate(dotnet_collection_count_total{generation="2"}[1m])
```

**Configuration** :
- Type : **Time series**
- Titre : "GC Collections/sec (Gen 0/1/2)"
- Légende : Afficher les générations

---

## 🎨 Personnalisation des dashboards

### Options de panel utiles

**Dans l'éditeur de panel, onglet "Panel options"** :

| Option | Utilité | Exemple |
|--------|---------|---------|
| **Title** | Nom du panel | "Latence P95" |
| **Description** | Info-bulle (hover) | "Temps de réponse du 95e percentile" |
| **Transparent** | Fond transparent | Pour des overlays |
| **Repeat by** | Répéter pour chaque label | Un panel par endpoint |

**Onglet "Standard options"** :

| Option | Utilité | Exemple |
|--------|---------|---------|
| **Unit** | Format de la valeur | `s`, `ms`, `bytes`, `percent` |
| **Decimals** | Nombre de décimales | `2` pour 0.01 |
| **Min/Max** | Limites de l'axe Y | Min: 0, Max: auto |
| **Color scheme** | Palette de couleurs | Green-Yellow-Red (traffic light) |

**Onglet "Thresholds"** :

Définis des seuils de couleur :
```
0-50ms   : Vert (bon)
50-200ms : Jaune (acceptable)
>200ms   : Rouge (problème)
```

---

## ⚙️ Variables de dashboard (avancé)

**Utilité** : Filtrer dynamiquement les données

### Exemple : Variable "endpoint"

1. **Dashboard settings** (⚙️ en haut) → **Variables** → **Add variable**
2. **Name** : `endpoint`
3. **Type** : `Query`
4. **Data source** : `Prometheus`
5. **Query** :
   ```promql
   label_values(http_requests_received_total, endpoint)
   ```
6. **Multi-value** : ✓ (permet sélection multiple)
7. **Include All option** : ✓

**Utilisation dans les requêtes** :
```promql
rate(http_requests_received_total{endpoint="$endpoint"}[1m])
```

**Résultat** : Dropdown en haut du dashboard pour filtrer par endpoint !

---

## 📸 Alerting (optionnel - Phase avancée)

**Grafana peut envoyer des alertes** :

### Exemple : Alerte si latence P95 > 200ms

1. **Panel Latency** → **Edit**
2. **Onglet "Alert"** → **Create alert rule**
3. **Condition** :
   ```
   WHEN avg() OF query(A, 5m, now) IS ABOVE 0.2
   ```
4. **Notifications** : Slack, Email, Webhook, etc.

---

## ✅ Checklist validation Grafana

| Critère | Comment vérifier | Statut |
|---------|------------------|--------|
| Interface accessible | http://localhost:3000 | ✅ |
| Login admin/admin | Connexion réussie | ✅ |
| Datasource Prometheus | Configuration → Data sources | ✅ |
| Explore fonctionne | Explore → requête PromQL | ✅ |
| Dashboard créé | Dashboards → Browse | ✅ |
| Panels s'affichent | Graphiques avec données | ✅ |
| Temps réel | Refresh automatique actif | ✅ |

---

## 🎓 Exercice final : Dashboard complet "BrokerX Production"

**Objectif** : Créer un dashboard de monitoring complet

### Row 1 : Vue globale (4 stats)

**4 panels de type "Stat" côte à côte** :

1. **Requêtes totales (24h)** :
   ```promql
   sum(increase(http_requests_received_total[24h]))
   ```

2. **Requêtes/sec (moyenne 5min)** :
   ```promql
   sum(rate(http_requests_received_total[5m]))
   ```

3. **Latence P95 (maintenant)** :
   ```promql
   histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))
   ```

4. **Taux d'erreurs (%)** :
   ```promql
   sum(rate(http_requests_received_total{code=~"5.."}[5m])) 
   / 
   sum(rate(http_requests_received_total[5m])) 
   * 100
   ```

### Row 2 : 4 Golden Signals (4 graphiques)

Ajoute les 4 panels décrits plus haut.

### Row 3 : Détails par endpoint (2 tables)

1. **Top 10 endpoints** : Table avec trafic/latence/erreurs
2. **Endpoints avec erreurs** : Seulement ceux avec code 4xx/5xx

### Row 4 : Ressources système (3 graphiques)

1. CPU, RAM, Threads
2. GC Collections
3. Handles ouverts

**Sauvegarde** : `BrokerX - Production Monitoring`

---

# Partie 3 : k6 - Tests de charge

---

# Partie 3 : k6 - Tests de charge

## ⏳ Installation (à venir)

**Ce qu'on va faire** :
1. Installer k6 (outil de load testing)
2. Écrire des scénarios de test :
   - `signup-load.js` : UC-01 (Inscription)
   - `auth-load.js` : UC-02 (Authentification)
   - `deposit-load.js` : UC-03 (Dépôt)
   - `mixed-load.js` : Scénario mixte réaliste
3. Exécuter des tests avec différentes charges (10, 50, 100, 200 VUs)
4. Analyser les résultats dans Grafana

**📝 Cette section sera complétée au fur et à mesure de l'installation.**

---

## 🔗 Liens rapides

| Service | URL | Credentials | Statut |
|---------|-----|-------------|--------|
| **BrokerX API** | http://localhost:5000 | - | ✅ Running |
| **Swagger** | http://localhost:5000/swagger | - | ✅ Available |
| **Health Check** | http://localhost:5000/health | - | ✅ Available |
| **Métriques Prometheus** | http://localhost:5000/metrics | - | ✅ Exposing |
| **Prometheus UI** | http://localhost:9090 | - | ✅ Running |
| **Grafana UI** | http://localhost:3000 | admin / admin | ✅ Running |
| **MySQL** | localhost:3307 | brokerx / brokerx | ✅ Running (healthy) |

**💡 Note importante - Ports** :
- **Port 5000** : API BrokerX (standard ASP.NET)
- **Port 8080** : Sera réservé pour **KrakenD API Gateway** (Phase 2b)
- Quand on ajoutera le load balancing, on aura plusieurs instances API (5001, 5002, 5003...)
- Le client final accèdera via **KrakenD (8080)** qui dispatche vers les APIs

---

## 🎉 Étape actuelle : Prometheus + Grafana ✅ COMPLÉTÉS

**Prochaine étape** : k6 (Tests de charge)

**Prêt à continuer ?** 🚀
