# ADR 001 – Architecture hexagonale (Ports & Adapters) avec Repositories (C#/.NET)

## Statut
Acceptée

## Contexte
Le projet **BrokerX** (Phase 1) implémente trois cas d’utilisation prioritaires (**Inscription**, **Authentification**, **Approvisionnement**).
Exigences clés : **testabilité** des règles métier (KYC/OTP, MFA, idempotence), **évolutivité** vers des intégrations externes (paiement simulé aujourd’hui, réel demain), possibilité de **règlement asynchrone** (Pending → Settled), et séparation claire **domaine ↔ infrastructure**. L’application est en **C#/.NET**, conteneurisée, et intégrée en CI/CD.

## Décision
Adopter l’architecture **Hexagonale (Ports & Adapters)** avec **Repositories** pour la persistance.

* **Domaine (core)** : entités/agrégats (`Client`, `Account`, `Portfolio`, `PayTx`, `MfaPolicy`, …) et règles **pures**, sans dépendance framework/HTTP/BD.
* **Application (use cases)** : services d’orchestration (`SignupService`, `AuthService`, `WalletService`) qui séquencent les étapes, gèrent la transaction et **appellent des ports**.
* **Ports** :
  * *Inbound* (exposés) : `ISignupUseCase`, `IAuthUseCase`, `IDepositUseCase`.
  * *Outbound* (dépendances) : `IOtpPort`, `IKycPort`, `IPaymentPort`, `ISessionPort`, `IAuditPort`, `ILedgerPort`, `IClientRepository`, `IAccountRepository`, `IPayTxRepository`, `IPortfolioRepository`.
* **Adapters** :
  * *Entrants* : contrôleurs REST (mapping DTO ↔ Use Cases).
  * *Sortants* : implémentations concrètes (EF Core/DAO pour Repos, JWT pour `SessionPort`, SMTP/SMS pour `OtpPort`, simulateur pour `PaymentPort`, logs structurés pour `AuditPort`, SQL pour `LedgerPort`).
* **Sens des dépendances** : `Infrastructure → Application → Domaine` (jamais l’inverse).
* **Persistance** : Repositories + contrainte **UNIQUE** sur `PayTx.IdempotencyKey` ; table **Ledger** en **append-only** (pas d’UPDATE/DELETE).

## Conséquences
*  **Testabilité élevée** : règles d’UC (KYC, OTP/MFA, idempotence, Pending→Settled) testées sans BD ni HTTP (mocks des ports).
*  **Évolutivité** : remplacement facile des systèmes externes (paiement simulé → réel), passage futur vers microservices/événements sans toucher au domaine.
*  **Séparation nette** : contrôleurs minces, logique métier isolée, couplage contrôlé aux frameworks.
*  **Plomberie supplémentaire** : interfaces/ports, mappers DTO↔domaine, configuration DI.
*  **Discipline d’architecture** : outillage recommandé (tests d’architecture/analyzers) pour garantir l’absence de cycles et le respect du sens des dépendances.
