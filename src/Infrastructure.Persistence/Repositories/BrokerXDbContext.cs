using Microsoft.EntityFrameworkCore;
using ProjetLog430.Domain.Model.Identite;
using ProjetLog430.Domain.Model.PortefeuilleReglement;
using ProjetLog430.Domain.Model.Securite;

namespace ProjetLog430.Infrastructure.Persistence;

public sealed class BrokerXDbContext : DbContext
{
    public BrokerXDbContext(DbContextOptions<BrokerXDbContext> options) : base(options) {}

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Compte> Accounts => Set<Compte>();
    public DbSet<Portefeuille> Portfolios => Set<Portefeuille>();
    public DbSet<TransactionPaiement> PayTxs => Set<TransactionPaiement>();
    public DbSet<EcritureLedger> LedgerEntries => Set<EcritureLedger>();
    public DbSet<DossierKYC> KycDossiers => Set<DossierKYC>();
    public DbSet<VerifContactOTP> ContactOtps => Set<VerifContactOTP>();
    
    // Tables MFA et sécurité
    public DbSet<PolitiqueMFA> MfaPolicies => Set<PolitiqueMFA>();
    public DbSet<DefiMFA> MfaChallenges => Set<DefiMFA>();
    public DbSet<Session> Sessions => Set<Session>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // === Client ===
        b.Entity<Client>(eb =>
        {
            eb.HasKey(x => x.ClientId);
            eb.Property(x => x.Email).IsRequired().HasMaxLength(254);
            eb.Property(x => x.NomComplet).IsRequired().HasMaxLength(100);
            eb.Property(x => x.Telephone).HasMaxLength(30);
            eb.Property(x => x.PasswordHash).IsRequired().HasMaxLength(100);
            eb.Property(x => x.Statut).HasConversion<string>().HasMaxLength(16);
            eb.Property(x => x.CreatedAt);
            eb.Property(x => x.UpdatedAt);
            eb.HasIndex(x => x.Email).IsUnique();

            // Relations configurées explicitement
            eb.HasOne(x => x.Kyc)
              .WithOne()
              .HasForeignKey<DossierKYC>(k => k.ClientId)
              .OnDelete(DeleteBehavior.Cascade);
              
            eb.HasMany(x => x.ContactOtps)
              .WithOne()
              .HasForeignKey(o => o.ClientId)
              .OnDelete(DeleteBehavior.Cascade);
              
            eb.HasMany(x => x.Comptes)
              .WithOne()
              .HasForeignKey(c => c.ClientId)
              .OnDelete(DeleteBehavior.Cascade);
        });

        // === Compte ===
        b.Entity<Compte>(eb =>
        {
            eb.HasKey(x => x.AccountId);
            eb.Property(x => x.ClientId).IsRequired();
            eb.Property(x => x.AccountNo).IsRequired().HasMaxLength(40);
            eb.Property(x => x.Statut).HasConversion<string>().HasMaxLength(16);
            eb.Property(x => x.CreatedAt);
            eb.Property(x => x.UpdatedAt);
            eb.HasIndex(x => x.AccountNo).IsUnique();

            // FK (optionnelle si tu veux la contrainte DB)
            // eb.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId);
        });

        // === Portefeuille (1:1 avec Compte) ===
        b.Entity<Portefeuille>(eb =>
        {
            eb.HasKey(x => x.Id);
            eb.Property(x => x.AccountId).IsRequired();
            eb.Property(x => x.Devise).IsRequired().HasMaxLength(3);
            eb.Property(x => x.SoldeMonnaie).HasColumnType("decimal(18,2)");
            eb.Property(x => x.UpdatedAt);
            eb.HasIndex(x => x.AccountId).IsUnique();
        });

        // === TransactionPaiement ===
        b.Entity<TransactionPaiement>(eb =>
        {
            eb.HasKey(x => x.PaymentTxId);
            eb.Property(x => x.AccountId).IsRequired();
            eb.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            eb.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            eb.Property(x => x.Statut).HasConversion<string>().HasMaxLength(16);
            eb.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(80);
            eb.Property(x => x.CreatedAt);
            eb.Property(x => x.SettledAt);
            eb.Property(x => x.FailureReason).HasMaxLength(120);
            eb.HasIndex(x => x.IdempotencyKey).IsUnique();
            // FK possible :
            // eb.HasOne<Compte>().WithMany().HasForeignKey(x => x.AccountId);
        });

        // === EcritureLedger ===
        b.Entity<EcritureLedger>(eb =>
        {
            eb.HasKey(x => x.LedgerEntryId);
            eb.Property(x => x.AccountId).IsRequired();
            eb.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            eb.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            eb.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            eb.Property(x => x.RefType).HasConversion<string>().HasMaxLength(16);
            eb.Property(x => x.RefId).IsRequired();
            eb.Property(x => x.CreatedAt);
            eb.HasIndex(x => new { x.AccountId, x.RefType, x.RefId, x.Kind });
        });

        // === DossierKYC ===
        b.Entity<DossierKYC>(eb =>
        {
            eb.HasKey(x => x.KycId);
            eb.Property(x => x.ClientId).IsRequired();
            eb.Property(x => x.Niveau).IsRequired().HasMaxLength(20);
            eb.Property(x => x.Statut).HasConversion<string>().HasMaxLength(16);
            eb.Property(x => x.UpdatedAt);
        });

        // === VerifContactOTP ===
        b.Entity<VerifContactOTP>(eb =>
        {
            eb.HasKey(x => x.OtpId);
            eb.Property(x => x.ClientId).IsRequired();
            eb.Property(x => x.Canal).HasConversion<string>().HasMaxLength(10);
            eb.Property(x => x.Statut).HasConversion<string>().HasMaxLength(16);
            eb.Property(x => x.ExpiresAt);
            eb.Property(x => x.CreatedAt);
        });

        // === PolitiqueMFA ===
        b.Entity<PolitiqueMFA>(eb =>
        {
            eb.HasKey(x => x.MfaId);
            eb.Property(x => x.ClientId).IsRequired();
            eb.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            eb.Property(x => x.EstActive).IsRequired();
            eb.Property(x => x.CreatedAt).IsRequired();
            eb.Property(x => x.UpdatedAt).IsRequired();
            eb.HasIndex(x => x.ClientId);
        });

        // === DefiMFA ===
        b.Entity<DefiMFA>(eb =>
        {
            eb.HasKey(x => x.ChallengeId);
            eb.Property(x => x.ClientId).IsRequired();
            eb.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            eb.Property(x => x.Statut).HasConversion<string>().HasMaxLength(20);
            eb.Property(x => x.CreatedAt).IsRequired();
            eb.Property(x => x.ExpiresAt).IsRequired();
            eb.Property(x => x.CompletedAt);
            eb.HasIndex(x => x.ClientId);
            eb.HasIndex(x => x.ExpiresAt);
        });

        // === Session ===
        b.Entity<Session>(eb =>
        {
            eb.HasKey(x => x.SessionId);
            eb.Property(x => x.ClientId).IsRequired();
            eb.Property(x => x.TokenType).HasConversion<string>().HasMaxLength(20);
            eb.Property(x => x.Token).IsRequired().HasMaxLength(500);
            eb.Property(x => x.IssuedAt).IsRequired();
            eb.Property(x => x.ExpiresAt).IsRequired();
            eb.Property(x => x.Ip).HasMaxLength(45); // IPv6
            eb.Property(x => x.Device).HasMaxLength(255);
            eb.Property(x => x.Revoked).IsRequired();
            eb.HasIndex(x => x.ClientId);
            eb.HasIndex(x => x.Token).IsUnique();
            eb.HasIndex(x => x.ExpiresAt);
        });
    }
}
