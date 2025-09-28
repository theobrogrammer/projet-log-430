using Microsoft.EntityFrameworkCore;
using ProjetLog430.Domain.Model.Identite;
using ProjetLog430.Domain.Model.PortefeuilleReglement;

namespace ProjetLog430.Infrastructure.Persistence;

public sealed class BrokerXDbContext : DbContext
{
    public BrokerXDbContext(DbContextOptions<BrokerXDbContext> options) : base(options) {}

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Compte> Accounts => Set<Compte>();
    public DbSet<Portefeuille> Portfolios => Set<Portefeuille>();
    public DbSet<TransactionPaiement> PayTxs => Set<TransactionPaiement>();
    public DbSet<EcritureLedger> LedgerEntries => Set<EcritureLedger>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // === Client ===
        b.Entity<Client>(eb =>
        {
            eb.HasKey(x => x.ClientId);
            eb.Property(x => x.Email).IsRequired().HasMaxLength(254);
            eb.Property(x => x.NomComplet).IsRequired().HasMaxLength(100);
            eb.Property(x => x.Telephone).HasMaxLength(30);
            eb.Property(x => x.Statut).HasConversion<string>().HasMaxLength(16);
            eb.Property(x => x.CreatedAt);
            eb.Property(x => x.UpdatedAt);
            eb.HasIndex(x => x.Email).IsUnique();

            // On ignore les collections/objets non nécessaires en P1
            // (si tes entités les exposent)
            // eb.Ignore(x => x.Comptes);
            // eb.Ignore(x => x.ContactOtps);
            // eb.Ignore(x => x.Kyc);
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
    }
}
