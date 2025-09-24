// src/Infrastructure.Adapters/Otp/EmailSmsOtpAdapter.cs
using System.Text.Json;
using ProjetLog430.Domain.Model.Identite;
using ProjetLog430.Domain.Ports.Outbound;

namespace ProjetLog430.Infrastructure.Adapters.Otp;

public sealed class EmailSmsOtpAdapter : IOtpPort
{
    private readonly IAuditPort _audit;
    public EmailSmsOtpAdapter(IAuditPort audit) => _audit = audit;

    public async Task SendContactOtpAsync(Guid clientId, Guid otpId, CanalOTP canal, string destination, string code, CancellationToken ct = default)
    {
        // Démo : on trace le “send” via audit + console.
        Console.WriteLine($"[OTP] canal={canal} dest={destination} code={code} client={clientId} otp={otpId}");
        var payload = JsonSerializer.Serialize(new { clientId, otpId, canal = canal.ToString(), destination, code });
        await _audit.WriteAsync(Observabilite.AuditLog.Ecrire("OTP_SENT", "system", accountId: null, payloadJson: payload), ct);
    }
}
