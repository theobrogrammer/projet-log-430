# Injection de Dépendances (DI) dans BrokerX

## Question : D'où vient `_signup` dans le SignupController ?

### Réponse courte
**ASP.NET Core** instancie automatiquement le `SignupController` et injecte `ISignupUseCase` via le **conteneur IoC (Inversion of Control)**.

---

## Le flux complet expliqué

### 1️⃣ Configuration dans `Program.cs` (Point d'entrée)

**Fichier : `Infrastructure.Web/Program.cs`**

```csharp
var builder = WebApplication.CreateBuilder(args);

// ========================================
// ENREGISTREMENT DES SERVICES (Configuration DI)
// ========================================

// Controllers (tous les contrôleurs découverts automatiquement)
builder.Services.AddControllers();

// ===== PORTS ENTRANTS (Use Cases) =====
builder.Services.AddScoped<ISignupUseCase, SignupService>();
//                         ↑ Interface      ↑ Implémentation
//                         (ce que demande) (ce qui sera fourni)

builder.Services.AddScoped<IAuthUseCase, AuthService>();
builder.Services.AddScoped<IDepositUseCase, WalletService>();

// ===== REPOSITORIES =====
builder.Services.AddScoped<IClientRepository, InMemoryClientRepository>();
builder.Services.AddScoped<IAccountRepository, InMemoryAccountRepository>();
builder.Services.AddScoped<IPortfolioRepository, InMemoryPortfolioRepository>();
builder.Services.AddScoped<IPayTxRepository, InMemoryPayTxRepository>();
builder.Services.AddScoped<IMfaPolicyRepository, InMemoryMfaPolicyRepository>();
builder.Services.AddScoped<IMfaChallengeRepository, InMemoryMfaChallengeRepository>();
builder.Services.AddScoped<ISessionRepository, InMemorySessionRepository>();

// ===== ADAPTERS SORTANTS =====
builder.Services.AddSingleton<IAuditPort>(
    new StructuredAuditAdapter("logs/audit.jsonl"));

builder.Services.AddSingleton<ILedgerPort>(
    new FileLedgerAdapter("logs/ledger.jsonl"));

builder.Services.AddScoped<IOtpPort>(serviceProvider => 
{
    var audit = serviceProvider.GetRequiredService<IAuditPort>();
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    
    var smtpConfig = new SmtpConfig(
        Host: config["Smtp:Host"] ?? "smtp.gmail.com",
        Port: int.Parse(config["Smtp:Port"] ?? "587"),
        User: config["Smtp:User"] ?? "",
        Password: config["Smtp:Password"] ?? "",
        FromEmail: config["Smtp:FromEmail"] ?? "noreply@brokerx.com",
        FromName: config["Smtp:FromName"] ?? "BrokerX Security"
    );
    
    return new HybridEmailOtpAdapter(audit, smtpConfig);
});

builder.Services.AddSingleton<ISessionPort, JwtSessionAdapter>();
builder.Services.AddSingleton<IKycPort, KycAdapterSim>();
builder.Services.AddHttpClient<PaymentAdapterSim>();
builder.Services.AddSingleton<IPaymentPort>(sp => sp.GetRequiredService<PaymentAdapterSim>());

var app = builder.Build();
app.MapControllers(); // Active le routing des contrôleurs
app.Run();
```

---

### 2️⃣ Déclaration du Controller

**Fichier : `Infrastructure.Web/Controllers/SignupController.cs`**

```csharp
[ApiController]
[Route("api/v1/signup")]
public sealed class SignupController : ControllerBase
{
    private readonly ISignupUseCase _signup;

    // ===== INJECTION PAR CONSTRUCTEUR =====
    public SignupController(ISignupUseCase signup)
    {
        _signup = signup;
    }
    
    [HttpPost]
    public async Task<ActionResult<SignupResponseDto>> Signup(
        [FromBody] SignupRequestDto dto,
        CancellationToken ct)
    {
        // Utilisation de _signup (injecté automatiquement)
        var result = await _signup.CreateAccountAsync(...);
        return Ok(result);
    }
}
```

**Ce qui se passe :**
1. ASP.NET Core **détecte** que `SignupController` demande `ISignupUseCase` dans son constructeur
2. Il **regarde** dans le conteneur DI : "Qui implémente `ISignupUseCase` ?"
3. Il **trouve** : `SignupService` (enregistré dans `Program.cs`)
4. Il **instancie** `SignupService` (avec TOUTES ses dépendances récursivement)
5. Il **injecte** l'instance dans `SignupController`

---

### 3️⃣ Déclaration du Service

**Fichier : `Application/Services/SignupService.cs`**

```csharp
public sealed class SignupService : ISignupUseCase
{
    private readonly IClientRepository _clients;
    private readonly IAccountRepository _comptes;
    private readonly IPortfolioRepository _portefeuilles;
    private readonly IKycPort _kyc;
    private readonly IOtpPort _otp;
    private readonly IAuditPort _audit;
    private readonly IMfaPolicyRepository _mfaPolicies;

    // ===== INJECTION PAR CONSTRUCTEUR =====
    public SignupService(
        IClientRepository clients,
        IAccountRepository comptes,
        IPortfolioRepository portefeuilles,
        IKycPort kyc,
        IOtpPort otp,
        IAuditPort audit,
        IMfaPolicyRepository mfaPolicies)
    {
        _clients = clients;
        _comptes = comptes;
        _portefeuilles = portefeuilles;
        _kyc = kyc;
        _otp = otp;
        _audit = audit;
        _mfaPolicies = mfaPolicies;
    }

    public async Task<SignupResult> CreateAccountAsync(...)
    {
        // Utilisation des dépendances injectées
        await _clients.AddAsync(client, ct);
        await _otp.SendContactOtpAsync(...);
        await _audit.WriteAsync(...);
        // etc.
    }
}
```

**Ce qui se passe :**
1. ASP.NET Core **voit** que `SignupService` demande 7 dépendances
2. Il **résout** chacune récursivement :
   - `IClientRepository` → `InMemoryClientRepository`
   - `IAccountRepository` → `InMemoryAccountRepository`
   - `IPortfolioRepository` → `InMemoryPortfolioRepository`
   - `IKycPort` → `KycAdapterSim`
   - `IOtpPort` → `HybridEmailOtpAdapter` (créé avec factory)
   - `IAuditPort` → `StructuredAuditAdapter` (singleton déjà créé)
   - `IMfaPolicyRepository` → `InMemoryMfaPolicyRepository`
3. Il **instancie** `SignupService` avec toutes ces dépendances
4. Il **retourne** l'instance au `SignupController`

---

## Diagramme du flux d'instanciation

```
┌─────────────────────────────────────────────────────────────┐
│           1. Requête HTTP arrive                            │
│           POST /api/v1/signup                               │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  2. ASP.NET Core Routing                                    │
│     - Trouve la route : SignupController.Signup()           │
│     - Vérifie : SignupController existe ?                   │
│       → NON, il faut l'instancier                           │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  3. Conteneur DI (ServiceProvider)                          │
│     - Regarde le constructeur de SignupController           │
│     - Trouve : besoin de ISignupUseCase                     │
│     - Cherche dans les enregistrements...                   │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  4. Résolution de ISignupUseCase                            │
│     - Trouvé dans Program.cs:                               │
│       builder.Services.AddScoped<ISignupUseCase,            │
│                                   SignupService>();         │
│     - Doit instancier SignupService                         │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  5. Résolution des dépendances de SignupService             │
│     - Regarde le constructeur de SignupService              │
│     - Trouve : besoin de 7 dépendances                      │
│     - Résout chacune récursivement...                       │
└────────────────────────┬────────────────────────────────────┘
                         │
                ┌────────┴────────┬─────────────┬──────────────┐
                ▼                 ▼             ▼              ▼
    ┌──────────────────┐ ┌──────────────┐ ┌─────────┐ ┌────────────┐
    │IClientRepository │ │IAccountRepo  │ │IOtpPort │ │IAuditPort  │
    │        ↓         │ │      ↓       │ │    ↓    │ │     ↓      │
    │InMemoryClient    │ │InMemoryAcct  │ │Hybrid   │ │Structured  │
    │Repository        │ │Repository    │ │EmailOtp │ │AuditAdapter│
    │(NEW instance)    │ │(NEW instance)│ │Adapter  │ │(singleton) │
    └──────────────────┘ └──────────────┘ └─────────┘ └────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  6. Instanciation de SignupService                          │
│     var service = new SignupService(                        │
│         clientRepo,      // résolu                          │
│         accountRepo,     // résolu                          │
│         portfolioRepo,   // résolu                          │
│         kycPort,         // résolu                          │
│         otpPort,         // résolu                          │
│         auditPort,       // résolu                          │
│         mfaPolicyRepo    // résolu                          │
│     );                                                      │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  7. Instanciation de SignupController                       │
│     var controller = new SignupController(service);         │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  8. Appel de la méthode                                     │
│     controller.Signup(dto, ct);                             │
└─────────────────────────────────────────────────────────────┘
```

---

## Les 3 durées de vie (Lifetimes)

### 🔹 **Transient** (`AddTransient`)
- **Nouvelle instance** à chaque demande
- Utilisé pour services légers, sans état

```csharp
builder.Services.AddTransient<IMyService, MyService>();
// Chaque injection = nouvelle instance
```

### 🔸 **Scoped** (`AddScoped`)
- **Une instance par requête HTTP**
- Partagée entre tous les services de la même requête
- **Utilisé dans BrokerX** pour repositories et services

```csharp
builder.Services.AddScoped<ISignupUseCase, SignupService>();
// SignupService vivra pendant toute la durée de la requête HTTP
// Si 2 classes demandent ISignupUseCase dans la même requête → même instance
```

### 🔺 **Singleton** (`AddSingleton`)
- **Une seule instance** pour toute l'application
- Créée au démarrage, réutilisée partout
- **Utilisé dans BrokerX** pour `IAuditPort`, `ILedgerPort`, etc.

```csharp
builder.Services.AddSingleton<IAuditPort>(
    new StructuredAuditAdapter("logs/audit.jsonl"));
// Une seule instance pour TOUTE l'application
```

---

## Exemple concret avec une requête

### Requête entrante :
```bash
POST /api/v1/signup
Content-Type: application/json
{
  "email": "test@example.com",
  "fullName": "John Doe",
  "password": "SecurePass123!"
}
```

### Ce qui se passe en mémoire :

```
Requête HTTP #1 (Thread #42)
│
├─ SignupController (NEW instance Scoped)
│  └─ _signup: ISignupUseCase
│     │
│     └─ SignupService (NEW instance Scoped)
│        ├─ _clients: IClientRepository
│        │  └─ InMemoryClientRepository (NEW instance Scoped)
│        │
│        ├─ _comptes: IAccountRepository
│        │  └─ InMemoryAccountRepository (NEW instance Scoped)
│        │
│        ├─ _portefeuilles: IPortfolioRepository
│        │  └─ InMemoryPortfolioRepository (NEW instance Scoped)
│        │
│        ├─ _kyc: IKycPort
│        │  └─ KycAdapterSim (SINGLETON réutilisé)
│        │
│        ├─ _otp: IOtpPort
│        │  └─ HybridEmailOtpAdapter (NEW instance Scoped)
│        │     └─ _audit: IAuditPort
│        │        └─ StructuredAuditAdapter (SINGLETON réutilisé)
│        │
│        ├─ _audit: IAuditPort
│        │  └─ StructuredAuditAdapter (SINGLETON réutilisé - MÊME que ci-dessus)
│        │
│        └─ _mfaPolicies: IMfaPolicyRepository
│           └─ InMemoryMfaPolicyRepository (NEW instance Scoped)
│
└─ Fin de la requête HTTP #1
   → TOUTES les instances Scoped sont détruites
   → Les Singletons restent en mémoire
```

---

## Avantages de cette approche

### ✅ 1. Testabilité
```csharp
// Dans les tests unitaires, on peut facilement mocker
var mockSignup = new Mock<ISignupUseCase>();
var controller = new SignupController(mockSignup.Object);
// Pas besoin du vrai service, on contrôle tout !
```

### ✅ 2. Découplage
- `SignupController` ne sait PAS quelle implémentation il utilise
- On peut changer `SignupService` par `SignupServiceV2` sans toucher au controller

### ✅ 3. Configuration centralisée
- Tous les "câblages" sont dans `Program.cs`
- Facile de voir toutes les dépendances du projet

### ✅ 4. Gestion automatique du cycle de vie
- ASP.NET Core gère la création/destruction des instances
- Pas de `new` manuel (évite les memory leaks)

---

## Résumé en 3 points

1. **`Program.cs`** enregistre les mappings (Interface → Implémentation)
   ```csharp
   builder.Services.AddScoped<ISignupUseCase, SignupService>();
   ```

2. **`SignupController`** demande `ISignupUseCase` dans son constructeur
   ```csharp
   public SignupController(ISignupUseCase signup) => _signup = signup;
   ```

3. **ASP.NET Core DI** résout automatiquement et injecte `SignupService`
   - Instancie `SignupService` avec TOUTES ses dépendances
   - Injecte l'instance dans `SignupController`
   - Gère le cycle de vie (Scoped/Singleton/Transient)

---

## Pour aller plus loin

### Configuration dans `appsettings.json`
```json
{
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": "587",
    "User": "your-email@gmail.com",
    "Password": "your-app-password",
    "FromEmail": "noreply@brokerx.com",
    "FromName": "BrokerX Security"
  }
}
```

Ces valeurs sont lues dans `Program.cs` :
```csharp
var smtpConfig = new SmtpConfig(
    Host: config["Smtp:Host"] ?? "smtp.gmail.com",
    Port: int.Parse(config["Smtp:Port"] ?? "587"),
    // ...
);
```

### Voir les services enregistrés (debug)
```csharp
// Dans Program.cs, après builder.Build()
foreach (var service in app.Services.GetServices<ServiceDescriptor>())
{
    Console.WriteLine($"{service.ServiceType.Name} → {service.ImplementationType?.Name}");
}
```

---

**Date** : 27 octobre 2025  
**Auteur** : Équipe BrokerX  
**Version** : 1.0
