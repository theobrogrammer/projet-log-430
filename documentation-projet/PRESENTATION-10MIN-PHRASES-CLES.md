# 🎤 Présentation Projet BrokerX - Phrases Clés (10 minutes)

**Projet:** BrokerX - Plateforme de Trading  
**Date:** 4 décembre 2025  
**Durée:** 10 minutes  
**Objectif:** Démontrer l'architecture, la qualité et l'observabilité du système

---

## 📋 Structure de la Présentation

```
Introduction (1 min)
├── Architecture Hexagonale (2 min)
├── Technologies & Infrastructure (3 min)
├── Observabilité & Qualité (2 min)
├── Démonstration (1.5 min)
└── Conclusion (0.5 min)
```

---

## 🎯 INTRODUCTION (1 minute)

### Phrase d'ouverture
> **"BrokerX est une plateforme de trading moderne qui respecte les principes d'architecture logicielle de niveau entreprise."**

### Points clés à mentionner
- **Projet:** Courtage en ligne avec gestion de comptes, dépôts et données de marché en temps réel
- **Challenge:** Construire un système **fiable, scalable et observable**
- **Approche:** Architecture hexagonale + Clean Architecture + DDD

### Phrase de transition
> **"Commençons par l'architecture qui est le fondement de la qualité du système."**

---

## 🏗️ ARCHITECTURE HEXAGONALE (2 minutes)

### Concept principal
> **"L'architecture hexagonale isole la logique métier des détails techniques - la base de données et le framework ne sont que des détails d'implémentation."**

### Diagramme 4+1 de Kruchten
**Montrer le diagramme et dire:**
> **"Nous avons appliqué le modèle 4+1 de Philippe Kruchten : vue logique pour les concepts métier, vue développement pour l'organisation du code, vue processus pour les flux temps réel, vue physique pour le déploiement, et scénarios pour les use cases."**

### Les 3 couches - TRÈS IMPORTANT
```
┌─────────────────────────────────────────┐
│ DOMAIN (Cœur métier)                    │
│ → Client, Compte, Portefeuille, Quote   │
│ → Règles métier pures (pas de DB!)      │
└─────────────────────────────────────────┘
         ↓ Ports (Interfaces)
┌─────────────────────────────────────────┐
│ APPLICATION (Use Cases)                  │
│ → SignupService, WalletService           │
│ → Orchestration des règles métier        │
└─────────────────────────────────────────┘
         ↓ Ports (Interfaces)
┌─────────────────────────────────────────┐
│ INFRASTRUCTURE (Adapters)                │
│ → MySQL, Redis, SignalR, KrakenD         │
│ → Détails techniques interchangeables    │
└─────────────────────────────────────────┘
```

### Phrase clé à répéter
> **"Le Domain ne dépend de RIEN - il n'a aucune référence à Entity Framework, ASP.NET ou Redis. C'est l'infrastructure qui dépend du Domain, pas l'inverse."**

### Exemple concret
> **"Par exemple, la classe `Client` dans le Domain ne sait pas qu'elle sera stockée dans MySQL. Elle expose une méthode `Creer()` qui valide l'email, le téléphone et l'âge minimum - c'est tout. C'est InMemoryClientRepository dans l'infrastructure qui s'occupe de la persistance."**

### Bénéfices - dire clairement
> **"Cette approche nous donne 3 avantages majeurs :**
> 1. **Testabilité:** Je peux tester la logique métier sans base de données
> 2. **Maintenabilité:** Changer de MySQL à PostgreSQL n'affecte que l'infrastructure
> 3. **Compréhension:** Le code métier est isolé et facile à comprendre"

---

## 💉 INJECTION DE DÉPENDANCES (intégré dans architecture - 30 secondes)

### Phrase clé
> **"L'injection de dépendances dans Program.cs gère automatiquement le cycle de vie des objets avec 3 lifetimes : Singleton pour les services lourds comme Redis, Scoped pour les requêtes HTTP, et Transient pour les objets légers."**

### Exemple rapide
> **"Par exemple, le DbContext est Scoped - une instance par requête HTTP - pour isoler les transactions. Redis ConnectionMultiplexer est Singleton car la connexion est coûteuse et thread-safe."**

---

## 🛠️ TECHNOLOGIES & INFRASTRUCTURE (3 minutes)

### ASP.NET Core Web API
> **"Notre API est construite avec ASP.NET Core 8, le framework Microsoft moderne pour les services web haute performance."**

**Points techniques:**
- Controllers exposent les endpoints REST
- Middleware pipeline pour CORS, logs, métriques
- SignalR pour WebSocket (données temps réel)

### Redis Cache (30 secondes)
> **"Redis est notre cache distribué pour améliorer les performances."**

**Dire:**
> **"J'ai implémenté un pattern de Cache-Aside avec graceful degradation - si Redis tombe, l'application continue à fonctionner, juste plus lentement."**

**Exemple:**
```csharp
// 1. Check cache
var cached = await cache.GetAsync("account:123");
if (cached != null) return cached; // ⚡ Fast path

// 2. Cache miss → Database
var account = await db.GetAccount(123);

// 3. Store in cache
await cache.SetAsync("account:123", account, ttl: 5min);
```

### KrakenD API Gateway (45 secondes)
> **"KrakenD est notre API Gateway qui protège et optimise nos services backend."**

**Fonctionnalités clés à mentionner:**
1. **Rate Limiting:** 10 requêtes/seconde par IP - protège contre les abus
2. **Circuit Breaker:** Après 5 erreurs consécutives, coupe le trafic pendant 10 secondes
3. **Métriques Prometheus:** Collecte automatiquement latence, erreurs, throughput

**Phrase d'impact:**
> **"KrakenD transforme 10 lignes de configuration JSON en un proxy intelligent qui gère rate limiting, circuit breaker et métriques automatiquement."**

### Prometheus & Grafana (1 minute)
> **"L'observabilité est critique pour un système en production. Nous utilisons Prometheus pour collecter les métriques et Grafana pour les visualiser."**

**Métriques collectées:**
- **HTTP:** Nombre de requêtes, latence (p50, p95, p99), taux d'erreur
- **Redis:** Hit ratio du cache, latence, connexions
- **SignalR:** Nombre de connexions WebSocket actives, messages/seconde

**Montrer dashboard Grafana et dire:**
> **"Ce dashboard montre en temps réel la santé du système : ici on voit 150 requêtes/seconde avec une latence p95 de 45ms."**

### Serilog (Audit Logs) (30 secondes)
> **"Pour l'audit et le débogage, j'utilise Serilog qui produit des logs structurés en JSON."**

**Exemple de log:**
```json
{
  "@t": "2025-12-04T10:30:00Z",
  "@mt": "Dépôt effectué",
  "ClientId": "abc-123",
  "Amount": 1000.00,
  "Currency": "CAD",
  "TransactionId": "tx-456"
}
```

**Phrase clé:**
> **"Les logs structurés sont interrogeables comme une base de données - je peux chercher tous les dépôts d'un client en filtrant sur ClientId."**

---

## 📊 OBSERVABILITÉ & QUALITÉ (2 minutes)

### Les 3 piliers de l'observabilité
> **"Un système production nécessite 3 types d'observabilité : Logs pour le 'quoi', Métriques pour le 'combien', et Traces pour le 'où'."**

**Notre implémentation:**
1. **Logs:** Serilog → fichiers JSON rotatifs (7 jours de rétention)
2. **Métriques:** Prometheus → 15 jours de rétention
3. **Traces:** (Futur: OpenTelemetry)

### Tests - Pyramide de tests
> **"Nous suivons la pyramide de tests : beaucoup de tests unitaires, moins de tests d'intégration, peu de tests E2E."**

**Montrer les chiffres:**
```
Tests E2E (Lents)           ▲  5 tests
    Tests Intégration      ▲▲▲  15 tests
        Tests Unitaires  ▲▲▲▲▲  30 tests
```

**Exemple de test unitaire:**
> **"Voici un test du WalletService qui vérifie qu'on ne peut pas déposer un montant négatif - le test utilise des mocks pour isoler la logique."**

```csharp
[Fact]
public async Task Deposit_NegativeAmount_ThrowsException()
{
    // Arrange: Setup
    var service = CreateService();
    
    // Act & Assert: Vérifie l'exception
    await Assert.ThrowsAsync<ArgumentException>(
        () => service.DepositAsync(clientId, -100m)
    );
}
```

### Tests d'intégration (Testcontainers)
> **"Les tests d'intégration utilisent Testcontainers pour lancer un vrai Redis dans Docker - pas de mocks, on teste la vraie intégration."**

---

## 🎬 DÉMONSTRATION (1.5 minutes)

### Préparer à l'avance
- Terminal avec `docker-compose up` déjà lancé
- Navigateur avec Grafana ouvert
- Postman/curl avec requêtes prêtes

### Scénario de démo

**1. Montrer l'architecture en action (30 sec)**
> **"Voici l'application en cours d'exécution. Nous avons 5 conteneurs Docker : l'API, MySQL, Redis, Prometheus et Grafana."**

```bash
docker-compose ps
# Montrer les 5 services "Up"
```

**2. Appel API + Observer les logs (30 sec)**
> **"Je vais créer un compte avec l'endpoint /signup. Regardez les logs Serilog en temps réel."**

```bash
# Terminal 1: Logs en temps réel
tail -f src/Infrastructure.Web/logs/app-*.jsonl | jq

# Terminal 2: Appel API
curl -X POST http://localhost:8080/api/v1/signup \
  -H "Content-Type: application/json" \
  -d '{
    "email": "demo@brokerx.com",
    "phone": "+15141234567",
    "fullName": "Demo User",
    "password": "SecurePass123!",
    "dateOfBirth": "1990-01-01"
  }'
```

**Dire pendant l'appel:**
> **"Voyez les logs apparaître : validation de l'email, vérification KYC, création du compte, audit log. Tout est tracé."**

**3. Montrer Grafana (30 sec)**
> **"Maintenant regardons le dashboard Grafana. Ce graphique montre que la requête a pris 120ms, et le cache Redis a un hit ratio de 85%."**

Naviguer vers: `http://localhost:3000`

**Points à montrer sur le dashboard:**
- Graphique des requêtes/seconde
- Latence p95
- Taux d'erreur (0%)
- Redis hit ratio

---

## 🎯 CONCLUSION (30 secondes)

### Résumé des réalisations
> **"En résumé, BrokerX démontre 3 principes d'architecture moderne :**
> 1. **Séparation des préoccupations** avec l'architecture hexagonale
> 2. **Infrastructure as Code** avec Docker Compose
> 3. **Observabilité complète** avec logs, métriques et dashboards"

### Phrase de clôture forte
> **"Ce projet montre qu'une architecture bien pensée dès le départ facilite non seulement le développement, mais aussi le débogage, la maintenance et l'évolution du système."**

### Questions?
> **"Je suis prêt à répondre à vos questions sur l'architecture, l'implémentation ou les choix techniques."**

---

## 💡 PHRASES CLÉS À MÉMORISER

### Top 10 des phrases d'impact

1. **Architecture:**
   > "Le Domain ne dépend de RIEN - l'infrastructure dépend du Domain, pas l'inverse."

2. **Testabilité:**
   > "Je peux tester la logique métier sans base de données grâce à l'architecture hexagonale."

3. **Injection de dépendances:**
   > "Singleton pour Redis, Scoped pour DbContext, Transient pour les objets légers."

4. **Redis:**
   > "Cache-Aside avec graceful degradation - si Redis tombe, l'app continue."

5. **KrakenD:**
   > "10 lignes de JSON = Rate limiting + Circuit breaker + Métriques automatiques."

6. **Prometheus:**
   > "Collecte automatique de la latence p50, p95, p99 et du taux d'erreur."

7. **Serilog:**
   > "Logs structurés en JSON - interrogeables comme une base de données."

8. **Tests:**
   > "Pyramide de tests : 30 unitaires, 15 intégration, 5 E2E."

9. **Observabilité:**
   > "3 piliers : Logs pour le 'quoi', Métriques pour le 'combien', Traces pour le 'où'."

10. **Conclusion:**
    > "Une architecture bien pensée facilite le développement, le débogage et l'évolution."

---

## 🎭 GESTION DU STRESS

### Si vous oubliez quelque chose
- **Respirez profondément**
- **Revenez au diagramme** - il contient toute l'histoire
- **Utilisez la phrase:** "Laissez-moi vous montrer un exemple concret..."

### Si une question vous bloque
- **Soyez honnête:** "C'est une excellente question. Dans le temps imparti, j'ai priorisé X, mais Y serait la prochaine étape."
- **Redirigez vers vos forces:** "Ce que je peux vous montrer, c'est comment nous avons résolu le problème Z..."

### Transition entre sections
- **Architecture → Techno:** "Maintenant que vous comprenez l'architecture, voyons les technologies qui l'implémentent."
- **Techno → Observabilité:** "Ces technologies sont excellentes, mais comment savoir si le système fonctionne? C'est le rôle de l'observabilité."
- **Observabilité → Démo:** "Assez de théorie - voyons tout ça en action."

---

## ⏱️ TIMING DÉTAILLÉ (à respecter!)

| Section | Durée | Temps cumulé |
|---------|-------|--------------|
| Introduction | 1:00 | 1:00 |
| Architecture Hexagonale | 2:00 | 3:00 |
| Technologies (ASP.NET, Redis, KrakenD) | 3:00 | 6:00 |
| Observabilité (Prometheus, Grafana, Serilog) | 2:00 | 8:00 |
| Démonstration | 1:30 | 9:30 |
| Conclusion | 0:30 | 10:00 |

**Conseil:** Pratiquez avec un chronomètre et coupez les détails si vous dépassez!

---

## 🎯 QUESTIONS PROBABLES & RÉPONSES

### Q1: "Pourquoi l'architecture hexagonale?"
> **"Pour 3 raisons : testabilité (pas besoin de DB pour tester), maintenabilité (changer de techno n'affecte que l'infra), et clarté (le code métier est isolé)."**

### Q2: "Redis peut-il devenir un point de défaillance unique?"
> **"Non, j'ai implémenté un pattern de graceful degradation. Si Redis tombe, le RedisCacheAdapter attrape l'exception et retourne null - l'app va chercher en base de données directement."**

### Q3: "Combien de temps pour implémenter tout ça?"
> **"L'architecture hexagonale a pris 2-3 jours pour bien structurer. L'observabilité (Prometheus, Grafana, Serilog) a pris 1 jour. KrakenD a pris 4 heures. Le reste est du développement classique."**

### Q4: "Comment vous assurez-vous de la qualité?"
> **"Trois mécanismes : tests automatisés (50 tests), logs structurés pour l'audit, et métriques temps réel pour détecter les anomalies."**

### Q5: "C'est over-engineered pour un projet étudiant, non?"
> **"C'est exactement ce qu'on demande en entreprise pour un système production. Le projet simule un système réel avec des contraintes réelles : scalabilité, observabilité, fiabilité."**

### Q6: "Quelle est la partie la plus difficile?"
> **"Respecter strictement la séparation des couches - le Domain ne doit JAMAIS dépendre de l'infrastructure. Ça demande de la discipline."**

---

## 📝 CHECKLIST AVANT LA PRÉSENTATION

### Technique (30 min avant)
- [ ] Lancer `docker-compose up -d`
- [ ] Vérifier que les 5 services sont "Up"
- [ ] Ouvrir Grafana et se connecter (admin/admin)
- [ ] Tester un appel API avec curl/Postman
- [ ] Ouvrir un terminal avec `tail -f logs/app-*.jsonl | jq`
- [ ] Charger les diagrammes (4+1, architecture hexagonale)

### Mental (10 min avant)
- [ ] Relire les 10 phrases clés
- [ ] Respirer profondément 3 fois
- [ ] Visualiser le succès de la présentation
- [ ] Se rappeler: "Je connais ce projet par cœur"

### Pendant la présentation
- [ ] Parler LENTEMENT (tendance à accélérer sous stress)
- [ ] Regarder l'audience, pas l'écran
- [ ] Utiliser les mains pour souligner les points importants
- [ ] Sourire - vous êtes fier de votre travail!

---

## 🎬 SCRIPT COMPLET CHRONO (pour mémorisation)

### 0:00 - Introduction
"Bonjour, je vais vous présenter BrokerX, une plateforme de trading qui démontre les principes d'architecture logicielle moderne."

### 1:00 - Architecture
"Commençons par l'architecture hexagonale. Le principe est simple : le Domain ne dépend de RIEN."

### 3:00 - Technologies
"Voyons maintenant les technologies. ASP.NET Core pour l'API, Redis pour le cache, KrakenD pour le gateway."

### 6:00 - Observabilité
"Un système production doit être observable. Nous utilisons Prometheus, Grafana et Serilog."

### 8:00 - Démonstration
"Assez de théorie - voyons tout ça en action. Je lance un appel API et on observe les logs."

### 9:30 - Conclusion
"En résumé : architecture hexagonale, infrastructure as code, observabilité complète. Questions?"

---

## ✨ BONUS: Phrases pour impressionner le prof

1. **Sur les patterns:**
   > "J'ai appliqué plusieurs patterns : Repository pour la persistance, Cache-Aside pour Redis, et CQRS implicite avec la séparation Use Cases / Queries."

2. **Sur la production:**
   > "Ce système est production-ready : rolling logs, health checks, graceful shutdown, et circuit breakers."

3. **Sur les metrics:**
   > "Nous mesurons les 4 golden signals : latence, trafic, erreurs et saturation."

4. **Sur le code:**
   > "Le code respecte SOLID : Single Responsibility, Open/Closed, Liskov, Interface Segregation, Dependency Inversion."

5. **Sur la scalabilité:**
   > "Le système scale horizontalement : on peut lancer 10 instances de l'API derrière NGINX, Redis gère la synchronisation du cache."

---

**🎯 DERNIER CONSEIL:** Répétez cette présentation 3 fois à voix haute avant le jour J. Le timing va devenir naturel et vous serez confiant!

**Bonne chance! 🚀**
