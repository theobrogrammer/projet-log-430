# ADR 003 – Stratégie d’erreurs et versionnage interne des contrats

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
