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
using ProjetLog430.Infrastructure.Adapters.Cache;
using Microsoft.AspNetCore.Rewrite;
using Prometheus;
using Serilog;
using Serilog.Formatting.Compact;
using StackExchange.Redis;

// ===================================================================
// SERILOG CONFIGURATION (Phase 2 - Étape 2a: Observabilité - Logs structurés)
// ===================================================================
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithThreadId()
    .Enrich.WithMachineName()
    .Enrich.WithProperty("Application", "BrokerX")
    .Enrich.WithProperty("Environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production")
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Application", Serilog.Events.LogEventLevel.Information)
    .MinimumLevel.Override("Domain", Serilog.Events.LogEventLevel.Information)
    .WriteTo.Console(new CompactJsonFormatter())
    .WriteTo.File(
        new CompactJsonFormatter(),
        "logs/app-.jsonl",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    Log.Information("🚀 BrokerX API démarrage...");

var builder = WebApplication.CreateBuilder(args);

// Utiliser Serilog comme provider de logs
builder.Host.UseSerilog();

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

// ===================================================================
// REDIS CACHE CONFIGURATION (Phase 2 - Étape 2a: Performance)
// ===================================================================
var redisConnectionString = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379,abortConnect=false";
Log.Information("🔧 Configuration Redis: {RedisConnectionString}", redisConnectionString);

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse(redisConnectionString);
    configuration.AbortOnConnectFail = false; // Graceful degradation si Redis down
    Log.Information("🔌 Tentative connexion Redis...");
    var conn = ConnectionMultiplexer.Connect(configuration);
    Log.Information("✅ Redis connecté: {Endpoints}, Status: {IsConnected}", 
        string.Join(", ", conn.GetEndPoints().Select(e => e.ToString())), conn.IsConnected);
    return conn;
});
builder.Services.AddSingleton<ICachePort, RedisCacheAdapter>();

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

// ===================================================================
// PROMETHEUS METRICS (Phase 2 - Étape 2a: Observabilité)
// ===================================================================
// Middleware pour collecter automatiquement les métriques HTTP
app.UseHttpMetrics();

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

// Endpoint Prometheus pour exposer les métriques (Phase 2 - Étape 2a)
app.MapMetrics();

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
    
    // Tester la connexion Redis au démarrage
    try
    {
        var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
        var cache = scope.ServiceProvider.GetRequiredService<ICachePort>();
        Log.Information("✅ Redis et Cache adapter initialisés avec succès");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "❌ Erreur lors de l'initialisation du cache Redis");
        // Ne pas throw - graceful degradation
    }
}

app.Run();

}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ L'application BrokerX a échoué au démarrage");
    throw;
}
finally
{
    Log.Information("🛑 BrokerX API arrêt");
    Log.CloseAndFlush();
}

// Expose Program class for testing
public partial class Program { }
