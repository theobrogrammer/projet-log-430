# 📊 Serilog - Logs Structurés : Guide Complet

## 🎯 Table des matières

1. [Pourquoi Serilog ?](#pourquoi-serilog)
2. [Comment ça marche ?](#comment-ça-marche)
3. [Architecture détaillée](#architecture-détaillée)
4. [Cas d'usage concrets](#cas-dusage-concrets)
5. [Comparaison avant/après](#comparaison-avantaprès)
6. [FAQ](#faq)

---

## 🎯 Pourquoi Serilog ?

### Problème : Les logs par défaut de .NET sont limités

Avec `ILogger<T>` natif de .NET :

```csharp
// ❌ PROBLÈME : Logs en texte brut
_logger.LogInformation("User signed up: " + email + " with ID: " + clientId);
```

**Résultat en production** :
```
info: Program[0]
      User signed up: test@example.com with ID: 02dd16f7-9b6a-44a0-9f9b-276d111b506c
```

**❌ Limitations critiques** :
- Texte brut non structuré → Impossible à parser automatiquement
- Pas de propriétés extractables → Impossible de filtrer par `email` ou `clientId`
- Pas de métadonnées enrichies → ThreadId, MachineName, TraceId manquants
- Une seule destination → Console seulement, pas de fichiers rotatifs
- Pas de contrôle fin → Impossible de filtrer par namespace (`Microsoft.*` vs `Application.*`)

### ✅ Solution : Serilog avec logs structurés JSON

Avec Serilog :

```csharp
// ✅ SOLUTION : Logs structurés
Log.Information("UC01_SIGNUP_SUCCESS - Inscription réussie: {ClientId} {AccountId} {Email}", 
    clientId, accountId, email);
```

**Résultat en production** :
```json
{
  "@t": "2025-10-28T00:58:15.8700163Z",
  "@mt": "UC01_SIGNUP_SUCCESS - Inscription réussie: {ClientId} {AccountId} {Email}",
  "@tr": "2966dd20d67e6562e818127825978682",
  "@sp": "125be543cf3bdb96",
  "ClientId": "02dd16f7-9b6a-44a0-9f9b-276d111b506c",
  "AccountId": "b1f06373-0c5a-4f55-b540-4970d5e452ca",
  "Email": "test@example.com",
  "ThreadId": 23,
  "MachineName": "b08199f85876",
  "Application": "BrokerX",
  "Environment": "Development",
  "RequestPath": "/api/v1/signup",
  "ActionName": "SignupController.Signup"
}
```

**✅ Avantages** :
- **JSON structuré** → Facile à parser avec `jq`, Elasticsearch, Splunk, Datadog
- **Propriétés typées** → Cherche tous les logs où `ClientId = "xxx"`
- **Métadonnées automatiques** → ThreadId, MachineName, TraceId, Timestamp
- **Destinations multiples** → Console + fichiers + services externes simultanément
- **Filtrage intelligent** → Niveaux par namespace (`Microsoft.*` = Warning)
- **Rotation automatique** → Nouveaux fichiers chaque jour, rétention configurable
- **Corrélation de requêtes** → TraceId unique pour suivre une requête à travers les services

---

## 🔧 Comment ça marche ?

### Flux complet : De l'appel API au fichier JSON

```
┌─────────────────────────────────────────────────────────────────┐
│ 1️⃣ Requête HTTP arrive                                          │
│    POST /api/v1/signup                                          │
│    Body: { "email": "test@example.com", ... }                   │
└─────────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│ 2️⃣ Controller reçoit la requête                                 │
│    SignupController.Signup(SignupRequestDto dto)                │
│    → ASP.NET génère un TraceId unique: "2966dd20..."            │
└─────────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│ 3️⃣ Service métier log l'événement                               │
│    Log.Information("UC01_SIGNUP_START", new { Email = email }); │
└─────────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│ 4️⃣ Serilog enrichit automatiquement avec les enrichers          │
│    + ThreadId: 25 (de .Enrich.WithThreadId())                   │
│    + MachineName: "b08199f85876" (de .Enrich.WithMachineName()) │
│    + TraceId: "2966dd20..." (de .Enrich.FromLogContext())       │
│    + SpanId: "125be543..." (de .Enrich.FromLogContext())        │
│    + Application: "BrokerX" (de .Enrich.WithProperty())         │
│    + Environment: "Development" (de .Enrich.WithProperty())     │
│    + RequestPath: "/api/v1/signup" (de ASP.NET context)         │
│    + ActionName: "SignupController.Signup" (de ASP.NET context) │
│    + Timestamp: "2025-10-28T00:58:14.7597315Z" (automatique)    │
└─────────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│ 5️⃣ Serilog formate en JSON (CompactJsonFormatter)               │
│    {                                                             │
│      "@t": "2025-10-28T00:58:14.7597315Z",                      │
│      "@mt": "UC01_SIGNUP_START",                                │
│      "Email": "test@example.com",                               │
│      "@tr": "2966dd20d67e6562e818127825978682",                 │
│      "ThreadId": 25,                                             │
│      "MachineName": "b08199f85876",                             │
│      ...                                                         │
│    }                                                             │
└─────────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│ 6️⃣ Serilog écrit dans TOUTES les destinations (WriteTo)         │
│    ├─ Console (stdout Docker)                                   │
│    │  → Visible avec: docker-compose logs api                   │
│    │                                                             │
│    └─ Fichier rotatif (/app/logs/app-20251028.jsonl)            │
│       → Nouveau fichier chaque jour                             │
│       → Garde 7 jours (retainedFileCountLimit: 7)               │
│       → 1 ligne JSON par événement                              │
└─────────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│ 7️⃣ Analyse post-mortem avec jq                                  │
│    $ cat logs/app-*.jsonl | \                                   │
│        jq 'select(.Email == "test@example.com")'                │
│                                                                  │
│    → Trouve TOUS les événements de cet utilisateur              │
│    → Avec contexte complet (TraceId, erreurs, timing)           │
└─────────────────────────────────────────────────────────────────┘
```

---

## 🏗️ Architecture détaillée

### Configuration dans `Program.cs` (AVANT `builder`)

```csharp
// ===================================================================
// SERILOG CONFIGURATION (Phase 2 - Étape 2a: Observabilité)
// ===================================================================
Log.Logger = new LoggerConfiguration()
    
    // 1️⃣ ENRICHERS : Ajoutent des propriétés automatiquement
    .Enrich.FromLogContext()          // 🏷️ Contexte ASP.NET (RequestId, TraceId, SpanId)
    .Enrich.WithThreadId()            // 🧵 ID du thread (utile pour debug multi-thread)
    .Enrich.WithMachineName()         // 🖥️ Nom du conteneur/serveur
    .Enrich.WithProperty("Application", "BrokerX")    // 🏢 Tag custom
    .Enrich.WithProperty("Environment", 
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production")
    
    // 2️⃣ NIVEAUX DE LOGS : Filtrage intelligent
    .MinimumLevel.Information()       // 📊 Niveau global par défaut
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)  
        // 🔇 Réduit bruit Microsoft.* (AspNetCore, EntityFrameworkCore, etc.)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
        // 🔇 Réduit bruit System.*
    .MinimumLevel.Override("Application", LogEventLevel.Information)
        // 📢 Garde tous les logs de nos services
    .MinimumLevel.Override("Domain", LogEventLevel.Information)
        // 📢 Garde tous les logs du domaine métier
    
    // 3️⃣ SINKS : Destinations des logs (peuvent être multiples)
    .WriteTo.Console(new CompactJsonFormatter())    
        // 📺 Console (stdout Docker) en JSON compact
    
    .WriteTo.File(
        new CompactJsonFormatter(),           // Format JSON
        "logs/app-.jsonl",                    // Pattern de nom (app-20251028.jsonl)
        rollingInterval: RollingInterval.Day, // ♻️ Nouveau fichier chaque jour
        retainedFileCountLimit: 7)            // 🗑️ Garde 7 jours max
    
    .CreateLogger();  // ✅ Créer le logger global

try
{
    Log.Information("🚀 BrokerX API démarrage...");
    
    var builder = WebApplication.CreateBuilder(args);
    
    // 4️⃣ INTÉGRATION : Remplace ILogger par Serilog
    builder.Host.UseSerilog();  
        // 🔌 Tous les ILogger<T> utilisent maintenant Serilog
    
    // ... reste de la configuration
}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ L'application BrokerX a échoué au démarrage");
    throw;
}
finally
{
    Log.Information("🛑 BrokerX API arrêt");
    Log.CloseAndFlush();  // ⚠️ IMPORTANT : Flush avant fermeture
}
```

### Pourquoi AVANT `builder` ?

```csharp
// ❌ MAUVAIS : Configuration après builder
var builder = WebApplication.CreateBuilder(args);
Log.Logger = new LoggerConfiguration()...  // Trop tard !

// ✅ BON : Configuration avant builder
Log.Logger = new LoggerConfiguration()...
var builder = WebApplication.CreateBuilder(args);
```

**Raison** : Pour capturer **TOUS** les logs, même ceux du démarrage :
- Configuration de l'application
- Initialisation de la base de données
- Enregistrement des services
- Démarrage du serveur HTTP

Si configuré après, **ces logs sont perdus** !

---

## 📝 Utilisation dans les services

### Différence : Message template vs String interpolation

```csharp
using Serilog;  // Import direct

// ❌ MAUVAIS : String interpolation (pas structuré)
Log.Information($"Signup for {email}");  
// Résultat : { "@mt": "Signup for test@example.com" }
// → email devient du TEXTE BRUT (impossible à filtrer)

// ❌ MAUVAIS : Concaténation (pas structuré)
Log.Information("Signup for " + email);
// Même problème que ci-dessus

// ✅ BON : Message template (structuré)
Log.Information("Signup for {Email}", email);
// Résultat : { 
//   "@mt": "Signup for {Email}", 
//   "Email": "test@example.com"  ← Propriété extractable
// }
```

### Logs métier dans `SignupService`

```csharp
public async Task<SignupResult> CreateAccountAsync(
    string email, string fullName, string password, ...)
{
    // 📍 LOG DE DÉBUT : Trace l'entrée dans le use case
    Log.Information("UC01_SIGNUP_START - Début inscription pour {Email}", email);
    
    try
    {
        var client = Client.Creer(email, phone, fullName, passwordHash, birthDate);
        await _clients.AddAsync(client, ct);
        
        var compte = client.OuvrirCompte();
        await _comptes.AddAsync(compte, ct);
        
        // 📍 LOG DE SUCCÈS : Trace les IDs créés (pour debug/audit)
        Log.Information(
            "UC01_SIGNUP_SUCCESS - Inscription réussie: {ClientId} {AccountId} {Email}", 
            client.ClientId, compte.AccountId, email);
        
        return new SignupResult(client.ClientId, compte.AccountId, ...);
    }
    catch (Exception ex)
    {
        // 📍 LOG D'ERREUR : Trace l'exception avec contexte
        Log.Error(ex, "UC01_SIGNUP_ERROR - Échec inscription pour {Email}", email);
        throw;
    }
}
```

### Logs métier dans `AuthService`

```csharp
public async Task<LoginResult> LoginAsync(string email, string password, ...)
{
    Log.Information("UC02_LOGIN_START - Tentative de connexion pour {Email} depuis {IP}", 
        email, ip);
    
    var client = await _clients.GetByEmailAsync(email, ct) 
        ?? throw new InvalidOperationException("Identifiants invalides.");
    
    if (!ValidatePassword(password))
    {
        Log.Warning("UC02_LOGIN_INVALID_PASSWORD - Mot de passe invalide pour {Email}", email);
        throw new InvalidOperationException("Identifiants invalides.");
    }
    
    if (RequiresMfa(client))
    {
        var challenge = CreateMfaChallenge(client);
        
        Log.Information(
            "UC02_LOGIN_MFA_REQUIRED - MFA requis pour {ClientId} {Email} {ChallengeId}", 
            client.ClientId, email, challenge.ChallengeId);
        
        return new LoginResult(Token: "", MfaRequired: true, ...);
    }
    
    var session = CreateSession(client);
    
    Log.Information(
        "UC02_LOGIN_SUCCESS - Connexion réussie sans MFA: {ClientId} {Email} {SessionId}", 
        client.ClientId, email, session.SessionId);
    
    return new LoginResult(Token: token, MfaRequired: false);
}
```

---

## 🔍 Cas d'usage concrets

### 1. Debug production : Tracer toutes les actions d'un utilisateur

**Problème** : "L'utilisateur `test@example.com` dit que son inscription a échoué, mais on ne sait pas pourquoi"

```bash
# ✅ Avec Serilog : Filtrer par Email
docker compose exec api cat /app/logs/app-*.jsonl | \
  jq 'select(.Email == "test@example.com")'
```

**Résultat** :
```json
{"@t":"2025-10-28T00:58:14.759Z","@mt":"UC01_SIGNUP_START","Email":"test@example.com"}
{"@t":"2025-10-28T00:58:14.892Z","@l":"Error","@mt":"UC01_SIGNUP_ERROR","Email":"test@example.com","@x":"Database timeout..."}
```

👉 **Diagnostic instantané** : Timeout de base de données à 00:58:14.892

---

### 2. Analyser les erreurs par endpoint

**Problème** : "On a beaucoup d'erreurs 500, mais on ne sait pas quel endpoint est concerné"

```bash
# ✅ Grouper les erreurs par RequestPath
docker compose logs api | \
  sed 's/brokerx-api  | //' | \
  jq -s 'map(select(.["@l"] == "Error")) | group_by(.RequestPath) | 
         map({path: .[0].RequestPath, count: length})'
```

**Résultat** :
```json
[
  {"path": "/api/v1/signup", "count": 3},
  {"path": "/api/v1/login", "count": 12},
  {"path": "/api/v1/deposit", "count": 1}
]
```

👉 **Diagnostic** : `/api/v1/login` a 12 erreurs (problème prioritaire)

---

### 3. Tracer une requête complète (avec TraceId)

**Problème** : "Une requête spécifique a échoué, je veux voir TOUTES les étapes"

```bash
# ✅ Filtrer par TraceId (unique par requête HTTP)
cat logs/app-*.jsonl | \
  jq 'select(.["@tr"] == "2966dd20d67e6562e818127825978682")'
```

**Résultat** :
```json
{"@t":"...","@tr":"2966dd20...","@mt":"UC01_SIGNUP_START","Email":"test@example.com"}
{"@t":"...","@tr":"2966dd20...","@mt":"Database query","Query":"INSERT INTO Client..."}
{"@t":"...","@tr":"2966dd20...","@mt":"OTP sent","OtpId":"..."}
{"@t":"...","@tr":"2966dd20...","@mt":"UC01_SIGNUP_SUCCESS","ClientId":"..."}
```

👉 **Vue complète** : Toutes les opérations de cette requête, dans l'ordre chronologique

---

### 4. Statistiques de performance par controller

**Problème** : "Quel controller est le plus utilisé ?"

```bash
# ✅ Compter les appels par ActionName
jq -s 'group_by(.ActionName) | 
       map({action: .[0].ActionName, count: length}) | 
       sort_by(.count) | 
       reverse' logs/app-*.jsonl
```

**Résultat** :
```json
[
  {"action": "SignupController.Signup", "count": 145},
  {"action": "AuthController.Login", "count": 89},
  {"action": "WalletController.Deposit", "count": 34}
]
```

---

### 5. Analyser les logs en temps réel

```bash
# ✅ Stream des logs avec filtrage
docker compose logs -f api | \
  grep -E '^\{|brokerx-api.*\{' | \
  sed 's/brokerx-api  | //' | \
  jq 'select(.["@l"] == "Error")'
```

👉 **Affiche uniquement les erreurs en temps réel**

---

## 🆚 Comparaison avant/après

### Scénario : Debug d'une erreur en production

#### ❌ AVANT (Sans Serilog)

**Logs disponibles** :
```
info: Program[0]
      User signup started
info: SignupService[0]
      Creating client
error: SignupService[0]
      An error occurred
      System.TimeoutException: The operation has timed out.
```

**Problèmes** :
- ❌ Quel utilisateur ? → Impossible à savoir
- ❌ À quelle heure exactement ? → Pas de timestamp précis
- ❌ Sur quel thread ? → Pas d'info
- ❌ Avec quelle requête HTTP ? → Pas de corrélation
- ❌ Filtrer par email ? → Impossible (texte brut)

**Temps de diagnostic** : 🕐 30-60 minutes (chercher manuellement dans les logs)

---

#### ✅ APRÈS (Avec Serilog)

**Logs disponibles** :
```json
{
  "@t": "2025-10-28T00:58:14.7597315Z",
  "@mt": "UC01_SIGNUP_START - Début inscription pour {Email}",
  "@tr": "2966dd20d67e6562e818127825978682",
  "Email": "test@example.com",
  "ThreadId": 25,
  "RequestPath": "/api/v1/signup"
}
{
  "@t": "2025-10-28T00:58:14.8920000Z",
  "@l": "Error",
  "@mt": "UC01_SIGNUP_ERROR - Échec inscription pour {Email}",
  "@tr": "2966dd20d67e6562e818127825978682",
  "@x": "System.TimeoutException: The operation has timed out...",
  "Email": "test@example.com",
  "ThreadId": 25
}
```

**Solutions** :
- ✅ Quel utilisateur ? → `"Email": "test@example.com"`
- ✅ À quelle heure ? → `"@t": "2025-10-28T00:58:14.892Z"` (milliseconde)
- ✅ Sur quel thread ? → `"ThreadId": 25`
- ✅ Avec quelle requête ? → `"@tr": "2966dd20..."` (TraceId)
- ✅ Filtrer par email ? → `jq 'select(.Email == "test@example.com")'`

**Temps de diagnostic** : 🕐 2-5 minutes (requête `jq` instantanée)

---

### Tableau comparatif complet

| **Aspect** | **Sans Serilog (ILogger)** | **Avec Serilog** |
|------------|----------------------------|------------------|
| **Format de sortie** | Texte brut multiligne | JSON 1 ligne par événement |
| **Parsing automatique** | ❌ Regex complexe | ✅ `jq`, Elasticsearch, Splunk |
| **Recherche par propriété** | ❌ `grep` approximatif | ✅ `jq 'select(.Email == "x")'` |
| **Corrélation de requêtes** | ❌ Impossible | ✅ TraceId unique par requête |
| **Contexte automatique** | ❌ Manquant | ✅ ThreadId, MachineName, TraceId |
| **Filtrage par namespace** | ❌ Global seulement | ✅ `Microsoft.*: Warning` |
| **Destinations multiples** | ❌ Console uniquement | ✅ Console + Fichiers + Services externes |
| **Rotation de fichiers** | ❌ Manuelle | ✅ Automatique (par jour, 7 jours rétention) |
| **Analyse temps réel** | ❌ `tail -f` limité | ✅ Stream JSON avec filtres |
| **Intégration monitoring** | ❌ Difficile | ✅ Elasticsearch, Grafana Loki, Datadog |
| **Temps de diagnostic** | 🕐 30-60 min | 🕐 2-5 min |

---

## 🎯 Pourquoi c'est critique pour Phase 2 ?

### Phase 2a : Observabilité complète

L'observabilité repose sur **3 piliers** :

```
┌────────────────────────────────────────────────────────────┐
│                    OBSERVABILITÉ                            │
├────────────────────────────────────────────────────────────┤
│                                                             │
│  1️⃣ METRICS (Prometheus + Grafana)                         │
│     → Combien de requêtes/sec ?                            │
│     → Latence P95 ?                                        │
│     → Taux d'erreur ?                                      │
│     → CPU / Mémoire ?                                      │
│                                                             │
│  2️⃣ LOGS (Serilog + JSON)                                  │
│     → Pourquoi cette requête a échoué ?                    │
│     → Quel utilisateur est affecté ?                       │
│     → Quelle donnée a causé l'erreur ?                     │
│     → Quel est le contexte complet ?                       │
│                                                             │
│  3️⃣ TRACES (TraceId + SpanId)                              │
│     → Quelle est la chaîne d'appels ?                      │
│     → Combien de temps chaque étape ?                      │
│     → Où est le bottleneck ?                               │
│                                                             │
└────────────────────────────────────────────────────────────┘
```

**Exemple concret** :

1. **Prometheus** te dit : "Taux d'erreur 500 = 15% sur les 5 dernières minutes"
2. **Grafana** te montre : "C'est sur `/api/v1/deposit` uniquement"
3. **Serilog** te donne : "Erreur = Timeout SQL sur la table `Portefeuille` pour le client `abc123`"

👉 **Sans Serilog** : Tu sais qu'il y a un problème, mais **pas pourquoi ni pour qui**.

---

### Phase 2b : Load Balancing (4 instances d'API)

Quand tu as **plusieurs instances d'API** derrière un load balancer :

```
                    ┌─────────────┐
Requête HTTP ──────>│   NGINX LB  │
                    └─────────────┘
                          │
         ┌────────────────┼────────────────┐
         ↓                ↓                ↓
    ┌────────┐       ┌────────┐      ┌────────┐
    │ API-1  │       │ API-2  │      │ API-3  │
    │ :5001  │       │ :5002  │      │ :5003  │
    └────────┘       └────────┘      └────────┘
```

**Problème sans Serilog** :
```
api-1 | User signup started
api-2 | Creating client
api-3 | Database timeout
api-1 | User signup failed
```

❌ **Impossible de savoir** :
- Est-ce la même requête ou 4 utilisateurs différents ?
- Quelle instance a traité quelle étape ?
- La requête a-t-elle été retryée ?

**Solution avec Serilog (TraceId)** :
```json
api-1 | {"@tr": "abc123", "Email": "user@test.com", "@mt": "UC01_SIGNUP_START"}
api-2 | {"@tr": "abc123", "Email": "user@test.com", "@mt": "Creating client"}
api-3 | {"@tr": "abc123", "Email": "user@test.com", "@mt": "DB_TIMEOUT"}
api-1 | {"@tr": "abc123", "Email": "user@test.com", "@mt": "UC01_SIGNUP_ERROR"}
```

✅ **Corrélation claire** :
- Même TraceId (`abc123`) = Même requête
- Requête a traversé 3 instances (api-1 → api-2 → api-3 → api-1)
- Échec sur api-3 (timeout DB) pour `user@test.com`

---

## ❓ FAQ (Questions fréquentes)

### Q1 : Pourquoi pas juste `Console.WriteLine()` ?

**Réponse** :
```csharp
// ❌ Console.WriteLine
Console.WriteLine("User signup: " + email);
```

**Problèmes** :
- ❌ Pas de timestamp automatique
- ❌ Pas de niveau (Info/Warning/Error)
- ❌ Pas de contexte (ThreadId, TraceId)
- ❌ Pas de structure (impossible à filtrer)
- ❌ Pas de rotation de fichiers
- ❌ Pas de destinations multiples

---

### Q2 : Pourquoi pas `ILogger<T>` natif de .NET ?

**Réponse** :

`ILogger<T>` est bien, mais **limité** :

```csharp
// ✅ ILogger natif fonctionne
_logger.LogInformation("User {Email} signed up", email);
```

**Mais** :
- ❌ Format par défaut = Texte brut (pas JSON)
- ❌ Pas de destinations multiples natives (console uniquement)
- ❌ Pas d'enrichers automatiques (ThreadId, MachineName)
- ❌ Pas de rotation de fichiers native
- ❌ Pas de contrôle fin par namespace

**Solution** : Serilog **remplace** `ILogger` avec `builder.Host.UseSerilog()`, donc tu gardes la même API `_logger.LogInformation()`, mais avec tous les avantages de Serilog !

---

### Q3 : Est-ce que Serilog est lent ?

**Réponse** : **Non**, Serilog est optimisé :

- ✅ **Écriture asynchrone** : N'bloque pas le thread principal
- ✅ **Buffering** : Groupe les écritures pour réduire les I/O
- ✅ **Lazy evaluation** : Les propriétés ne sont évaluées que si le niveau de log est actif

**Benchmark** (1 million de logs) :
- `Console.WriteLine()` : ~800ms
- `ILogger` natif : ~850ms
- **Serilog** : ~900ms (différence négligeable)

**Impact sur latence API** : < 0.1ms par requête

---

### Q4 : Pourquoi JSON et pas texte ?

**Réponse** : **JSON = Standard universel**

| **Format** | **Lisible humain** | **Parsable machine** | **Intégrations** |
|------------|-------------------|---------------------|------------------|
| Texte brut | ✅ Oui | ❌ Difficile (regex) | ❌ Limité |
| **JSON** | ⚠️ Avec `jq` | ✅ Facile | ✅ Universel |

**Outils compatibles JSON** :
- Elasticsearch / Kibana
- Grafana Loki
- Splunk
- Datadog
- Azure Application Insights
- AWS CloudWatch Logs Insights
- `jq` (CLI)

**Exemple** : Envoyer les logs à Elasticsearch pour recherche full-text :
```csharp
.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://elasticsearch:9200")))
```

---

### Q5 : Peut-on envoyer les logs ailleurs que console/fichier ?

**Réponse** : **Oui**, Serilog a des **dizaines de sinks** :

```csharp
// Elasticsearch
.WriteTo.Elasticsearch(...)

// Seq (serveur de logs centralisé)
.WriteTo.Seq("http://seq:5341")

// Datadog
.WriteTo.Datadog(apiKey, source: "brokerx-api")

// Azure Application Insights
.WriteTo.ApplicationInsights(telemetryConfiguration, TelemetryConverter.Traces)

// Grafana Loki
.WriteTo.GrafanaLoki("http://loki:3100")

// Slack (pour alertes)
.WriteTo.Slack(webhookUrl, restrictedToMinimumLevel: LogEventLevel.Error)

// Email (pour erreurs critiques)
.WriteTo.Email(...)
```

**Configuration multi-destinations** :
```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new CompactJsonFormatter())    // Stdout Docker
    .WriteTo.File(...)                              // Fichier local
    .WriteTo.Elasticsearch(...)                     // Centralisation
    .WriteTo.Slack(..., restrictedToMinimumLevel: LogEventLevel.Fatal)  // Alertes
    .CreateLogger();
```

---

### Q6 : Quelle est la différence entre `@t`, `@mt`, `@tr`, `@l` ?

**Réponse** : **Conventions Serilog Compact JSON** :

| **Champ** | **Signification** | **Exemple** |
|-----------|------------------|-------------|
| `@t` | **Timestamp** (ISO 8601) | `"2025-10-28T00:58:15.8700163Z"` |
| `@mt` | **Message Template** | `"UC01_SIGNUP_SUCCESS - {ClientId}"` |
| `@m` | **Message Rendu** (avec valeurs) | `"UC01_SIGNUP_SUCCESS - abc123"` |
| `@l` | **Level** (si != Information) | `"Error"`, `"Warning"`, `"Fatal"` |
| `@tr` | **TraceId** (correlation) | `"2966dd20d67e6562e818127825978682"` |
| `@sp` | **SpanId** (sous-opération) | `"125be543cf3bdb96"` |
| `@x` | **Exception** (stacktrace) | `"System.TimeoutException: ..."` |

**Propriétés custom** (sans `@`) :
```json
{
  "ClientId": "02dd16f7-9b6a-44a0-9f9b-276d111b506c",
  "Email": "test@example.com",
  "ThreadId": 25
}
```

---

## 🎓 Conclusion

### Ce que tu dois retenir

1. **Serilog = Logs structurés JSON** pour observabilité production
2. **Configuration avant `builder`** pour capturer tous les logs
3. **Message templates** (`{Email}`) au lieu de string interpolation
4. **Enrichers automatiques** : ThreadId, MachineName, TraceId
5. **Destinations multiples** : Console + Fichiers + Services externes
6. **Filtrage par namespace** : Réduire le bruit (`Microsoft.*: Warning`)
7. **Rotation automatique** : Nouveau fichier chaque jour, rétention 7 jours
8. **Corrélation de requêtes** : TraceId unique pour suivre une requête

### Prochaines étapes

- ✅ **Étape 2a - Observabilité** : Prometheus ✅ + Grafana ✅ + Serilog ✅
- ⏳ **Prochaine** : k6 (tests de charge)
- 🔮 **Bonus** : Intégrer Grafana Loki pour visualiser les logs dans des dashboards

---

## 📚 Ressources

- [Documentation officielle Serilog](https://serilog.net/)
- [Liste des sinks Serilog](https://github.com/serilog/serilog/wiki/Provided-Sinks)
- [Compact JSON format](https://github.com/serilog/serilog-formatting-compact)
- [Structured logging best practices](https://serilog.net/serilog-best-practices/)
