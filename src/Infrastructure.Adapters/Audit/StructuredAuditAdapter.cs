// src/Infrastructure.Adapters/Audit/StructuredAuditAdapter.cs
using System.Text.Json;
using ProjetLog430.Domain.Model.Observabilite;
using ProjetLog430.Domain.Ports.Outbound;

namespace ProjetLog430.Infrastructure.Adapters.Audit;

public sealed class StructuredAuditAdapter : IAuditPort
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerOptions.Default) { WriteIndented = false };

    public StructuredAuditAdapter(string filePath = "logs/audit.jsonl")
    {
        _filePath = filePath;
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
    }

    public async Task WriteAsync(AuditLog log, CancellationToken ct = default)
    {
        var line = JsonSerializer.Serialize(new
        {
            log.AuditId,
            log.EventType,
            log.Actor,
            log.AccountId,
            log.PayloadJson,
            log.CreatedAt
        }, _json);

        await File.AppendAllTextAsync(_filePath, line + Environment.NewLine, ct);
    }
}
