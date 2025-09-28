// src/Infrastructure.Adapters/Otp/RealEmailOtpAdapter.cs
using System.Net;
using System.Net.Mail;
using ProjetLog430.Domain.Model.Identite;
using ProjetLog430.Domain.Ports.Outbound;
using ProjetLog430.Domain.Model.Observabilite;

namespace ProjetLog430.Infrastructure.Adapters.Otp;

public sealed class RealEmailOtpAdapter : IOtpPort
{
    private readonly IAuditPort _audit;
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _smtpUser;
    private readonly string _smtpPass;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public RealEmailOtpAdapter(
        IAuditPort audit,
        string smtpHost = "smtp.gmail.com",
        int smtpPort = 587,
        string smtpUser = "",
        string smtpPass = "",
        string fromEmail = "noreply@brokerx.com",
        string fromName = "BrokerX Security")
    {
        _audit = audit;
        _smtpHost = smtpHost;
        _smtpPort = smtpPort;
        _smtpUser = smtpUser;
        _smtpPass = smtpPass;
        _fromEmail = fromEmail;
        _fromName = fromName;
    }

    public async Task SendContactOtpAsync(Guid clientId, Guid otpId, CanalOTP canal, string destination, string code, CancellationToken ct = default)
    {
        try
        {
            if (canal == CanalOTP.Email)
            {
                await SendEmailAsync(destination, code, ct);
                
                // Log successful send
                await _audit.WriteAsync(
                    AuditLog.Ecrire("OTP_EMAIL_SENT", "system", 
                        payload: new { clientId, otpId, destination = MaskEmail(destination) }), ct);
            }
            else
            {
                // SMS non implémenté - fallback console
                Console.WriteLine($"[OTP-SMS] dest={destination} code={code} (SMS non implémenté)");
                await _audit.WriteAsync(
                    AuditLog.Ecrire("OTP_SMS_FALLBACK", "system", 
                        payload: new { clientId, otpId, destination }), ct);
            }
        }
        catch (Exception ex)
        {
            // Fallback en cas d'erreur - affichage console pour développement
            Console.WriteLine($"[OTP-ERROR] Erreur envoi email: {ex.Message}");
            Console.WriteLine($"[OTP-FALLBACK] dest={destination} code={code}");
            
            await _audit.WriteAsync(
                AuditLog.Ecrire("OTP_EMAIL_ERROR", "system", 
                    payload: new { clientId, otpId, error = ex.Message, fallbackCode = code }), ct);
        }
    }

    private async Task SendEmailAsync(string toEmail, string otpCode, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_smtpUser) || string.IsNullOrEmpty(_smtpPass))
        {
            throw new InvalidOperationException("Configuration SMTP manquante");
        }

        using var client = new SmtpClient(_smtpHost, _smtpPort)
        {
            Credentials = new NetworkCredential(_smtpUser, _smtpPass),
            EnableSsl = true
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress(_fromEmail, _fromName),
            Subject = "🔐 Votre code de vérification BrokerX",
            Body = CreateEmailBody(otpCode),
            IsBodyHtml = true
        };
        
        mailMessage.To.Add(toEmail);

        await client.SendMailAsync(mailMessage, ct);
    }

    private static string CreateEmailBody(string otpCode) => $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <title>Code de vérification BrokerX</title>
</head>
<body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0;'>
        <h1 style='margin: 0; font-size: 28px;'>🔐 BrokerX</h1>
        <p style='margin: 10px 0 0 0; font-size: 16px;'>Code de vérification</p>
    </div>
    
    <div style='background: #f8f9fa; padding: 40px; border-radius: 0 0 10px 10px; border: 1px solid #dee2e6;'>
        <p style='font-size: 16px; color: #333; margin-bottom: 30px;'>
            Votre code de vérification pour activer votre compte BrokerX :
        </p>
        
        <div style='background: white; border: 2px solid #0d6efd; border-radius: 10px; padding: 20px; text-align: center; margin: 30px 0;'>
            <span style='font-size: 36px; font-weight: bold; color: #0d6efd; letter-spacing: 8px;'>{otpCode}</span>
        </div>
        
        <p style='font-size: 14px; color: #666; margin-top: 30px;'>
            ⏱️ Ce code expire dans <strong>10 minutes</strong><br>
            🔒 Ne partagez jamais ce code avec personne<br>
            📧 Si vous n'avez pas demandé ce code, ignorez cet email
        </p>
        
        <hr style='border: none; border-top: 1px solid #dee2e6; margin: 30px 0;'>
        
        <p style='font-size: 12px; color: #999; text-align: center;'>
            © 2025 BrokerX - Plateforme de trading sécurisée
        </p>
    </div>
</body>
</html>";

    private static string MaskEmail(string email)
    {
        var parts = email.Split('@');
        if (parts.Length != 2) return email;
        
        var local = parts[0];
        var domain = parts[1];
        
        if (local.Length <= 3) return email;
        
        return $"{local[0]}{new string('*', local.Length - 2)}{local[^1]}@{domain}";
    }
}