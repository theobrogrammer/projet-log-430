// src/Infrastructure.Adapters/Ledger/FileLedgerAdapter.cs
using System.Text.Json;
using ProjetLog430.Domain.Model.PortefeuilleReglement;
using ProjetLog430.Domain.Ports.Outbound;

namespace ProjetLog430.Infrastructure.Adapters.Ledger;

public sealed class FileLedgerAdapter : ILedgerPort
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = false };

    public FileLedgerAdapter(string filePath = "logs/ledger.jsonl")
    {
        _filePath = filePath;
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
    }

    public Task AddAsync(EcritureLedger entry, CancellationToken ct = default)
        => AppendAsync(entry, ct);

    public Task AddRangeAsync(IEnumerable<EcritureLedger> entries, CancellationToken ct = default)
        => Task.WhenAll(entries.Select(e => AppendAsync(e, ct)));

    private async Task AppendAsync(EcritureLedger e, CancellationToken ct)
    {
        var line = JsonSerializer.Serialize(new {
            e.LedgerEntryId, e.AccountId, e.Amount, e.Currency, Kind = e.Kind.ToString(),
            RefType = e.RefType.ToString(), e.RefId, e.CreatedAt
        }, _json);
        await File.AppendAllTextAsync(_filePath, line + Environment.NewLine, ct);
    }
}
