# Analyse de conformité des vues 4+1 selon Kruchten

## Rappel des normes 4+1 (Philippe Kruchten, 1995)

### Vue Logique (Logical View)
**Objectif**: Structure fonctionnelle du système pour les utilisateurs finaux  
**Audience**: Utilisateurs finaux, analystes métier  
**Notation**: Diagrammes de classes UML, diagrammes de packages  
**Contenu**: 
- Classes principales et leurs relations
- Packages/modules fonctionnels
- Abstraction des concepts métier
- **NE CONTIENT PAS**: détails techniques d'implémentation, threads, processus

### Vue Processus (Process View)
**Objectif**: Aspects dynamiques du système (concurrence, parallélisme, synchronisation)  
**Audience**: Intégrateurs système  
**Notation**: Diagrammes de séquence, diagrammes d'activité, diagrammes de communication  
**Contenu**:
- Processus et threads
- Communication inter-processus
- Synchronisation et concurrence
- Flux de contrôle runtime
- **CE N'EST PAS**: une vue C&C (Component & Connector) — confusion fréquente!

### Vue Développement (Development View / Implementation View)
**Objectif**: Organisation statique du code source  
**Audience**: Développeurs, gestionnaires de configuration  
**Notation**: Diagrammes de packages UML, diagrammes de composants  
**Contenu**:
- Modules/bibliothèques/packages
- Organisation du code source (répertoires, projets)
- Dépendances entre modules
- Layering (couches logicielles)

### Vue Déploiement (Physical View / Deployment View)
**Objectif**: Topologie physique du système  
**Audience**: Ingénieurs système, ops  
**Notation**: Diagrammes de déploiement UML  
**Contenu**:
- Nœuds physiques (serveurs, VMs, conteneurs)
- Composants déployés sur chaque nœud
- Réseaux et protocoles de communication
- Répartition de la charge

### Scénarios (Use Cases / +1)
**Objectif**: Illustrer et valider les 4 vues via des cas d'utilisation concrets  
**Audience**: Toutes parties prenantes  
**Notation**: Diagrammes de cas d'utilisation, diagrammes de séquence système  
**Contenu**:
- Cas d'utilisation principaux
- Scénarios bout-en-bout
- Validation que les 4 vues supportent les UC

---

## Analyse de nos diagrammes actuels

### ✅ Vue Logique (logique.puml) — CONFORME avec ajustements mineurs

**Ce qui est correct**:
- ✅ Focus sur les classes métier et bounded contexts (DDD)
- ✅ Relations entre entités (Client → Compte, Session → MFAChallenge)
- ✅ Abstraction métier claire (pas de détails techniques)
- ✅ Ports d'application (ISignupUseCase, etc.) comme interfaces

**Problèmes identifiés**:
- ⚠️ Les classes `MetricsExporter` et `LogsStructures` sont trop techniques pour la vue logique
  - **Solution**: Les déplacer vers la vue Processus (aspects runtime) ou créer un package "Infrastructure technique" minimal
- ⚠️ Manque de diagramme de packages de haut niveau
  - **Solution**: Ajouter un overview montrant les 4 bounded contexts principaux

**Actions correctives**:
1. Retirer `MetricsExporter` et `LogsStructures` de la vue logique
2. Ajouter un diagramme de packages simplifié en préambule
3. Clarifier que c'est la vue "pour les analystes métier"

---

### ❌ Vue Processus (processus.puml) — NON CONFORME (confusion avec C&C)

**Problème majeur**: Le diagramme actuel est une **vue C&C (Component & Connector)** de style architectural (Hexagonal), pas une vue Processus au sens de Kruchten.

**Ce qui est actuellement dans le diagramme**:
- ❌ Composants logiciels statiques (Controllers, Services, Repositories)
- ❌ Architecture Ports & Adapters (c'est une décision de style architectural)
- ❌ Dépendances statiques entre composants

**Ce qui devrait être dans la vue Processus selon Kruchten**:
- ✅ Threads et processus runtime
- ✅ Flux de contrôle asynchrone (webhooks, callbacks)
- ✅ Concurrence et synchronisation
- ✅ Files de messages, événements
- ✅ Ordre d'exécution temporel

**Solution**: Transformer radicalement cette vue pour montrer:
1. **Thread pool ASP.NET** qui gère les requêtes HTTP
2. **Flux asynchrone** du webhook payment (callback)
3. **Concurrence** des requêtes multiples (k6 load tests)
4. **Processus séparés** (app, MySQL, Redis, Prometheus, simulateurs)
5. **Communication IPC** (HTTP, TCP)
6. **Phases 2-3**: Processus multiples (app-1, app-2, app-3), LB, Gateway

**Actions correctives**:
1. **Renommer** le fichier actuel en `architecture-hexagonale.puml` (ce n'est PAS la vue Processus)
2. **Créer une vraie vue Processus** avec diagrammes de séquence/activité montrant:
   - UC-01 signup: requête HTTP → thread ASP.NET → appel DB → appel OTP-sim (async?) → réponse
   - UC-03 deposit: requête HTTP → DB insert → callback async du pay-sim → webhook → mise à jour
   - Phase 2: N instances app → NGINX (round-robin) → quelle instance traite quelle requête?

---

### ⚠️ Vue Déploiement (deploiement.puml) — PARTIELLEMENT CONFORME

**Ce qui est correct**:
- ✅ Nœuds physiques (Docker containers, VMs)
- ✅ Bases de données et leurs ports
- ✅ Communication réseau (HTTP, TCP)
- ✅ Évolution en 3 phases (clair)

**Problèmes identifiés**:
- ⚠️ Trop de détails "component-level" dans les conteneurs (ex: "Signup (UC-01)", "Auth (UC-02)")
  - **Solution**: Garder le niveau "composant déployable" (ex: "BrokerX API", pas les UC individuels)
- ⚠️ Les packages "Phase 1/2/3" ne sont pas des nœuds physiques
  - **Solution**: Restructurer en 3 diagrammes séparés ou utiliser des notes/frames clairs
- ⚠️ Manque de détails réseau (sous-réseaux, ports, protocoles)

**Actions correctives**:
1. Simplifier les détails internes des conteneurs (garder granularité "service")
2. Séparer en 3 diagrammes distincts (deploiement-phase1.puml, deploiement-phase2.puml, deploiement-phase3.puml)
3. Ajouter annotations réseau (ports, protocoles, latence attendue)

---

### ✅ Vue Développement (developpement.puml) — CONFORME

**Ce qui est correct**:
- ✅ Projets .NET clairement identifiés
- ✅ Dépendances inter-projets (<<access>>)
- ✅ Organisation en couches (Domain, Application, Infrastructure)
- ✅ Stéréotypes UML appropriés

**Améliorations possibles (non bloquantes)**:
- ➕ Ajouter la structure des dossiers physiques (src/, tests/, docs/)
- ➕ Montrer les tests (Domain.Tests, E2E.Tests) comme modules séparés
- ➕ Indiquer les technologies clés (EF Core, ASP.NET, JWT)

**Actions correctives**:
1. Ajouter package "Tests" avec projets de tests
2. Annoter les technologies principales par projet
3. Optionnel: Ajouter un diagramme de répertoires physiques

---

### ⚠️ Scénarios (+1) — PARTIELLEMENT PRÉSENTS

**Ce qui existe**:
- ✅ Fichiers `UC-01.puml`, `UC-02.puml`, `UC-03.puml` dans `scénarios/`
- ✅ Diagrammes de séquence (supposément)

**Problèmes potentiels**:
- ❓ Besoin de vérifier que ces diagrammes:
  - Illustrent le parcours à travers les 4 vues (Logique → Processus → Déploiement)
  - Montrent la validation des NFR (latence, throughput)
  - Incluent les scénarios Phase 2 (avec LB, cache) et Phase 3 (avec Gateway)

**Actions correctives**:
1. Lire les scénarios existants et vérifier leur complétude
2. Ajouter des scénarios Phase 2/3 si manquants
3. Créer un fichier `scenarios-overview.puml` montrant tous les UC et leur couverture

---

## Plan de corrections prioritaires

### 🔴 Priorité 1 (Critique — non-conformité 4+1)
1. **Créer une vraie vue Processus** (remplacer processus.puml actuel)
   - Diagrammes de séquence runtime pour UC-01/02/03
   - Montrer threads, async callbacks, concurrence
   - Phases 2-3: processus multiples, IPC
2. **Renommer processus.puml** → `architecture-hexagonale.puml` (pour référence, mais pas une vue 4+1)

### 🟡 Priorité 2 (Améliorations importantes)
3. **Simplifier vue Logique**: retirer classes techniques (Metrics, Logs)
4. **Restructurer vue Déploiement**: 3 diagrammes séparés (phase1/2/3)
5. **Enrichir vue Développement**: ajouter tests et structure de dossiers

### 🟢 Priorité 3 (Complétude)
6. **Vérifier et compléter Scénarios**: UC pour Phase 2 et 3
7. **Créer un index 4+1**: document maître expliquant quelle vue sert à quoi

---

## Décisions architecturales à documenter (ADRs manquants)

Au-delà de la conformité 4+1, il manque ces ADRs critiques:

1. **ADR-004: Choix de la vue Processus (async vs sync)**
   - Contexte: UC-03 dépôt avec callback async du pay-sim
   - Décision: Callback async + webhook vs polling vs sync
   - Conséquences: latence, complexité, idempotence

2. **ADR-005: Stratégie de scaling (Phase 2)**
   - Contexte: Besoin de ≥800 req/s (Phase 2)
   - Décision: NGINX round-robin + stateless app + session sticky (ou pas)
   - Alternatives rejetées: Sticky sessions, consistent hashing
   - Conséquences: complexité, single point of failure (NGINX)

3. **ADR-006: Choix API Gateway (KrakenD vs Kong vs Traefik)**
   - Contexte: Phase 3 microservices
   - Décision: KrakenD (config JSON, performant, open-source)
   - Alternatives: Kong (plus riche mais plus lourd), Traefik (bon pour Docker/K8s)
   - Conséquences: courbe apprentissage, features disponibles

---

## Validation finale (Checklist)

Avant de considérer les vues 4+1 comme "terminées", valider:

- [ ] **Vue Logique**: Compréhensible par un analyste métier sans connaissance technique?
- [ ] **Vue Processus**: Montre clairement les aspects runtime (threads, async, concurrence)?
- [ ] **Vue Développement**: Un nouveau dev peut comprendre l'organisation du code?
- [ ] **Vue Déploiement**: Un ops peut déployer le système avec ces diagrammes?
- [ ] **Scénarios**: Les UC principaux sont tracés à travers les 4 vues?
- [ ] **Cohérence**: Pas de contradictions entre les vues?
- [ ] **Complétude**: Toutes les phases (1, 2, 3) sont documentées?

---

## Prochaines actions immédiates (ordre d'exécution)

1. ✅ Créer ce document d'analyse (fait)
2. 🔄 Créer la vraie vue Processus (nouveau fichier `processus-runtime.puml`)
3. 🔄 Renommer `processus.puml` → `architecture-hexagonale.puml`
4. 🔄 Simplifier vue Logique (retirer classes techniques)
5. 🔄 Séparer vue Déploiement en 3 fichiers (phase1/2/3)
6. 🔄 Enrichir vue Développement (tests + dossiers)
7. 🔄 Vérifier scénarios existants
8. 🔄 Créer index 4+1 (README-4+1-views.md)

---

**Conclusion**: Nos diagrammes sont de bonne qualité technique mais ne respectent pas strictement la sémantique 4+1 de Kruchten. La principale confusion est la vue "Processus" qui est en réalité une vue "Component & Connector" architecturale. Une fois corrigé, nous aurons une documentation conforme aux standards académiques et industriels.
