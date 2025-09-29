# ADR 003 Décision
Défin## Conséquences
*  **API prévisible** : Clients front savent réagir (UI/flows) aux erreurs HTTP standard avec messages JSON structurés.
*  **Séparation claire** : Domaine lève des `InvalidOperationException`, contrôleurs traduisent en réponses HTTP avec mapping approprié.
*  **Développement rapide** : Pas de versionnage complexe, DTOs simples, focus sur la fonctionnalité plutôt que sur l'évolutivité prématurée.
*  **Token Authentication solide** : JWT avec validation centralisée via `GetCurrentClientAsync()` dans `AuthController`.
*  **Observabilité** : Journalisation via `AuditLog` et logs ASP.NET Core avec détails d'erreur appropriés.
* **Évolutivité future** : Architecture permet d'ajouter versionnage explicite si nécessaire sans refactor majeur.
*  **Gestion d'erreur centralisée** : Pattern cohérent `try-catch-BadRequest/Unauthorized` dans tous les contrôleurs. **stratégie d'erreurs stable** au niveau Application et gérer le **mapping** dans l'adapter Web. Utiliser des **DTOs simples** sans versionnage explicite pour le développement rapide.

* **Codes d'erreurs HTTP** : 400 (BadRequest - validation), 401 (Unauthorized - token invalide/expiré), 403 (Forbidden), 404 (NotFound - ressource), 422 (UnprocessableEntity - ModelState), 500 (unexpected).
* **Messages d'erreur** : objets JSON structurés `{ error: "message", details?: "..." }`, messages français clairs, pas de secrets ni détails internes.
* **Mapping HTTP** : Contrôleurs ASP.NET Core font le mapping **sans** logique métier, utilisation d'`InvalidOperationException` du domaine.
* **DTOs actuels** : `LoginRequestDto/ResponseDto`, `SignupRequestDto/ResponseDto`, `VerifyMfaRequestDto`, `DepositRequestDto/ResponseDto` - sans versionnage pour simplifier.
* **Token Authentication** : JWT avec `Authorization: Bearer <token>` pour les endpoints protégés, validation via `GetCurrentClientAsync()`.
* **Audit** : Utilisation d'`AuditLog.Ecrire()` pour journaliser les opérations importantes (AUTH_MFA_PASSED, etc.).tégie d’erreurs et versionnage interne des contrats

## Statut
Acceptée

## Contexte
Les UC exposent des erreurs variées : **OTP invalide/expiré**, **MFA requise**, **KYC rejeté**, **paiement Pending/Failed**, **doublon idempotency key**. On veut des **contrats stables** côté API, des contrôleurs **minces**, et une **observabilité** cohérente (logs/audit).

## Décision
Définir une **stratégie d’erreurs stable** au niveau Application et gérer le **mapping** dans l’adapter Web. Versionner **en interne** les DTO (v1).

* **Codes d’erreurs (exemples)** : `AUTH_MFA_REQUIRED`, `OTP_INVALID`, `OTP_EXPIRED`, `KYC_REJECTED`, `PAYMENT_PENDING`, `PAYMENT_FAILED`, `IDEMPOTENCY_CONFLICT`.
* **Mapping HTTP** : 400 (invalid), 401 (auth), 403 (forbidden), 404 (not found), 409 (conflict), 422 (validation), 500 (unexpected). Les contrôleurs font le mapping **sans** logique métier.
* **Messages** : clairs, non-sensibles (pas de secrets ni détails internes), corrélés par `requestId/traceId`.
* **Version interne** : DTO **v1** (prépare l’évolution sans breaking change) ; tolérance aux champs inconnus côté serveur.
* **Audit** : chaque tentative importante (login, MFA, dépôt) journalisée avec code/issue (sans données sensibles).

## Conséquences
*  **API prévisible** : clients front savent réagir (UI/flows) aux erreurs communes.
*  **Séparation claire** : domaine lève des exceptions spécifiques, contrôleurs traduisent en HTTP.
*  **Évolutivité** : passage à v2 possible (ajout champs, dépréciation graduelle) sans casser v1.
* **Discipline de catalogage** : maintenir la liste des erreurs et leur mapping.
*  **Bruit de logs** : calibrer niveaux (info/warn/error) pour éviter l’inondation.
