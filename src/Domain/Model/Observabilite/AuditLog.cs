// src/Domain/Model/Observabilite/AuditLog.cs
namespace ProjetLog430.Domain.Model.Observabilite;

public sealed class AuditLog
{
    public Guid AuditId { get; }
    /// <summary>Type fonctionnel de l'événement (ex.: "CLIENT_SIGNUP", "LOGIN", "DEPOSIT_SETTLED").</summary>
    public string EventType { get; }
    /// <summary>Qui a déclenché l'événement (ex.: userId/email, "system", "webhook").</summary>
    public string Actor { get; }
    /// <summary>Compte éventuellement concerné par l'événement.</summary>
    public Guid? AccountId { get; }
    /// <summary>Payload libre en JSON (données utiles de traçabilité).</summary>
    public string PayloadJson { get; }
    /// <summary>Date de création (UTC).</summary>
    public DateTimeOffset CreatedAt { get; }

    private AuditLog(Guid auditId, string eventType, string actor, Guid? accountId, string payloadJson)
    {
        AuditId     = auditId;
        EventType   = RequireNonEmpty(eventType, nameof(eventType));
        Actor       = RequireNonEmpty(actor, nameof(actor));
        AccountId   = accountId;
        PayloadJson = payloadJson ?? "{}";
        CreatedAt   = DateTimeOffset.UtcNow;
    }

    /// <summary>Fabrique générique : fournir un payload déjà sérialisé en JSON.</summary>
    public static AuditLog Ecrire(string eventType, string actor, Guid? accountId = null, string payloadJson = "{}")
        => new(Guid.NewGuid(), eventType, actor, accountId, payloadJson);

    /// <summary>
    /// Variante pratique : sérialise automatiquement l'objet 'payload' en JSON.
    /// NB: si tu veux un domaine 0-dépendance JSON, fais la sérialisation dans Application
    /// et appelle l'autre factory.
    /// </summary>
    public static AuditLog Ecrire<TPayload>(string eventType, string actor, TPayload payload, Guid? accountId = null)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        return new AuditLog(Guid.NewGuid(), eventType, actor, accountId, json);
    }

    private static string RequireNonEmpty(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{name} requis", name);
        return value.Trim();
    }
}
