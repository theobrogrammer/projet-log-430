# ADR 002 – Persistance & Idempotence (EF Core / DAO, Ledger append-only)

## Statut
Acceptée

## Contexte
Les UC-01/02/03 nécessitent : stockage des **clients/comptes/portefeuilles**, journalisation **Audit**, transactions de dépôt **PayTx** avec **idempotence**, et écritures **Ledger** en **append-only**. La démo doit être reproductible en VM/CI (PostgreSQL via Docker), avec migrations reproductibles et données de seed.

## Décision
Utiliser des **Repositories** (EF Core ou DAO) pour l’accès aux données, garantir l’**idempotence** et un **Ledger** immuable.

* **Technos** : PostgreSQL 16, EF Core (ou DAO explicites si requis).
* **Modèle** : tables pour `Client`, `Account`, `Portfolio`, `KycCase`, `ContactOtp`, `MfaPolicy`, `MfaChallenge`, `Session`, `PayTx`, `LedgerEntry`, `AuditLog`.
* **Idempotence** : contrainte **UNIQUE** sur `PayTx.IdempotencyKey`.
* **Ledger** : append-only (aucun UPDATE/DELETE), index temporels et par `AccountId`.
* **Transactions** : ouvertes au niveau **Application** (service UC), avec rollback sur erreurs.
* **Migrations** : outils EF Core ; `docker-compose` lance DB + migrations + seed.

## Conséquences
*  **Intégrité** : pas de double crédit (idempotency key), traçabilité forte via Ledger/Audit.
*  **Testabilité** : Repos testables (base conteneurisée), données seed pour scénarios.
*  **Portabilité** : EF Core abstrait une partie des détails SQL, DAO possibles si contraintes.
*  **Complexité schéma** : maintenance de PK/FK/index, conversions (ex. `DateOnly`), précision `decimal(18,2)`.
*  **Discipline transactionnelle** : bien entourer les modifications (PayTx→Ledger→Portfolio) d’une transaction.
