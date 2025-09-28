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

builder.Services.AddControllers();

// EF Core + MySQL (Pomelo)
var cs = builder.Configuration.GetConnectionString("BrokerX")
         ?? "Server=mysql;Port=3306;Database=brokerx;User Id=brokerx;Password=brokerx;TreatTinyAsBoolean=false";
builder.Services.AddDbContext<BrokerXDbContext>(opt =>
    opt.UseMySql(cs, ServerVersion.AutoDetect(cs)));

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
builder.Services.AddSingleton<IOtpPort, EmailSmsOtpAdapter>();
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
app.MapControllers();

app.Run();
