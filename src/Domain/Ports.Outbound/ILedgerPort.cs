namespace ProjetLog430.Domain.Ports.Outbound;

using ProjetLog430.Domain.Model.PortefeuilleReglement;

public interface ILedgerPort
{
    /// <summary>Ajoute une écriture comptable (Ledger) atomiquement avec la transaction métier.</summary>
    Task AddAsync(
        EcritureLedger entry,
        CancellationToken ct = default);

    /// <summary>Ajout batch (optionnel).</summary>
    Task AddRangeAsync(
        IEnumerable<EcritureLedger> entries,
        CancellationToken ct = default);
}
