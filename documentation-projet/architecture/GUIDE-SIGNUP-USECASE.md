# Guide Complet : Use Case Signup (UC-01)

## Table des matières
1. [Vue d'ensemble de l'architecture](#vue-densemble-de-larchitecture)
2. [Structure du projet](#structure-du-projet)
3. [Flux détaillé du Signup (UC-01)](#flux-détaillé-du-signup-uc-01)
4. [Explication couche par couche](#explication-couche-par-couche)
5. [Diagramme de séquence](#diagramme-de-séquence)
6. [Points clés de l'architecture hexagonale](#points-clés-de-larchitecture-hexagonale)

---

## Vue d'ensemble de l'architecture

Le projet BrokerX suit une **Architecture Hexagonale (Ports & Adapters)** qui sépare clairement :

```
┌─────────────────────────────────────────────────────────────┐
│                    CLIENT (Postman, k6)                     │
└────────────────────────┬────────────────────────────────────┘
                         │ HTTP POST /api/v1/signup
                         ▼
┌─────────────────────────────────────────────────────────────┐
│               ADAPTER ENTRANT (Infrastructure.Web)          │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  SignupController.cs                                 │   │
│  │  - Validation ModelState                             │   │
│  │  - Appelle le port entrant ISignupUseCase           │   │
│  └──────────────────────────────────────────────────────┘   │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                  PORT ENTRANT (Domain)                      │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  ISignupUseCase.cs (interface)                       │   │
│  │  - CreateAccountAsync()                              │   │
│  │  - ResendContactOtpAsync()                           │   │
│  │  - VerifyContactOtpAsync()                           │   │
│  └──────────────────────────────────────────────────────┘   │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│            SERVICE APPLICATION (Application)                │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  SignupService.cs (implémente ISignupUseCase)        │   │
│  │  - Orchestration du Use Case                         │   │
│  │  - Appelle le Domaine (entités métier)              │   │
│  │  - Appelle les ports sortants (dépendances)         │   │
│  └──────────────────────────────────────────────────────┘   │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                  DOMAINE (Domain/Model)                     │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  Client.cs (entité racine agrégat)                   │   │
│  │  - Creer() : factory method                          │   │
│  │  - DemarrerKycSiNecessaire()                         │   │
│  │  - DemarrerOtpActivation()                           │   │
│  │  - OuvrirCompte()                                    │   │
│  │  - ActiverSiAdmissible()                             │   │
│  └──────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  Compte.cs, DossierKYC.cs, VerifContactOTP.cs        │   │
│  │  Portefeuille.cs, PolitiqueMFA.cs                    │   │
│  └──────────────────────────────────────────────────────┘   │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              PORTS SORTANTS (Domain/Ports.Outbound)         │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  IClientRepository (persistance)                     │   │
│  │  IAccountRepository                                  │   │
│  │  IPortfolioRepository                                │   │
│  │  IOtpPort (envoi OTP)                                │   │
│  │  IKycPort (vérification KYC)                         │   │
│  │  IAuditPort (logs d'audit)                           │   │
│  └──────────────────────────────────────────────────────┘   │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│          ADAPTERS SORTANTS (Infrastructure.*)               │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  EfClientRepository (Entity Framework)               │   │
│  │  EfAccountRepository                                 │   │
│  │  EfPortfolioRepository                               │   │
│  │  EmailSmsOtpAdapter (simulateur OTP)                 │   │
│  │  KycAdapterSim (simulateur KYC)                      │   │
│  │  StructuredAuditAdapter (logs JSON)                  │   │
│  └──────────────────────────────────────────────────────┘   │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              SYSTEMES EXTERNES / BASE DE DONNEES            │
│  - MySQL (persistance)                                      │
│  - otp-sim (simulateur d'envoi OTP)                         │
│  - kyc-sim (simulateur de vérification KYC)                 │
└─────────────────────────────────────────────────────────────┘
```

---

## Structure du projet

```
src/
├── Domain/                              # Cœur métier (aucune dépendance externe)
│   ├── Model/                           # Entités du domaine
│   │   ├── Identite/
│   │   │   ├── Client.cs                # Agrégat racine (email, tel, statut)
│   │   │   ├── Compte.cs                # Compte bancaire (accountId, solde)
│   │   │   ├── DossierKYC.cs            # Dossier Know Your Customer
│   │   │   └── VerifContactOTP.cs       # OTP de vérification contact
│   │   ├── PortefeuilleReglement/
│   │   │   ├── Portefeuille.cs          # Portefeuille multi-devises
│   │   │   ├── PayTx.cs                 # Transaction de paiement
│   │   │   └── Ledger.cs                # Registre comptable
│   │   ├── Securite/
│   │   │   ├── Session.cs               # Session utilisateur (JWT)
│   │   │   ├── PolitiqueMFA.cs          # Politique MFA (SMS/Email)
│   │   │   └── DefiMFA.cs               # Défi MFA (challenge-response)
│   │   └── Observabilite/
│   │       └── AuditLog.cs              # Log d'audit (append-only)
│   ├── Ports.Inbound/                   # Interfaces des Use Cases
│   │   ├── ISignupUseCase.cs            # UC-01 : Inscription ✅
│   │   ├── IAuthUseCase.cs              # UC-02 : Authentification ✅
│   │   └── IDepositUseCase.cs           # UC-03 : Dépôt 🔄
│   ├── Ports.Outbound/                  # Interfaces des dépendances
│   │   ├── IClientRepository.cs         # Persistance Client
│   │   ├── IAccountRepository.cs        # Persistance Compte
│   │   ├── IPortfolioRepository.cs      # Persistance Portefeuille
│   │   ├── IOtpPort.cs                  # Envoi OTP (email/SMS)
│   │   ├── IKycPort.cs                  # Vérification KYC
│   │   ├── IAuditPort.cs                # Logs d'audit
│   │   └── ISessionPort.cs              # Gestion sessions JWT
│   └── Contracts/                       # DTOs de résultats
│       ├── SignupResult.cs
│       ├── LoginResult.cs
│       └── OtpVerificationResult.cs
│
├── Application/                         # Services d'orchestration
│   └── Services/
│       ├── SignupService.cs             # Implémente ISignupUseCase ✅
│       ├── AuthService.cs               # Implémente IAuthUseCase ✅
│       └── WalletService.cs             # Implémente IDepositUseCase 🔄
│
├── Infrastructure.Web/                  # Adapter entrant (HTTP)
│   ├── Controllers/
│   │   ├── SignupController.cs          # POST /api/v1/signup
│   │   ├── AuthController.cs            # POST /api/v1/login
│   │   └── WalletController.cs          # POST /api/v1/deposits
│   ├── DTOs/                            # DTOs HTTP (Request/Response)
│   │   ├── SignupRequestDto.cs
│   │   ├── SignupResponseDto.cs
│   │   ├── LoginRequestDto.cs
│   │   └── LoginResponseDto.cs
│   └── Program.cs                       # Configuration ASP.NET + DI
│
├── Infrastructure.Persistence/          # Adapters sortants (DB)
│   └── Repositories/
│       ├── EfClientRepository.cs        # Entity Framework - Client
│       ├── EfAccountRepository.cs       # Entity Framework - Compte
│       └── EfPortfolioRepository.cs     # Entity Framework - Portefeuille
│
└── Infrastructure.Adapters/             # Adapters sortants (externes)
    ├── Otp/
    │   └── EmailSmsOtpAdapter.cs        # Simulateur envoi OTP
    ├── Kyc/
    │   └── KycAdapterSim.cs             # Simulateur vérification KYC
    ├── Session/
    │   └── JwtSessionAdapter.cs         # Gestion JWT
    └── Audit/
        └── StructuredAuditAdapter.cs    # Logs JSON structurés
```

### Principes de l'organisation :

1. **Domain** = Cœur métier pur (pas de dépendances externes)
2. **Application** = Orchestration des Use Cases
3. **Infrastructure** = Adaptations techniques (Web, DB, externes)

---

## Flux détaillé du Signup (UC-01)

### Étapes du flux complet :

```
┌──────────┐
│  CLIENT  │
└────┬─────┘
     │ POST /api/v1/signup
     │ { email, phone, fullName, password, birthDate }
     ▼
┌────────────────────────────────────────────────────────────┐
│ 1. SignupController (Infrastructure.Web)                   │
│    - Validation ModelState (annotations [Required], etc.)  │
│    - Appel : await _signup.CreateAccountAsync(...)         │
└────┬───────────────────────────────────────────────────────┘
     │
     ▼
┌────────────────────────────────────────────────────────────┐
│ 2. SignupService (Application)                             │
│    Implémente ISignupUseCase                               │
└────┬───────────────────────────────────────────────────────┘
     │
     ▼
┌────────────────────────────────────────────────────────────┐
│ 2.1. Créer l'entité Client (Domain/Model/Identite)        │
│      var client = Client.Creer(email, phone, ...)          │
│      - Génère ClientId (UUID)                              │
│      - Hash le password avec BCrypt                        │
│      - Statut initial : Pending                            │
│      - Validation métier (email, phone)                    │
└────┬───────────────────────────────────────────────────────┘
     │
     ▼
┌────────────────────────────────────────────────────────────┐
│ 2.2. Démarrer KYC                                          │
│      client.DemarrerKycSiNecessaire()                      │
│      - Crée un DossierKYC (statut : Pending)               │
│      - KycId (UUID) généré                                 │
└────┬───────────────────────────────────────────────────────┘
     │
     ▼
┌────────────────────────────────────────────────────────────┐
│ 2.3. Démarrer OTP de vérification contact                 │
│      var otp = client.DemarrerOtpActivation(...)           │
│      - Canal : Email (par défaut)                          │
│      - TTL : 10 minutes                                    │
│      - Génère OtpId (UUID)                                 │
│      - Code 6 chiffres généré                              │
│      - Hash du code stocké (sécurité)                      │
└────┬───────────────────────────────────────────────────────┘
     │
     ▼
┌────────────────────────────────────────────────────────────┐
│ 2.4. Persister le Client                                   │
│      await _clients.AddAsync(client, ct)                   │
│      → EfClientRepository (Infrastructure.Persistence)     │
│      → Entity Framework → MySQL                            │
│      Table : Clients (+ DossiersKYC, VerificationsContactOTP) │
└────┬───────────────────────────────────────────────────────┘
     │
     ▼
┌────────────────────────────────────────────────────────────┐
│ 2.5. Ouvrir un Compte                                      │
│      var compte = client.OuvrirCompte()                    │
│      - Génère AccountId (UUID)                             │
│      - Lié au ClientId                                     │
│      - Solde initial : 0.00                                │
│      await _comptes.AddAsync(compte, ct)                   │
│      → Table : Comptes                                     │
└────┬───────────────────────────────────────────────────────┘
     │
     ▼
┌────────────────────────────────────────────────────────────┐
│ 2.6. Ouvrir un Portefeuille                                │
│      var portefeuille = Portefeuille.Ouvrir(accountId,"USD")│
│      - PortfolioId (UUID)                                  │
│      - Devise : USD (démo)                                 │
│      - Solde : 0.00                                        │
│      await _portefeuilles.AddAsync(portefeuille, ct)       │
│      → Table : Portefeuilles                               │
└────┬───────────────────────────────────────────────────────┘
     │
     ▼
┌────────────────────────────────────────────────────────────┐
│ 2.7. Activer politique MFA par défaut                     │
│      var mfaPolicy = PolitiqueMFA.Creer(clientId, SMS, true)│
│      - Type : SMS (utilise système OTP)                    │
│      - Activé par défaut pour sécurité                     │
│      await _mfaPolicies.AddAsync(mfaPolicy, ct)            │
│      → Table : PolitiquesMFA                               │
└────┬───────────────────────────────────────────────────────┘
     │
     ▼
┌────────────────────────────────────────────────────────────┐
│ 2.8. Déclencher vérification KYC (fire-and-forget)        │
│      _ = _kyc.SubmitAsync(clientId, kycId, ct)             │
│      → IKycPort (Domain/Ports.Outbound)                    │
│      → KycAdapterSim (Infrastructure.Adapters)             │
│      → Simulateur KYC (démo : auto-approuve)               │
└────┬───────────────────────────────────────────────────────┘
     │
     ▼
┌────────────────────────────────────────────────────────────┐
│ 2.9. Envoyer OTP par email                                 │
│      await _otp.SendContactOtpAsync(clientId, otpId, ...)  │
│      → IOtpPort (Domain/Ports.Outbound)                    │
│      → EmailSmsOtpAdapter (Infrastructure.Adapters)        │
│      → Console.WriteLine (démo : affiche le code)          │
│      → Audit log : OTP_SENT                                │
└────┬───────────────────────────────────────────────────────┘
     │
     ▼
┌────────────────────────────────────────────────────────────┐
│ 2.10. Log d'audit                                          │
│       await _audit.WriteAsync(AuditLog.Ecrire(...), ct)    │
│       → IAuditPort (Domain/Ports.Outbound)                 │
│       → StructuredAuditAdapter (Infrastructure.Adapters)   │
│       → Table : AuditLogs (append-only)                    │
│       Événement : CLIENT_SIGNUP                            │
│       Payload : { clientId, accountId }                    │
└────┬───────────────────────────────────────────────────────┘
     │
     ▼
┌────────────────────────────────────────────────────────────┐
│ 3. Retour du résultat                                      │
│    return new SignupResult(clientId, accountId, statut)    │
│    → SignupService retourne à SignupController             │
└────┬───────────────────────────────────────────────────────┘
     │
     ▼
┌────────────────────────────────────────────────────────────┐
│ 4. Réponse HTTP 200 OK                                     │
│    {                                                        │
│      "clientId": "uuid",                                   │
│      "accountId": "uuid",                                  │
│      "status": "Pending"                                   │
│    }                                                        │
└────────────────────────────────────────────────────────────┘
```

---

## Explication couche par couche

### 1️⃣ **Infrastructure.Web (Adapter entrant)**

**Fichier : `SignupController.cs`**

```csharp
[ApiController]
[Route("api/v1/signup")]
public sealed class SignupController : ControllerBase
{
    private readonly ISignupUseCase _signup;

    public SignupController(ISignupUseCase signup) => _signup = signup;

    [HttpPost]
    public async Task<ActionResult<SignupResponseDto>> Signup(
        [FromBody] SignupRequestDto dto,
        CancellationToken ct)
    {
        // 1. Validation automatique via ModelState (annotations DTO)
        if (!ModelState.IsValid) return UnprocessableEntity(ModelState);

        // 2. Appel du port entrant (Use Case)
        var result = await _signup.CreateAccountAsync(
            email: dto.Email,
            phone: dto.Phone,
            fullName: dto.FullName,
            password: dto.Password,
            birthDate: dto.BirthDate,
            ct: ct);

        // 3. Mapping du résultat vers DTO de réponse
        var resp = new SignupResponseDto {
            ClientId  = result.ClientId,
            AccountId = result.AccountId,
            Status    = result.Status
        };
        return Ok(resp);
    }
}
```

**Responsabilités :**
- ✅ Validation HTTP (ModelState)
- ✅ Désérialisation JSON → DTO
- ✅ Appel du port entrant (interface)
- ✅ Mapping résultat → DTO réponse
- ❌ **PAS de logique métier** (délégué au service)

---

### 2️⃣ **Domain/Ports.Inbound (Port entrant)**

**Fichier : `ISignupUseCase.cs`**

```csharp
public interface ISignupUseCase
{
    /// <summary>
    /// UC-01 : Inscription — crée le Client, ouvre le DossierKYC, 
    /// lance un OTP de contact, et crée un Compte + Portefeuille.
    /// </summary>
    Task<SignupResult> CreateAccountAsync(
        string email,
        string? phone,
        string fullName,
        string password,
        DateOnly? birthDate,
        CancellationToken ct = default);

    Task ResendContactOtpAsync(Guid clientId, CancellationToken ct = default);
    
    Task<OtpVerificationResult> VerifyContactOtpAsync(
        Guid clientId,
        string code,
        CancellationToken ct = default);
}
```

**Responsabilités :**
- ✅ **Contrat** du Use Case (interface)
- ✅ Définit les opérations métier disponibles
- ✅ Indépendant de HTTP/REST (pourrait être gRPC, GraphQL, etc.)
- ❌ **Pas d'implémentation** (juste le contrat)

---

### 3️⃣ **Application/Services (Service d'orchestration)**

**Fichier : `SignupService.cs`**

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

    public SignupService(/* injection des dépendances */) { ... }

    public async Task<SignupResult> CreateAccountAsync(...)
    {
        // 1. Créer le client (entité domaine)
        var passwordHash = Client.HashPassword(password);
        var client = Client.Creer(email, phone, fullName, passwordHash, birthDate);
        
        // 2. Démarrer KYC
        client.DemarrerKycSiNecessaire();
        
        // 3. Démarrer OTP
        var otp = client.DemarrerOtpActivation(CanalOTP.Email, TimeSpan.FromMinutes(10));

        // 4. Persister client
        await _clients.AddAsync(client, ct);

        // 5. Ouvrir compte
        var compte = client.OuvrirCompte();
        await _comptes.AddAsync(compte, ct);

        // 6. Ouvrir portefeuille
        var portefeuille = Portefeuille.Ouvrir(compte.AccountId, "USD");
        await _portefeuilles.AddAsync(portefeuille, ct);

        // 7. Activer MFA par défaut
        var mfaPolicy = PolitiqueMFA.Creer(client.ClientId, TypeMfa.Sms, true);
        await _mfaPolicies.AddAsync(mfaPolicy, ct);

        // 8. Déclencher KYC (fire-and-forget)
        _ = _kyc.SubmitAsync(client.ClientId, client.Kyc!.KycId, ct);
        
        // 9. Envoyer OTP
        var code = GenererCode6();
        otp.SetCodeHash(code);
        await _clients.UpdateAsync(client, ct); // MAJ avec hash OTP
        await _otp.SendContactOtpAsync(client.ClientId, otp.OtpId, 
                                       CanalOTP.Email, email, code, ct);

        // 10. Audit
        await _audit.WriteAsync(
            AuditLog.Ecrire("CLIENT_SIGNUP", $"user:{email}",
                payload: new { clientId = client.ClientId, 
                              accountId = compte.AccountId }), ct);

        return new SignupResult(client.ClientId, compte.AccountId, 
                                client.Statut.ToString());
    }
}
```

**Responsabilités :**
- ✅ **Orchestration** du Use Case (séquence d'opérations)
- ✅ Appelle les entités domaine (logique métier)
- ✅ Appelle les ports sortants (dépendances)
- ✅ Gère les transactions (via repositories)
- ❌ **PAS de détails techniques** (délégué aux adapters)

---

### 4️⃣ **Domain/Model (Entités métier)**

**Fichier : `Client.cs`**

```csharp
public sealed class Client
{
    public Guid ClientId { get; private set; }
    public string Email { get; private set; }
    public string? Telephone { get; private set; }
    public string NomComplet { get; private set; }
    public DateOnly? DateNaissance { get; private set; }
    public string PasswordHash { get; private set; }
    public StatutClient Statut { get; private set; }
    
    // Relations
    public DossierKYC? Kyc { get; private set; }
    private readonly List<VerifContactOTP> _contactOtps = new();
    private readonly List<Compte> _comptes = new();

    // Factory method (création contrôlée)
    public static Client Creer(string email, string? telephone, 
                                string nomComplet, string passwordHash, 
                                DateOnly? dateNaissance)
    {
        var id = Guid.NewGuid();
        return new Client(id, email, telephone, nomComplet, 
                         dateNaissance, passwordHash);
    }

    // Méthodes métier (comportements)
    public void DemarrerKycSiNecessaire()
    {
        if (Kyc != null) return;
        Kyc = DossierKYC.Ouvrir(ClientId);
    }

    public VerifContactOTP DemarrerOtpActivation(CanalOTP canal, TimeSpan ttl)
    {
        var otp = VerifContactOTP.Creer(ClientId, canal, ttl);
        _contactOtps.Add(otp);
        return otp;
    }

    public Compte OuvrirCompte()
    {
        var compte = Compte.Ouvrir(ClientId);
        _comptes.Add(compte);
        return compte;
    }

    public void ActiverSiAdmissible()
    {
        // Règle métier : KYC Verified + OTP vérifié
        if (Kyc?.Statut == StatutKYC.Verified && 
            _contactOtps.Any(o => o.EstVerifie))
        {
            Statut = StatutClient.Active;
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
    
    // Validation métier
    private static string ValiderEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ArgumentException("Email invalide", nameof(email));
        return email.Trim().ToLowerInvariant();
    }
}
```

**Responsabilités :**
- ✅ **Règles métier** pures (validation, invariants)
- ✅ **Comportements** riches (méthodes métier)
- ✅ **Encapsulation** (private setters, factory methods)
- ✅ **Indépendance totale** (pas de dépendances externes)
- ❌ **Pas de persistance** (délégué aux repositories)

---

### 5️⃣ **Domain/Ports.Outbound (Ports sortants)**

**Fichier : `IOtpPort.cs`**

```csharp
public interface IOtpPort
{
    /// <summary>Envoie un OTP de vérification de contact (email/SMS).</summary>
    Task SendContactOtpAsync(
        Guid clientId,
        Guid otpId,
        CanalOTP canal,
        string destination,  // email ou numéro SMS
        string code,         // valeur OTP à transmettre
        CancellationToken ct = default);
}
```

**Fichier : `IKycPort.cs`**

```csharp
public interface IKycPort
{
    /// <summary>Soumet un dossier KYC au fournisseur.</summary>
    Task SubmitAsync(Guid clientId, Guid kycId, CancellationToken ct = default);

    /// <summary>Interroge l'état courant du KYC.</summary>
    Task<StatutKYC> GetStatusAsync(Guid clientId, Guid kycId, 
                                    CancellationToken ct = default);
}
```

**Responsabilités :**
- ✅ **Contrats** des dépendances externes
- ✅ Définit QUOI (pas COMMENT)
- ✅ Inversé la dépendance (Domain ne dépend pas de l'infra)
- ❌ **Pas d'implémentation** (juste l'interface)

---

### 6️⃣ **Infrastructure.Adapters (Adapters sortants)**

**Fichier : `EmailSmsOtpAdapter.cs`**

```csharp
public sealed class EmailSmsOtpAdapter : IOtpPort
{
    private readonly IAuditPort _audit;
    
    public EmailSmsOtpAdapter(IAuditPort audit) => _audit = audit;

    public async Task SendContactOtpAsync(Guid clientId, Guid otpId, 
                                          CanalOTP canal, string destination, 
                                          string code, CancellationToken ct = default)
    {
        // Démo : affiche le code en console
        Console.WriteLine($"[OTP] canal={canal} dest={destination} " +
                         $"code={code} client={clientId}");
        
        // Log audit
        var payload = JsonSerializer.Serialize(new { 
            clientId, otpId, canal = canal.ToString(), destination, code 
        });
        await _audit.WriteAsync(
            AuditLog.Ecrire("OTP_SENT", "system", payloadJson: payload), ct);
    }
}
```

**Fichier : `KycAdapterSim.cs`**

```csharp
public sealed class KycAdapterSim : IKycPort
{
    public Task SubmitAsync(Guid clientId, Guid kycId, CancellationToken ct = default)
    {
        // Démo : pas d'appel externe, on "accepte" toujours
        return Task.CompletedTask;
    }

    public Task<StatutKYC> GetStatusAsync(Guid clientId, Guid kycId, 
                                          CancellationToken ct = default)
        => Task.FromResult(StatutKYC.Verified);
}
```

**Responsabilités :**
- ✅ **Implémentation concrète** des ports sortants
- ✅ Détails techniques (HTTP, SMTP, APIs externes)
- ✅ Simulateurs pour démo (pas de vrais services)
- ✅ Facilement remplaçables (changement d'adapter)

---

### 7️⃣ **Infrastructure.Persistence (Repositories)**

**Fichier : `EfClientRepository.cs`**

```csharp
public sealed class EfClientRepository : IClientRepository
{
    private readonly BrokerDbContext _db;

    public EfClientRepository(BrokerDbContext db) => _db = db;

    public async Task<Client?> GetByIdAsync(Guid clientId, CancellationToken ct)
        => await _db.Clients
            .Include(c => c.Kyc)
            .Include(c => c.ContactOtps)
            .FirstOrDefaultAsync(c => c.ClientId == clientId, ct);

    public async Task AddAsync(Client client, CancellationToken ct)
    {
        await _db.Clients.AddAsync(client, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Client client, CancellationToken ct)
    {
        _db.Clients.Update(client);
        await _db.SaveChangesAsync(ct);
    }
}
```

**Responsabilités :**
- ✅ **Persistance** via Entity Framework
- ✅ Mapping ORM (entités ↔ tables)
- ✅ Gestion des transactions (SaveChanges)
- ✅ Inclusion des relations (Include)

---

## Diagramme de séquence

```
Client    Controller    Service       Domain        Repositories    Adapters      MySQL
  │           │            │             │               │              │            │
  ├──POST────>│            │             │               │              │            │
  │           ├─validate───┤             │               │              │            │
  │           ├─CreateAccount────────────>│               │              │            │
  │           │            ├─Creer()────>│               │              │            │
  │           │            │<─Client─────┤               │              │            │
  │           │            ├─DemarrerKyc()──────────────>│               │            │
  │           │            ├─DemarrerOtp()──────────────>│               │            │
  │           │            ├─AddAsync(client)──────────>│               │            │
  │           │            │             │               ├─INSERT───────>│            │
  │           │            │             │               │<──OK──────────┤            │
  │           │            ├─OuvrirCompte()─────────────>│               │            │
  │           │            ├─AddAsync(compte)──────────>│               │            │
  │           │            │             │               ├─INSERT───────>│            │
  │           │            ├─Ouvrir(portfolio)──────────>│               │            │
  │           │            ├─AddAsync(portfolio)────────>│               │            │
  │           │            │             │               ├─INSERT───────>│            │
  │           │            ├─SubmitAsync(kyc)──────────────────────────>│            │
  │           │            │             │               │              ├─simulate──>│
  │           │            ├─SendOtp()────────────────────────────────>│            │
  │           │            │             │               │              ├─console───>│
  │           │            ├─WriteAudit()────────────────────────────>│            │
  │           │            │             │               │              ├─INSERT────>│
  │           │<─SignupResult──────────┤               │              │            │
  │<─200 OK────┤            │             │               │              │            │
```

---

## Points clés de l'architecture hexagonale

### ✅ Avantages

1. **Indépendance du domaine**
   - Le cœur métier (`Domain`) ne dépend d'AUCUNE bibliothèque externe
   - Pas de référence à Entity Framework, ASP.NET, etc.

2. **Testabilité**
   - Tous les ports sont des interfaces
   - Mock facile pour tests unitaires
   - Service isolable du framework

3. **Flexibilité**
   - Changer de DB : remplacer `EfClientRepository` par `MongoClientRepository`
   - Changer de transport : REST → gRPC (nouvelle couche adapter)
   - Remplacer simulateurs par vrais services

4. **Clarté des responsabilités**
   - Controller = HTTP uniquement
   - Service = Orchestration Use Case
   - Domain = Logique métier pure
   - Repositories = Persistance
   - Adapters = Détails techniques

### 🎯 Règles respectées

- **Règle de dépendance** : Infrastructure → Application → Domain
- **Inversion de dépendance** : Domain définit les interfaces, Infra les implémente
- **Principe ouvert/fermé** : Ajout d'adapters sans modifier le domaine
- **Single Responsibility** : Chaque couche a UNE responsabilité claire

---

## Résumé du flux Signup

| Étape | Couche | Responsabilité |
|-------|--------|----------------|
| 1 | Controller | Validation HTTP, désérialisation JSON |
| 2 | Service | Orchestration du Use Case |
| 3 | Domain | Création entités, règles métier |
| 4 | Repositories | Persistance en base MySQL |
| 5 | Adapters | Envoi OTP, vérification KYC, audit |
| 6 | Controller | Sérialisation réponse JSON, HTTP 200 |

**État final après UC-01 :**
- ✅ Client créé (statut : Pending)
- ✅ DossierKYC ouvert (statut : Pending)
- ✅ OTP envoyé (code affiché en console)
- ✅ Compte créé (solde : 0.00)
- ✅ Portefeuille créé (USD, solde : 0.00)
- ✅ Politique MFA activée (type : SMS)
- ✅ Logs d'audit enregistrés

**Prochaines étapes :**
1. Client reçoit le code OTP (email/SMS)
2. Client appelle `POST /api/v1/signup/verify-otp`
3. Si code valide → Statut devient `Active`
4. Client peut alors s'authentifier (`POST /api/v1/login`)

---

## Pour aller plus loin

### Tests du Use Case

```bash
# Tester l'inscription
curl -X POST http://localhost:5000/api/v1/signup \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "phone": "+15145551234",
    "fullName": "John Doe",
    "password": "SecurePass123!",
    "birthDate": "1990-01-15"
  }'

# Réponse attendue (200 OK)
{
  "clientId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "accountId": "7f1a4b2e-3c5d-4e8f-9a1b-2c3d4e5f6a7b",
  "status": "Pending"
}

# Le code OTP s'affiche en console :
# [OTP] canal=Email dest=test@example.com code=123456 client=...
```

### Documentation connexe

- [Architecture Hexagonale complète](/docs/4+1/architecture-hexagonale.puml)
- [Vue Logique (domaine)](/docs/4+1/logique.puml)
- [Vue Processus (runtime)](/docs/4+1/processus.puml)
- [Scénario UC-01](/docs/4+1/scénarios/UC-01%20Inscription%20(activation%20par%20OTP%20+%20KYC).puml)
- [ADR-001 Architecture Hexagonale](/docs/adr/ADR-001-Architecture-hexagonale%20(1).md)

---

**Date de création** : 27 octobre 2025  
**Auteur** : Équipe BrokerX  
**Version** : 1.0 (Phase 1 - État initial avant Phase 2)
