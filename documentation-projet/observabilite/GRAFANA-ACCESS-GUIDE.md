# 🎨 Guide d'Accès Grafana - BrokerX

**Date** : 27 octobre 2025  
**Dashboard** : 4 Golden Signals

---

## 🚀 Accès rapide

### Étape 1 : Ouvre Grafana dans ton navigateur

```
http://localhost:3000
```

---

### Étape 2 : Connexion

**Identifiants** :
- **Username** : `admin`
- **Password** : `admin`

**⚠️ Premier login** :
- Grafana te demandera de changer le mot de passe
- **Tu peux cliquer sur "Skip"** pour développement local

---

### Étape 3 : Accède au dashboard "4 Golden Signals"

**Option 1 - URL directe** (le plus rapide) :
```
http://localhost:3000/d/brokerx-golden-signals/brokerx-4-golden-signals
```

**Option 2 - Via le menu** :
1. Clique sur **☰** (menu hamburger en haut à gauche)
2. Clique sur **"Dashboards"**
3. Cherche **"BrokerX - 4 Golden Signals"**
4. Clique dessus

---

## 📊 Vue d'ensemble du dashboard

### Panels disponibles (9 au total)

#### Ligne 1 - Latence et Trafic

**1. 🚀 Latence (P50, P95, P99) par Endpoint**
- Graphique time series (gauche)
- 3 courbes : P50 (médiane), P95, P99
- Unité : secondes
- Légende avec valeurs moyennes

**2. 📊 Trafic (Requêtes/sec) par Endpoint**
- Graphique time series (droite)
- Courbes par endpoint (/health, /api/v1/signup, etc.)
- Unité : requêtes/seconde

---

#### Ligne 2 - Erreurs et Saturation

**3. ❌ Erreurs (Taux d'erreurs 4xx/5xx)**
- Graphique stacked area (gauche)
- Courbe orange : erreurs 4xx (client)
- Courbe rouge : erreurs 5xx (serveur)
- Unité : pourcentage

**4. 💾 Saturation - Mémoire**
- Graphique time series (centre)
- Mémoire utilisée par l'API
- Unité : MB (mégaoctets)

**5. ⚡ Saturation - CPU**
- Graphique time series (droite)
- Utilisation CPU
- Unité : pourcentage (0 à 100%)

---

#### Ligne 3 - Indicateurs globaux

**6. Latence P95 Globale**
- Gauge circulaire (gauche)
- Seuils :
  - 🟢 Vert : < 50ms
  - 🟡 Jaune : 50-100ms
  - 🔴 Rouge : > 100ms

**7. Trafic Total (req/s)**
- Gauge circulaire (centre)
- Seuils :
  - 🟢 Vert : < 10 req/s
  - 🟡 Jaune : 10-50 req/s
  - 🔴 Rouge : > 50 req/s

**8. Taux d'Erreurs Global**
- Gauge circulaire (centre-droite)
- Seuils :
  - 🟢 Vert : < 1%
  - 🟡 Jaune : 1-5%
  - 🔴 Rouge : > 5%

**9. Distribution des Requêtes par Endpoint**
- Pie chart (droite)
- Pourcentage de trafic par endpoint
- Couleurs différentes pour chaque route

---

## ⚙️ Fonctionnalités utiles

### Change la période d'observation

**En haut à droite** : Clique sur `Last 15 minutes 🕐`

**Options** :
- `Last 5 minutes` - Vue très récente
- `Last 15 minutes` - Par défaut
- `Last 30 minutes` - Vue courte
- `Last 1 hour` - Vue moyenne
- `Last 6 hours` - Vue large
- `Custom time range` - Personnalisé

---

### Active le refresh automatique

**En haut à droite** : Clique sur l'icône **⟳**

**Fréquences disponibles** :
- `Off` - Pas de refresh
- `5s` - Toutes les 5 secondes (recommandé)
- `10s` - Toutes les 10 secondes
- `30s` - Toutes les 30 secondes
- `1m` - Toutes les minutes

**💡 Astuce** : Active `5s` pour voir les métriques en temps réel !

---

### Zoom sur un panel

**Méthode 1** :
- Clique sur le titre du panel
- Clique sur **"View"**
- Le panel s'affiche en plein écran
- **ESC** pour sortir

**Méthode 2** :
- Clique et glisse sur le graphique pour zoomer sur une période
- Double-clic pour reset le zoom

---

### Exporte un panel en image

**Étapes** :
1. Clique sur le titre du panel
2. Clique sur **"Share"**
3. Onglet **"Link"** : Copie le lien direct
4. Onglet **"Snapshot"** : Crée un snapshot permanent
5. Ou utilise un outil de capture d'écran

---

## 🧪 Génère du trafic pour voir les métriques

### Test 1 : Trafic normal

```bash
# 30 requêtes espacées de 0.5s
for i in {1..30}; do 
  curl -s http://localhost:5000/health > /dev/null
  echo "✓ Request $i/30"
  sleep 0.5
done
```

**Observe dans Grafana** :
- Panel **"Trafic"** : montée progressive
- Panel **"Latence"** : stable autour de 5-10ms
- Gauge **"Trafic Total"** : ~2 req/s

---

### Test 2 : Génération d'erreurs

```bash
# 20 erreurs 400 (validation)
for i in {1..20}; do 
  curl -s -X POST http://localhost:5000/api/v1/signup \
    -H "Content-Type: application/json" \
    -d '{"invalid":"data"}' > /dev/null
  echo "✓ Error $i/20"
  sleep 0.3
done
```

**Observe dans Grafana** :
- Panel **"❌ Erreurs"** : courbe orange monte
- Gauge **"Taux d'Erreurs"** : passe au jaune/rouge
- Pie chart : `/api/v1/signup` apparaît

---

### Test 3 : Charge importante

```bash
# 100 requêtes en parallèle
for i in {1..100}; do 
  curl -s http://localhost:5000/health > /dev/null &
done
wait
echo "✓ 100 requêtes envoyées en parallèle"
```

**Observe dans Grafana** :
- **Trafic** : gros pic
- **Latence P95/P99** : augmentation temporaire
- **CPU** : pic d'utilisation
- **Mémoire** : peut augmenter légèrement

---

### Test 4 : Stress test continu

```bash
# Génère du trafic continu pendant 2 minutes
echo "🔥 Stress test démarré (2 minutes)..."
timeout 120 bash -c '
  while true; do 
    curl -s http://localhost:5000/health > /dev/null
    sleep 0.1
  done
'
echo "✓ Stress test terminé"
```

**Observe dans Grafana** (refresh 5s activé) :
- Trafic stable à ~10 req/s
- Latence P95 stable ou légèrement élevée
- CPU et mémoire montent progressivement

---

## 🎯 Requêtes PromQL utilisées dans le dashboard

### Panel "Latence P50/P95/P99"

**P50 (médiane)** :
```promql
histogram_quantile(0.50, sum by (endpoint, le) (rate(http_request_duration_seconds_bucket[5m])))
```

**P95** :
```promql
histogram_quantile(0.95, sum by (endpoint, le) (rate(http_request_duration_seconds_bucket[5m])))
```

**P99** :
```promql
histogram_quantile(0.99, sum by (endpoint, le) (rate(http_request_duration_seconds_bucket[5m])))
```

---

### Panel "Trafic"

```promql
sum by (endpoint) (rate(http_requests_received_total[1m]))
```

---

### Panel "Erreurs"

**4xx** :
```promql
sum(rate(http_requests_received_total{code=~"4.."}[1m])) / sum(rate(http_requests_received_total[1m]))
```

**5xx** :
```promql
sum(rate(http_requests_received_total{code=~"5.."}[1m])) / sum(rate(http_requests_received_total[1m]))
```

---

### Panel "Mémoire"

```promql
process_working_set_bytes / 1024 / 1024
```

---

### Panel "CPU"

```promql
rate(process_cpu_seconds_total[1m])
```

---

## ❓ Troubleshooting

### Le dashboard n'apparaît pas

**Problème** : Aucun dashboard "BrokerX" visible

**Solutions** :
1. **Attends 10-15 secondes** (le provisioning prend du temps)
2. **Rafraîchis la page** (F5)
3. **Vérifie les logs Grafana** :
   ```bash
   docker compose logs grafana | grep -i dashboard
   ```
4. **Redémarre Grafana** :
   ```bash
   docker compose restart grafana
   ```

---

### Les panels sont vides

**Problème** : Dashboards visible mais aucune donnée

**Solutions** :
1. **Vérifie la datasource** :
   - Configuration → Data sources → Prometheus
   - Clique sur **"Test"**
   - Doit afficher "Data source is working"

2. **Génère du trafic** :
   ```bash
   for i in {1..10}; do curl -s http://localhost:5000/health > /dev/null; done
   ```

3. **Change la période** : En haut à droite, sélectionne `Last 5 minutes`

4. **Vérifie Prometheus** : http://localhost:9090/targets (doit être UP)

---

### Erreur "Login failed"

**Problème** : Impossible de se connecter avec admin/admin

**Solutions** :
1. **Vérifie les logs** :
   ```bash
   docker compose logs grafana | grep -i password
   ```

2. **Reset le mot de passe** :
   ```bash
   docker compose exec grafana grafana-cli admin reset-admin-password admin
   ```

3. **Redémarre Grafana** :
   ```bash
   docker compose restart grafana
   ```

---

### Dashboard "404 Not Found"

**Problème** : URL `/d/brokerx-golden-signals` retourne 404

**Solutions** :
1. **Vérifie que le fichier existe** :
   ```bash
   ls grafana/dashboards/4-golden-signals.json
   ```

2. **Vérifie le provisioning** :
   ```bash
   docker compose logs grafana | grep "provision"
   ```

3. **Vérifie l'UID dans le JSON** :
   - Ouvre `grafana/dashboards/4-golden-signals.json`
   - Cherche `"uid": "brokerx-golden-signals"`

---

## ✅ Checklist de validation

| Critère | Commande/Action | Statut |
|---------|-----------------|--------|
| Grafana accessible | http://localhost:3000 | ⬜ |
| Login admin/admin | Connexion réussie | ⬜ |
| Datasource Prometheus | Configuration → Test = Working | ⬜ |
| Dashboard visible | Dashboards → BrokerX trouvé | ⬜ |
| 9 panels affichés | Dashboard complet | ⬜ |
| Données dans les panels | Génère trafic → graphiques bougent | ⬜ |
| Refresh 5s fonctionne | Activation → mise à jour temps réel | ⬜ |

---

## 🎉 Bravo !

Tu maîtrises maintenant **Grafana** ! 

**Prochaine étape** : k6 pour les tests de charge

---

## 📚 Pour aller plus loin

### Crée un panel personnalisé

1. Dans le dashboard, clique sur **"Add panel"** (en haut)
2. Entre une requête PromQL, exemple :
   ```promql
   process_num_threads
   ```
3. Change le titre : "Nombre de Threads"
4. Clique sur **"Apply"**
5. Sauvegarde : 💾 **"Save dashboard"**

---

### Exporte le dashboard en JSON

1. Dashboard → ⚙️ **Settings** (en haut à droite)
2. Onglet **"JSON Model"**
3. Copie le JSON
4. Sauvegarde dans un fichier `.json`
5. Partage avec ton équipe !

---

### Crée une alerte

1. Ouvre un panel (mode Edit)
2. Onglet **Alert** 🔔
3. **"Create alert rule from this panel"**
4. Configure la condition (ex: `latency > 0.1s`)
5. Choisis le canal de notification (email, Slack, etc.)
6. **Save rule**

---

**Documentation officielle Grafana** : https://grafana.com/docs/
