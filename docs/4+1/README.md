# Index des Vues 4+1 — BrokerX

## Qu'est-ce que le modèle 4+1 ?

Le modèle de vues architecturales **4+1** a été proposé par **Philippe Kruchten** (1995) pour documenter l'architecture logicielle selon plusieurs perspectives complémentaires. Chaque vue s'adresse à une audience spécifique et répond à des questions différentes.

**Référence**: Kruchten, P. (1995). _"The 4+1 View Model of Architecture"_, IEEE Software, 12(6), 42-50.

---

## 📐 Les 4 Vues + Scénarios

### 1️⃣ Vue Logique (Logical View)
**Fichier**: [`logique.puml`](./logique.puml)  
**Audience**: Utilisateurs finaux, analystes métier, architectes fonctionnels  
**Objectif**: Montrer la structure fonctionnelle du système en termes de concepts métier  
**Notation**: Diagrammes de classes UML, diagrammes de packages  
**Contenu**:
- Classes métier et bounded contexts (DDD)
- Relations entre entités (Client, Compte, Portefeuille, etc.)
- Ports d'application (ISignupUseCase, IAuthUseCase, etc.)
- **Abstraction**: Concepts métier, pas de détails techniques d'implémentation

**Questions clés répondues**:
- Quelles sont les entités métier principales ?
- Comment les concepts métier sont-ils reliés ?
- Quels sont les bounded contexts (DDD) ?

---

### 2️⃣ Vue Processus (Process View)
**Fichier**: [`processus.puml`](./processus.puml)  
**Audience**: Intégrateurs système, architectes techniques  
**Objectif**: Montrer les aspects dynamiques runtime (concurrence, threads, communication IPC)  
**Notation**: Diagrammes de séquence, diagrammes d'activité, diagrammes de déploiement dynamiques  
**Contenu**:
- Processus OS (dotnet, MySQL, NGINX, Redis, simulateurs)
- Threads et thread pools (ASP.NET, NGINX workers)
- Communication inter-processus (IPC: TCP/IP, HTTP)
- Flux asynchrones (webhooks, callbacks)
- Concurrence et synchronisation (verrous, atomic ops Redis)

**Questions clés répondues**:
- Combien de processus OS tournent en parallèle ?
- Comment les threads sont-ils gérés (thread pool ASP.NET) ?
- Quelle est la communication entre processus (HTTP, TCP) ?
- Comment fonctionne l'asynchronisme (webhook payment) ?
- Comment se fait la synchronisation (Redis atomic, EF concurrency tokens) ?

**⚠️ Important**: Ne pas confondre avec une vue Component & Connector (C&C) qui est statique.  
→ Voir [`architecture-hexagonale.puml`](./architecture-hexagonale.puml) pour la vue C&C.

---

### 3️⃣ Vue Développement (Development View / Implementation View)
**Fichier**: [`developpement.puml`](./developpement.puml)  
**Audience**: Développeurs, gestionnaires de configuration, nouveaux membres de l'équipe  
**Objectif**: Montrer l'organisation statique du code source (modules, bibliothèques, dépendances)  
**Notation**: Diagrammes de packages UML, diagrammes de composants  
**Contenu**:
- Projets .NET (Domain, Application, Infrastructure.Web, Infrastructure.Persistence, Infrastructure.Adapters)
- Dépendances inter-projets (<<access>>)
- Layering (couches logicielles: UI → App → Domain ← Infra)
- Tests (Domain.Tests, E2E.Tests, Infrastructure.Tests)

**Questions clés répondues**:
- Comment le code est-il organisé (répertoires, projets) ?
- Quelles sont les dépendances entre modules ?
- Quel est le sens des dépendances (Dependency Rule) ?
- Où se trouvent les tests ?

---

### 4️⃣ Vue Déploiement (Physical View / Deployment View)
**Fichier**: [`deploiement.puml`](./deploiement.puml)  
**Audience**: Ingénieurs système, DevOps, ops  
**Objectif**: Montrer la topologie physique du système (serveurs, conteneurs, réseau)  
**Notation**: Diagrammes de déploiement UML  
**Contenu**:
- Nœuds physiques (VM, conteneurs Docker)
- Composants déployés sur chaque nœud (BrokerX API, MySQL, NGINX, Redis, etc.)
- Réseaux et protocoles (HTTP, TCP/IP, ports)
- Évolution en 3 phases:
  - **Phase 1**: Monolith (app + MySQL + simulateurs)
  - **Phase 2**: Load Balancing (NGINX + N instances app + Redis + Prometheus/Grafana)
  - **Phase 3**: Microservices (KrakenD Gateway + services indépendants)

**Questions clés répondues**:
- Sur quels nœuds physiques le système tourne-t-il ?
- Comment les composants communiquent-ils sur le réseau ?
- Quels sont les ports et protocoles utilisés ?
- Comment le système scale (1 → N instances) ?

---

### ➕ Scénarios (Use Cases / +1)
**Fichiers**: [`scénarios/`](./scénarios/)  
**Audience**: Toutes parties prenantes  
**Objectif**: Illustrer et valider les 4 vues via des cas d'utilisation bout-en-bout  
**Notation**: Diagrammes de séquence système, diagrammes d'activité  
**Contenu**:
- **UC-01**: Inscription (activation par OTP + KYC)
- **UC-02**: Authentification (MFA conditionnelle)
- **UC-03**: Dépôt de fonds (async, idempotence)

**Questions clés répondues**:
- Comment un cas d'utilisation traverse-t-il les 4 vues ?
- Les vues supportent-elles tous les UC requis ?
- Les NFR (latence, throughput) sont-ils atteignables ?

---

## 🗂️ Fichiers disponibles

| Vue | Fichier PUML | Description | État |
|-----|-------------|-------------|------|
| **Logique** | [`logique.puml`](./logique.puml) | Classes métier, bounded contexts, ports application | ✅ Conforme |
| **Processus** | [`processus.puml`](./processus.puml) | Threads, processus OS, IPC, concurrence | ✅ Conforme |
| **Développement** | [`developpement.puml`](./developpement.puml) | Projets .NET, dépendances, layering | ✅ Conforme |
| **Déploiement** | [`deploiement.puml`](./deploiement.puml) | Nœuds physiques, conteneurs, réseau (Phases 1-3) | ⚠️ À simplifier |
| **Scénarios UC-01** | [`scénarios/UC-01 Inscription...puml`](./scénarios/) | Flux inscription | ✅ Existant |
| **Scénarios UC-02** | [`scénarios/UC-02 Authentification...puml`](./scénarios/) | Flux auth + MFA | ✅ Existant |
| **Scénarios UC-03** | [`scénarios/UC-03 Dépôt...puml`](./scénarios/) | Flux dépôt async | ✅ Existant |

### Fichiers complémentaires (hors 4+1)
| Fichier | Description | Rôle |
|---------|-------------|------|
| [`architecture-hexagonale.puml`](./architecture-hexagonale.puml) | Vue Component & Connector (Ports & Adapters) | **Bonus**: Montre le style architectural, pas une vue 4+1 officielle |
| [`ANALYSE-CONFORMITE-4+1.md`](./ANALYSE-CONFORMITE-4+1.md) | Analyse de conformité aux normes de Kruchten | Documentation méthodologique |
| [`README-execution.md`](./README-execution.md) | Plan d'exécution détaillé (checklist 65 items) | Guide d'implémentation |

---

## 🎯 Comment utiliser ces vues ?

### Pour les analystes métier / Product Owners
→ **Vue Logique** ([`logique.puml`](./logique.puml))  
Comprendre les concepts métier (Client, Compte, Portefeuille) et leurs relations.

### Pour les développeurs
→ **Vue Développement** ([`developpement.puml`](./developpement.puml))  
Comprendre l'organisation du code (projets, dépendances, tests).  
→ **Architecture Hexagonale** ([`architecture-hexagonale.puml`](./architecture-hexagonale.puml)) (bonus)  
Comprendre le style architectural (Ports & Adapters, flux de contrôle).

### Pour les intégrateurs / architectes techniques
→ **Vue Processus** ([`processus.puml`](./processus.puml))  
Comprendre les aspects runtime (threads, IPC, concurrence, synchronisation).

### Pour les DevOps / ingénieurs système
→ **Vue Déploiement** ([`deploiement.puml`](./deploiement.puml))  
Comprendre la topologie physique (conteneurs, réseau, scaling).

### Pour valider les exigences
→ **Scénarios** ([`scénarios/`](./scénarios/))  
Vérifier que les UC critiques sont supportés par les 4 vues.

---

## 📊 Cohérence entre les vues

Les 4 vues doivent être **cohérentes** entre elles. Voici des exemples de cohérence :

| Élément | Vue Logique | Vue Processus | Vue Développement | Vue Déploiement |
|---------|------------|--------------|------------------|----------------|
| **Client (entité)** | Classe `Client` dans BC Identité | — | Fichier `Domain/Model/Identite/Client.cs` | — |
| **SignupService** | Port `ISignupUseCase` | — | Fichier `Application/Services/SignupService.cs` | — |
| **Thread pool ASP.NET** | — | Queue thread pool (100-200 threads) | — | Process `dotnet` (container) |
| **MySQL** | — | Process `mysqld` + connection pool | Référencé dans `Infrastructure.Persistence` | Container `mysql:8` (port 3306) |
| **NGINX** | — | Process nginx (1 master + 4 workers) | — | Container `nginx:alpine` (Phase 2) |
| **UC-01 Signup** | `ISignupUseCase` → `Client` → `Compte` | Thread ASP.NET → DB → OTP-sim (HTTP) | `SignupController` → `SignupService` | POST /api/v1/signup (Phase 1) |

---

## ✅ Checklist de validation (conformité Kruchten)

Avant de considérer les vues 4+1 comme "terminées", valider:

- [x] **Vue Logique**: Compréhensible par un analyste métier sans connaissance technique ?
- [x] **Vue Processus**: Montre clairement les aspects runtime (threads, async, concurrence) ?
- [x] **Vue Développement**: Un nouveau dev peut comprendre l'organisation du code ?
- [ ] **Vue Déploiement**: Un ops peut déployer le système avec ces diagrammes ? (à simplifier)
- [x] **Scénarios**: Les UC principaux sont tracés à travers les 4 vues ?
- [x] **Cohérence**: Pas de contradictions entre les vues ?
- [ ] **Complétude**: Toutes les phases (1, 2, 3) sont documentées ? (Phase 2-3 à compléter)

---

## 📚 Références

- Kruchten, P. (1995). _"The 4+1 View Model of Architecture"_, IEEE Software, 12(6), 42-50.
- Bass, L., Clements, P., Kazman, R. (2012). _Software Architecture in Practice_ (3rd ed.), Addison-Wesley.
- Martin, R. C. (2017). _Clean Architecture: A Craftsman's Guide to Software Structure and Design_, Prentice Hall.

---

## 🔧 Outils pour visualiser les diagrammes

### PlantUML (recommandé)
```bash
# Installer PlantUML
sudo apt install plantuml

# Générer PNG
plantuml docs/4+1/*.puml

# Ou via VS Code
# Extension: jebbs.plantuml
# Raccourci: Alt+D pour preview
```

### En ligne (si pas d'install locale)
- PlantUML Online: https://www.plantuml.com/plantuml/uml/
- Copier-coller le contenu des fichiers `.puml`

---

## 📝 Prochaines actions (TODO)

1. ✅ Créer vue Processus conforme (fait)
2. ✅ Renommer ancien processus.puml → architecture-hexagonale.puml (fait)
3. ⏳ Simplifier vue Déploiement (séparer en 3 fichiers phase1/2/3)
4. ⏳ Enrichir vue Développement (ajouter tests et structure dossiers)
5. ⏳ Vérifier scénarios Phase 2/3 (avec LB, cache, Gateway)
6. ⏳ Générer images PNG de tous les diagrammes

---

**Auteurs**: Équipe BrokerX — Projet LOG-430 (ÉTS)  
**Dernière mise à jour**: 26 octobre 2025
