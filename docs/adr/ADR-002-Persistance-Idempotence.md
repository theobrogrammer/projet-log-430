# ADR 002 – Persis## Conséquences
*  **Développement rapide** : InMemoryDatabase évite la complexité d'installation/migration d'une base de données externe.
*  **Testabilité élevée** : Données en mémoire permettent des tests rapides et isolés, pas de cleanup database entre tests.
*  **Architecture cohérente** : Repositories nommés "InMemory*" mais utilisant EF Core, transition transparente vers une vraie DB.
*  **Intégrité maintenue** : Contraintes d'idempotence préservées, traçabilité via Ledger/Audit.
*  **Évolutivité** : Simple changement de `UseInMemoryDatabase` vers `UseMySQL/UsePostgreSQL` pour production.
*  **Limitation production** : InMemoryDatabase ne persiste pas entre redémarrages, acceptable pour développement/démo.
*  **Configuration centralisée** : `BrokerXDbContext` avec `OnModelCreating` pour tous les mappings EF Core.& Idempotence (EF Core InMemoryDatabase, Repositories hybrides)

## Statut
Acceptée (Révisée)

## Contexte
Les UC-01/02/03 nécessitent : stockage des **clients/comptes/portefeuilles**, journalisation **Audit**, transactions de dépôt **PayTx** avec **idempotence**, et écritures **Ledger** en **append-only**. L'application cible le développement rapide et les tests avec une base de données en mémoire, tout en conservant une architecture prête pour la production.

## Décision
Utiliser **EF Core avec InMemoryDatabase** et des **Repositories hybrides** nommés "InMemory*" mais utilisant réellement EF Core pour la persistance.

* **Technos** : EF Core 9.0 avec `UseInMemoryDatabase("TestDatabase")`, `BrokerXDbContext` comme contexte unifié.
* **Modèle** : DbSets pour `Client`, `Compte` (Account), `Portefeuille` (Portfolio), `ContactOtp`, `PolitiqueMFA` (MfaPolicy), `DefiMFA` (MfaChallenge), `Session`, `TransactionPaiement` (PayTx), `EcritureLedger` (LedgerEntry), `AuditLog`.
* **Repositories** : Pattern "InMemory*Repository" (ex: `InMemoryMfaPolicyRepository`, `InMemorySessionRepository`) qui utilisent `BrokerXDbContext` avec EF Core pour la persistance réelle.
* **Idempotence** : contrainte **UNIQUE** sur `TransactionPaiement.IdempotencyKey` via EF Core.
* **Ledger** : append-only (aucun UPDATE/DELETE), écritures via `EcritureLedger.DepositCredit()` et `EcritureLedger.Adjustment()`.
* **Transactions** : gérées au niveau **Application** via `SaveChangesAsync()` sur le contexte EF.

## Conséquences
*  **Intégrité** : pas de double crédit (idempotency key), traçabilité forte via Ledger/Audit.
*  **Testabilité** : Repos testables (base conteneurisée), données seed pour scénarios.
*  **Portabilité** : EF Core abstrait une partie des détails SQL, DAO possibles si contraintes.
*  **Complexité schéma** : maintenance de PK/FK/index, conversions (ex. `DateOnly`), précision `decimal(18,2)`.
*  **Discipline transactionnelle** : bien entourer les modifications (PayTx→Ledger→Portfolio) d’une transaction.
