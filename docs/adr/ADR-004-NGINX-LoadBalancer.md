# ADR 004 – Load Balancing avec NGINX (Phase 2 - Étape 2a)

## Statut
Acceptée

## Contexte
La Phase 1 déploie une **instance unique** de l'API BrokerX, créant un point de défaillance unique (SPOF). Les objectifs Phase 2 exigent :
* **Disponibilité** : ≥ 99.9% avec tolérance aux pannes
* **Performance** : ≥ 1200 req/s avec latence P95 < 100ms
* **Scaling horizontal** : déploiement de N réplicas API
* **Failover automatique** : routage vers instances saines uniquement

Le choix du load balancer doit être simple à configurer, performant, et compatible avec l'observabilité.

## Décision
Adopter **NGINX** comme load balancer reverse proxy avec algorithme **least_conn** et déploiement de **3 réplicas API**.

* **Technologie** : NGINX (image `nginx:latest`)
* **Algorithme** : `least_conn` pour distribution de charge optimale
* **Configuration** : 3 services API backend, health checks `max_fails=3` et `fail_timeout=30s`
* **Keepalive** : Réutilisation connexions TCP pour réduire latence
* **Headers** : Forwarding `X-Real-IP`, `X-Forwarded-For`, `Host`
* **Port** : NGINX sur port 80, forward vers APIs sur 8080

## Alternatives considérées

### HAProxy
* **Avantages** : Performance légèrement supérieure, health checks sophistiqués, stats page détaillée.
* **Inconvénients** : Configuration plus verbieuse, communauté plus petite.
* **Rejeté** : NGINX plus polyvalent et meilleure intégration observabilité.

### Traefik
* **Avantages** : Configuration dynamique via labels Docker, dashboard intégré.
* **Inconvénients** : Consommation mémoire élevée (~100MB vs ~10MB), complexité non justifiée.
* **Rejeté** : Overkill pour déploiement Docker Compose simple.

## Conséquences

### Positives
* **Disponibilité accrue** : Tolérance panne 1-2 instances sur 3
* **Performance** : Throughput ×3 (300 → 900+ req/s), latence P95 réduite (500ms → 150ms)
* **Scaling simple** : Ajout réplicas sans modification code
* **Observabilité** : Métriques Prometheus via stub status

### Négatives
* **Complexité** : +1 composant à gérer, configuration à maintenir
* **SPOF** : NGINX non répliqué devient point de défaillance
* **Overhead** : +2-5ms latence par requête (proxy hop)
* **Configuration statique** : Reload NGINX nécessaire pour ajout/retrait instances

### Risques et mitigations
* **Risque** : NGINX down → API inaccessible
  * **Mitigation** : Monitoring + alertes, healthcheck Docker, runbook
* **Risque** : Mauvaise distribution charge
  * **Mitigation** : Tests k6, logs NGINX, dashboard per-instance

---

**Date** : 26 octobre 2025  
**Auteurs** : Équipe BrokerX  
**Révisions** : v1.0 (acceptée)
