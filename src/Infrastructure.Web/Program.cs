using Microsoft.EntityFrameworkCore;
using ProjetLog430.Infrastructure.Persistence;
using ProjetLog430.Domain.Ports.Outbound;
using ProjetLog430.Domain.Ports.Inbound;
using ProjetLog430.Application.Services;
using ProjetLog430.Infrastructure.Adapters.Repositories;
using ProjetLog430.Infrastructure.Adapters.Audit;
using ProjetLog430.Infrastructure.Adapters.Ledger;
using ProjetLog430.Infrastructure.Adapters.Otp;
using ProjetLog430.Infrastructure.Adapters.Payment;
using ProjetLog430.Infrastructure.Adapters.Session;
using ProjetLog430.Infrastructure.Adapters.Kyc;
using Microsoft.AspNetCore.Rewrite;

// ... autres using (use cases, adapters OTP/Payment/Ledger/Audit etc.)

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Permettre la désérialisation des enums par leur nom (string)
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// EF Core + MySQL (Pomelo) - avec support pour les tests
var cs = builder.Configuration.GetConnectionString("BrokerX")
         ?? "Server=mysql;Port=3306;Database=brokerx;User Id=brokerx;Password=brokerx;TreatTinyAsBoolean=false";

// Configuration de la base de données - utilisons toujours InMemoryDatabase pour les tests MFA
builder.Services.AddDbContext<BrokerXDbContext>(opt =>
    opt.UseInMemoryDatabase("TestDatabase"));

// Ports entrants (use cases)
builder.Services.AddScoped<ISignupUseCase, SignupService>();
builder.Services.AddScoped<IAuthUseCase,   AuthService>();
builder.Services.AddScoped<IDepositUseCase, WalletService>();
builder.Services.AddScoped<ISettlementCallbackUseCase, WalletService>();

// Repositories (mêmes noms)
builder.Services.AddScoped<IClientRepository,    InMemoryClientRepository>();
builder.Services.AddScoped<IAccountRepository,   InMemoryAccountRepository>();
builder.Services.AddScoped<IPortfolioRepository, InMemoryPortfolioRepository>();
builder.Services.AddScoped<IPayTxRepository,     InMemoryPayTxRepository>();
builder.Services.AddScoped<IMfaPolicyRepository, InMemoryMfaPolicyRepository>();
builder.Services.AddScoped<IMfaChallengeRepository, InMemoryMfaChallengeRepository>();
builder.Services.AddScoped<ISessionRepository,   InMemorySessionRepository>();

// Adapters sortants (audit/ledger/otp/payment/session)
builder.Services.AddSingleton<IAuditPort>(new StructuredAuditAdapter("logs/audit.jsonl"));
builder.Services.AddSingleton<ILedgerPort>(new FileLedgerAdapter("logs/ledger.jsonl"));
builder.Services.AddScoped<IOtpPort>(serviceProvider => 
{
    var audit = serviceProvider.GetRequiredService<IAuditPort>();
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    
    // Configuration SMTP depuis appsettings.json
    var smtpConfig = new ProjetLog430.Infrastructure.Adapters.Otp.SmtpConfig(
        Host: config["Smtp:Host"] ?? "smtp.gmail.com",
        Port: int.Parse(config["Smtp:Port"] ?? "587"),
        User: config["Smtp:User"] ?? "",
        Password: config["Smtp:Password"] ?? "",
        FromEmail: config["Smtp:FromEmail"] ?? "noreply@brokerx.com",
        FromName: config["Smtp:FromName"] ?? "BrokerX Security"
    );
    
    return new ProjetLog430.Infrastructure.Adapters.Otp.HybridEmailOtpAdapter(audit, smtpConfig);
});
builder.Services.AddSingleton<ISessionPort, JwtSessionAdapter>();
builder.Services.AddSingleton<IKycPort, KycAdapterSim>();
builder.Services.AddHttpClient<PaymentAdapterSim>();
builder.Services.AddSingleton<IPaymentPort>(sp => sp.GetRequiredService<PaymentAdapterSim>());

// Static files (pages)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// URL Rewriting pour pages HTML sans extension
var rewriteOptions = new RewriteOptions()
    .AddRewrite("^signin$", "signin.html", skipRemainingRules: false)
    .AddRewrite("^signup$", "signup.html", skipRemainingRules: false)
    .AddRewrite("^signup-otp$", "signup-otp.html", skipRemainingRules: false)
    .AddRewrite("^test-mfa$", "test-mfa.html", skipRemainingRules: false)
    .AddRewrite("^login$", "signin.html", skipRemainingRules: false);
app.UseRewriter(rewriteOptions);

if (app.Environment.IsDevelopment()) 
{ 
    app.UseSwagger(); 
    app.UseSwaggerUI(); 
}

// Health check endpoint
app.MapGet("/health", () => "Healthy")
   .WithName("HealthCheck")
   .WithOpenApi();

// API routing
app.UseDefaultFiles();
app.UseStaticFiles();

// Configuration des routes par défaut
app.MapFallbackToFile("index.html");

app.MapControllers();

// ===================================================================
// FORCER ENTITY FRAMEWORK À CRÉER LA BASE DE DONNÉES
// ===================================================================
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<BrokerXDbContext>();
    
    try 
    {
        // Créer la base de données si elle n'existe pas
        dbContext.Database.EnsureCreated();
        
        // Log pour confirmer que la DB a été créée/vérifiée
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("✅ Base de données vérifiée/créée avec succès");
        
        // Optionnel: Lister les tables créées pour debug
        var tableNames = dbContext.Model.GetEntityTypes()
            .Select(t => t.GetTableName())
            .Where(name => !string.IsNullOrEmpty(name))
            .ToList();
            
        logger.LogInformation("📋 Tables configurées dans EF: {Tables}", string.Join(", ", tableNames));
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "❌ Erreur lors de la création/vérification de la base de données");
        throw; // Arrêter l'application si la DB ne peut pas être créée
    }
}

app.Run();

// Expose Program class for testing
public partial class Program { }
