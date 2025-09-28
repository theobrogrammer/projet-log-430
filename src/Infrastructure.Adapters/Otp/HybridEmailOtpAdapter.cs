// src/Infrastructure.Adapters/Otp/HybridEmailOtpAdapter.cs
using System.Text.Json;
using System.Net;
using System.Net.Mail;
using ProjetLog430.Domain.Model.Identite;
using ProjetLog430.Domain.Ports.Outbound;
using ProjetLog430.Domain.Model.Observabilite;

namespace ProjetLog430.Infrastructure.Adapters.Otp;

/// <summary>
/// Adapter hybride qui essaie d'envoyer un email réel via SMTP, 
/// puis fallback sur console pour le développement.
/// Respecte le principe de séparation des responsabilités.
/// </summary>
public sealed class HybridEmailOtpAdapter : IOtpPort
{
    private readonly IAuditPort _audit;
    private readonly SmtpConfig? _smtpConfig;

    public HybridEmailOtpAdapter(IAuditPort audit, SmtpConfig? smtpConfig = null)
    {
        _audit = audit;
        _smtpConfig = smtpConfig;
    }

    public async Task SendContactOtpAsync(Guid clientId, Guid otpId, CanalOTP canal, string destination, string code, CancellationToken ct = default)
    {
        var emailSent = false;
        
        // 1) Essayer l'envoi d'email réel si configuré
        if (canal == CanalOTP.Email && _smtpConfig != null && _smtpConfig.IsValid())
        {
            try
            {
                await SendRealEmailAsync(destination, code, ct);
                emailSent = true;
                
                await _audit.WriteAsync(AuditLog.Ecrire("OTP_EMAIL_SENT", "system",
                    payload: new { clientId, otpId, destination = MaskEmail(destination) }), ct);
            }
            catch (Exception ex)
            {
                await _audit.WriteAsync(AuditLog.Ecrire("OTP_EMAIL_FAILED", "system",
                    payload: new { clientId, otpId, error = ex.Message }), ct);
            }
        }

        // 2) Fallback développement : console + audit
        if (!emailSent)
        {
            Console.WriteLine($"[OTP] canal={canal} dest={destination} code={code} client={clientId} otp={otpId}");
            var payload = JsonSerializer.Serialize(new { clientId, otpId, canal = canal.ToString(), destination, code });
            await _audit.WriteAsync(AuditLog.Ecrire("OTP_CONSOLE_FALLBACK", "system", payloadJson: payload), ct);
        }
    }

    private async Task SendRealEmailAsync(string toEmail, string otpCode, CancellationToken ct)
    {
        if (_smtpConfig == null) throw new InvalidOperationException("Configuration SMTP manquante");

        using var client = new SmtpClient(_smtpConfig.Host, _smtpConfig.Port)
        {
            Credentials = new NetworkCredential(_smtpConfig.User, _smtpConfig.Password),
            EnableSsl = true
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress(_smtpConfig.FromEmail, _smtpConfig.FromName),
            Subject = "🔐 Code de vérification BrokerX",
            Body = CreateEmailTemplate(otpCode),
            IsBodyHtml = true
        };
        
        mailMessage.To.Add(toEmail);
        await client.SendMailAsync(mailMessage, ct);
    }

    private static string CreateEmailTemplate(string code) => $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:20px'>
    <div style='background:linear-gradient(135deg,#667eea 0%,#764ba2 100%);color:white;padding:30px;text-align:center;border-radius:10px 10px 0 0'>
        <h1 style='margin:0;font-size:28px'>🔐 BrokerX</h1>
        <p style='margin:10px 0 0;font-size:16px'>Code de vérification</p>
    </div>
    <div style='background:#f8f9fa;padding:40px;border-radius:0 0 10px 10px;border:1px solid #dee2e6'>
        <p style='font-size:16px;color:#333;margin-bottom:30px'>
            Votre code de vérification pour activer votre compte :
        </p>
        <div style='background:white;border:2px solid #0d6efd;border-radius:10px;padding:20px;text-align:center;margin:30px 0'>
            <span style='font-size:36px;font-weight:bold;color:#0d6efd;letter-spacing:8px'>{code}</span>
        </div>
        <p style='font-size:14px;color:#666;margin-top:30px'>
            ⏱️ Ce code expire dans <strong>10 minutes</strong><br>
            🔒 Ne partagez jamais ce code<br>
            📧 Si vous n'avez pas demandé ce code, ignorez cet email
        </p>
    </div>
</div>";

    private static string MaskEmail(string email)
    {
        var parts = email.Split('@');
        if (parts.Length != 2) return email;
        
        var local = parts[0];
        if (local.Length <= 3) return email;
        
        return $"{local[0]}{new string('*', local.Length - 2)}{local[^1]}@{parts[1]}";
    }
}

/// <summary>Configuration SMTP simple sans dépendance externe</summary>
public sealed record SmtpConfig(
    string Host = "smtp.gmail.com",
    int Port = 587,
    string User = "",
    string Password = "",
    string FromEmail = "noreply@brokerx.com",
    string FromName = "BrokerX Security"
)
{
    public bool IsValid() => !string.IsNullOrEmpty(User) && !string.IsNullOrEmpty(Password);
}