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
        // Client configuration
        b.Entity<Client>(eb =>
        {
            eb.HasKey(x => x.ClientId);
            eb.Property(x => x.Email).IsRequired().HasMaxLength(254);
            eb.Property(x => x.NomComplet).IsRequired().HasMaxLength(100);
            eb.Property(x => x.Telephone).HasMaxLength(30);
            eb.Property(x => x.Statut).HasConversion<string>().HasMaxLength(16);
            eb.HasIndex(x => x.Email).IsUnique();
        });
    }
}