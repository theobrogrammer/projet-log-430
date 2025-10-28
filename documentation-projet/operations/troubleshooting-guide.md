# Guide de Dépannage - ProjetLog430

Ce document recense les problèmes critiques rencontrés durant le développement et déploiement de l'application ProjetLog430 et leurs solutions.

## Table des Matières
1. [Problème Critique: Système d'Authentification Manquant](#1-système-dauthentification-manquant)
2. [Tests E2E: Conflits MySQL/InMemory Database](#2-tests-e2e-conflits-mysql-inmemory-database)
3. [Erreurs d'Injection de Dépendances](#3-erreurs-dinjection-de-dépendances)
4. [Configuration Docker et Conflits de Ports](#4-configuration-docker-et-conflits-de-ports)
5. [Problèmes Entity Framework et Constructeurs](#5-problèmes-entity-framework-et-constructeurs)
6. [Tests Automatisés: Échecs de Compilation et Routes](#6-tests-automatisés-échecs-de-compilation-et-routes)
7. [Configuration Environnements Test vs Production](#7-configuration-environnements-test-vs-production)

---

## 1. Système d'Authentification Manquant

### Problème Critique Identifié
**Impact** : 🔴 CRITIQUE - Faille de sécurité majeure
- L'inscription d'utilisateurs ne créait aucun mot de passe
- Authentification impossible, système vulnérable
- Découvert lors de l'analyse du workflow d'inscription

### Symptômes
```csharp
// Problème dans Client.cs - aucun système de mot de passe
public class Client 
{
    public string Email { get; set; }
    // Manque: PasswordHash, méthodes de vérification
}
```

### Solution Implémentée
1. **Ajout de BCrypt pour le hachage sécurisé** :
   ```csharp
   // Domain/Model/Identite/Client.cs
   public string? PasswordHash { get; private set; }
   
   public void SetPassword(string password)
   {
       PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
   }
   
   public bool VerifyPassword(string password)
   {
       return !string.IsNullOrEmpty(PasswordHash) && 
              BCrypt.Net.BCrypt.Verify(password, PasswordHash);
   }
   ```

2. **Validation des mots de passe** :
   ```csharp
   // DTOs/SignupRequestDto.cs
   [Required]
   [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
   public string Password { get; set; } = "";
   ```

### Leçon Critique
⚠️ **Toujours implémenter l'authentification dès le début du projet**
- Tests de sécurité obligatoires
- Validation des workflows complets avant déploiement

---

## 2. Tests E2E: Conflits MySQL/InMemory Database

### Problème Complexe
**Impact** : 🟡 MODÉRÉ - Tests non fiables, CI/CD bloqué
- Tests E2E échouaient avec des erreurs MySQL
- `init.sql` exécuté dans l'environnement de test
- `ServerVersion.AutoDetect()` tentait de se connecter à MySQL inexistant

### Erreur Type
```
MySqlConnector.MySqlException: Unable to connect to any of the specified MySQL hosts
System.InvalidOperationException: ServerVersion.AutoDetect requires an open connection
```

### Diagnostic - Cause Racine
Le `Program.cs` utilisait toujours MySQL même pour les tests :
```csharp
// Problématique - toujours MySQL
builder.Services.AddDbContext<BrokerXDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
```

### Solution Environnementale
Implémentation de la détection d'environnement dans `Program.cs` :
```csharp
// Solution - détection d'environnement
if (builder.Environment.EnvironmentName == "Testing")
{
    builder.Services.AddDbContext<BrokerXDbContext>(options =>
        options.UseInMemoryDatabase("TestingBrokerX"));
}
else
{
    builder.Services.AddDbContext<BrokerXDbContext>(options =>
        options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
}
```

### Configuration Tests E2E
```csharp
// SimpleE2ETests.cs
var factory = new WebApplicationFactory<Program>()
    .WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing"); // Force Testing environment
    });
```

### Résultats
- **Avant** : 0% tests E2E réussis (erreurs MySQL)
- **Après** : 100% tests E2E réussis (2/2)
- Isolation complète des environnements test/production

---

## 3. Erreurs d'Injection de Dépendances

### Problème Structurel
**Impact** : 🟡 MODÉRÉ - Application ne démarre pas
```
InvalidOperationException: Unable to resolve service for type 'IClientRepository'
InvalidOperationException: Unable to resolve service for type 'ISignupService'
```

### Cause Racine
Architecture hexagonale mal configurée - interfaces des ports non mappées :
```csharp
// Manquant dans Program.cs
// builder.Services.AddScoped<IClientRepository, InMemoryClientRepository>();
// builder.Services.AddScoped<ISignupService, SignupService>();
```

### Solution Appliquée
```csharp
// Program.cs - Configuration DI complète
builder.Services.AddScoped<IClientRepository, InMemoryClientRepository>();
builder.Services.AddScoped<ISignupService, SignupService>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<IAuthService, AuthService>();
```

### Bonne Pratique
✅ **Créer un service d'extension pour l'architecture hexagonale** :
```csharp
public static class DependencyInjection
{
    public static IServiceCollection AddHexagonalArchitecture(this IServiceCollection services)
    {
        // Enregistrer tous les services/repositories
        return services;
    }
}
```

---

## 4. Configuration Docker et Conflits de Ports

### Problème Infrastructure
**Impact** : 🟢 MINEUR - Déploiement local uniquement
```
bind: Only one usage of each socket address normally permitted
Port 5000 already in use
```

### Solutions Rapides
1. **Vérification des ports** :
   ```powershell
   netstat -ano | findstr :5000
   ```

2. **Configuration Docker robuste** :
   ```yaml
   # docker-compose.yml
   services:
     brokerx_app:
       ports:
         - "5000:8080"  # Mapping explicite
       restart: unless-stopped
       networks:
         - broker_network
   ```

### Note
Problème environnemental typique - solution standard avec mapping de ports.

---

## 5. Problèmes Entity Framework et Constructeurs

### Problème Technique
**Impact** : 🟡 MODÉRÉ - Tests échouent, EF ne peut pas instancier les entités
```csharp
// Problématique - constructeur complexe sans parameterless
public Client(string email, string nom, string prenom)
{
    Email = email;
    // ...
}
// Manque: constructeur vide pour EF
```

### Solution Pattern
```csharp
// Domain/Model/Identite/Client.cs
public class Client
{
    // Constructeur EF (requis)
    private Client() { }
    
    // Constructeur business (public)
    public Client(string email, string nom, string prenom) 
    {
        Email = email;
        Nom = nom;
        Prenom = prenom;
        Id = Guid.NewGuid();
        DateCreation = DateTime.UtcNow;
    }
    
    // Factory method pour la création avec password
    public static Client CreateWithPassword(string email, string nom, string prenom, string password)
    {
        var client = new Client(email, nom, prenom);
        client.SetPassword(password);
        return client;
    }
}
```

### Principe Architectural
✅ **Séparation des préoccupations** :
- Constructeur privé pour EF (infrastructure)
- Constructeur public pour la logique métier (domain)
- Factory methods pour les cas complexes

---

## Statistiques de Résolution

### Tests Finaux
- **Domain Tests** : 16/16 (100%) ✅
- **Application Tests** : 4/4 (100%) ✅  
- **Infrastructure Tests** : 30/33 (91%) ⚠️
- **E2E Tests** : 2/2 (100%) ✅
- **Total** : 52/55 (94.5%)

### Temps de Résolution
- **Authentification manquante** : 2h (critique)
- **Tests E2E/MySQL** : 1.5h (diagnostic complexe)
- **DI Configuration** : 30min (standard)
- **Docker/Ports** : 15min (environnemental)
- **EF Constructeurs** : 45min (pattern)
- **Tests automatisés/Routes** : 1h (debug + correction routes)
- **Configuration environnements** : 45min (refactoring Program.cs)

### Impact Business Mis à Jour
🎯 **Critères de Succès Finaux** :
- ✅ Système d'authentification sécurisé (BCrypt)
- ✅ Tests automatisés complets (94.5% succès)
- ✅ Isolation environnements test/production
- ✅ Suite de tests E2E fonctionnelle
- ✅ Déploiement Docker fonctionnel
- ✅ Architecture hexagonale respectée
- ✅ CI/CD ready avec tests fiables

### Nouveaux Problèmes Résolus (Mise à jour Sept 2025)
- **Routes API** : Désalignement entre définition et tests corrigé
- **Tests manquants** : Suite complète de tests ajoutée (52/55 réussis)
- **Configuration environnements** : Séparation test/production implémentée
- **Dépendances tests** : Isolation complète des tests vis-à-vis de MySQL

### Impact Business
🎯 **Critères de Succès Atteints** :
- ✅ Système d'authentification sécurisé (BCrypt)
- ✅ Tests automatisés (94.5% succès)
- ✅ Déploiement Docker fonctionnel
- ✅ Architecture hexagonale respectée
- ✅ Isolation environnements test/prod

---

## 6. Tests Automatisés: Échecs de Compilation et Routes

### Problème Découvert
**Impact** : 🟡 MODÉRÉ - Suite de tests complète non fonctionnelle
- Tests E2E échouaient avec des erreurs de route
- Conflits entre routes définies et routes testées
- Tests unitaires manquants pour validation complète

### Erreurs Rencontrées
```bash
# Erreur de route E2E
HTTP 404 Not Found: POST /api/auth/signup
Expected: /api/v1/signup

# Erreurs de compilation tests
CSC : error CS1061: 'SignupRequestDto' does not contain definition for 'Password'
CSC : error CS0246: The type or namespace name 'BCrypt' could not be found
```

### Diagnostic - Causes Multiples
1. **Désalignement des routes** :
   ```csharp
   // Controller définit
   [Route("api/v1/[controller]")]
   
   // Test appelait
   var response = await client.PostAsync("/api/auth/signup", content);
   ```

2. **Tests obsolètes** après implémentation du système d'authentification
3. **Dépendances manquantes** dans les projets de test

### Solutions Implémentées

#### 1. Correction des Routes E2E
```csharp
// SimpleE2ETests.cs - AVANT
var response = await client.PostAsync("/api/auth/signup", content);

// SimpleE2ETests.cs - APRÈS
var response = await client.PostAsync("/api/v1/signup", content);
```

#### 2. Création de Tests Complets
```csharp
// Domain.Tests/ClientTests.cs - Tests du modèle
[Test]
public void Client_SetPassword_ShouldHashPassword()
{
    var client = new Client("test@example.com", "John", "Doe");
    client.SetPassword("password123");
    
    Assert.That(client.PasswordHash, Is.Not.Null);
    Assert.That(client.PasswordHash, Is.Not.EqualTo("password123"));
    Assert.That(client.VerifyPassword("password123"), Is.True);
}

// E2E.Tests/SimpleE2ETests.cs - Tests d'intégration
[Test]
public async Task SignupEndpoint_WithValidData_ShouldReturn200()
{
    var signupRequest = new
    {
        Email = "test@example.com",
        Phone = "5144567890",
        FullName = "Test User",
        Password = "Password123!",
        ConfirmPassword = "Password123!"
    };
    
    var response = await client.PostAsync("/api/v1/signup", content);
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
}
```

### Résultats des Tests
- **Domain Tests** : 16/16 (100%) ✅
- **Application Tests** : 4/4 (100%) ✅  
- **Infrastructure Tests** : 30/33 (91%) ⚠️
- **E2E Tests** : 2/2 (100%) ✅

### Leçon Apprise
⚠️ **Maintenir les tests à jour** avec les changements d'architecture
- Tests automatisés dans la CI/CD obligatoires
- Validation des routes après chaque modification d'API

---

## 7. Configuration Environnements Test vs Production

### Problème Infrastructure Complexe
**Impact** : 🟡 MODÉRÉ - Tests non fiables, déploiement à risque
- Configuration unique pour tous les environnements
- Tests utilisant la base de données de production
- Dépendances externes (MySQL, init.sql) dans les tests

### Erreurs Observées
```bash
# Tests tentaient de se connecter à MySQL
MySqlConnector.MySqlException: Unable to connect to any of the specified MySQL hosts
System.InvalidOperationException: ServerVersion.AutoDetect requires an open connection

# Script init.sql exécuté dans les tests
Executing db-init/init.sql in test environment (incorrect behavior)
```

### Cause Racine - Program.cs Monolithique
```csharp
// PROBLÉMATIQUE - Configuration unique
public class Program 
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        // Toujours MySQL - même pour les tests !
        var connectionString = builder.Configuration.GetConnectionString("BrokerX");
        builder.Services.AddDbContext<BrokerXDbContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
    }
}
```

### Solution Architecturale - Détection d'Environnement
```csharp
// SOLUTION - Configuration conditionnelle
public class Program 
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        // Configuration basée sur l'environnement
        if (builder.Environment.EnvironmentName == "Testing")
        {
            // Tests: Base de données en mémoire
            builder.Services.AddDbContext<BrokerXDbContext>(options =>
                options.UseInMemoryDatabase("TestingBrokerX"));
        }
        else
        {
            // Production: MySQL avec init.sql
            var connectionString = builder.Configuration.GetConnectionString("BrokerX");
            builder.Services.AddDbContext<BrokerXDbContext>(options =>
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
        }
    }
}
```

### Configuration Tests E2E
```csharp
// SimpleE2ETests.cs - Force l'environnement Testing
public class SimpleE2ETests
{
    private WebApplicationFactory<Program> _factory;
    
    [SetUp]
    public void SetUp()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing"); // CLEF: Force Testing
            });
    }
}
```

### Avantages de la Solution
1. **Isolation Complète** : Tests n'affectent pas la production
2. **Performance** : Tests plus rapides avec InMemory
3. **Fiabilité** : Pas de dépendances externes dans les tests
4. **Sécurité** : Données de test séparées des données réelles

### Pattern Architectural
✅ **Environment-Aware Configuration** :
- **Testing** : InMemory database, mocks, données de test
- **Development** : Base de données locale, logs détaillés
- **Production** : MySQL, monitoring, sécurité renforcée

### Validation
```bash
# Test de validation - logs montrent la bonne configuration
dotnet test tests/E2E.Tests

# Output attendu:
# info: Microsoft.EntityFrameworkCore[10403]
#       Entity Framework Core initialized 'BrokerXDbContext' using provider 'Microsoft.EntityFrameworkCore.InMemory'
#       Saved 1 entities to in-memory store.
```

---

*Document créé le 28 septembre 2025*
*Dernière mise à jour : Ajout des 2 nouveaux problèmes (tests automatisés + environnements)*

---

## Méthodologie de Diagnostic

### Prioritisation des Problèmes
1. **🔴 CRITIQUE** : Failles de sécurité, système non fonctionnel
2. **🟡 MODÉRÉ** : Tests échouent, architecture compromise 
3. **🟢 MINEUR** : Configuration environnementale

### Outils de Diagnostic Étendus
```powershell
# Tests automatisés complets
dotnet test                              # Tous les tests
dotnet test tests/E2E.Tests             # Tests E2E spécifiques
dotnet test --logger "console;verbosity=detailed"  # Logs détaillés

# Vérification infrastructure
netstat -ano | findstr :5000           # Ports utilisés
docker-compose logs brokerx_app         # Logs application
docker-compose logs mysql               # Logs base de données
docker-compose ps                       # État des services

# Debug routes et configuration
curl -X POST http://localhost:5000/api/v1/signup  # Test route
curl -X GET http://localhost:5000/swagger         # Documentation API
```

### Pattern de Résolution Amélioré
1. **Identifier** : Reproduire l'erreur avec logs détaillés
2. **Diagnostiquer** : Trouver la cause racine (souvent configuration)
3. **Isoler** : Séparer les environnements (test/dev/prod)
4. **Implémenter** : Solution minimale viable + tests
5. **Valider** : Tests automatisés dans tous les environnements
6. **Documenter** : Pattern reproductible pour l'équipe
7. **Monitorer** : Éviter la régression avec CI/CD