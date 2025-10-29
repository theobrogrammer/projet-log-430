// src/Infrastructure.Adapters/Payment/PaymentAdapterSim.cs
using System.Net.Http.Json;
using ProjetLog430.Domain.Ports.Outbound;

namespace ProjetLog430.Infrastructure.Adapters.Payment;

public sealed class PaymentAdapterSim : IPaymentPort
{
    private readonly HttpClient _http;
    private readonly string _webhookUrl; // ex.: http://localhost:8080/api/v1/payment/webhook
    private readonly Random _random = new();

    public PaymentAdapterSim(HttpClient http, string webhookUrl)
    {
        _http = http;
        _webhookUrl = webhookUrl.TrimEnd('/');
    }

    public Task RequestDepositAsync(Guid paymentTxId, Guid accountId, decimal amount, string currency, CancellationToken ct = default)
    {
        // Simulation plus réaliste du PSP avec plusieurs scénarios
        return Task.Run(async () =>
        {
            try
            {
                // Délai variable entre 500ms et 3s pour simuler processing
                var delay = _random.Next(500, 3000);
                await Task.Delay(delay, ct);

                // 90% de succès, 10% d'échec pour simulation réaliste
                var success = _random.NextDouble() > 0.1;
                
                // Les petits montants ont plus de chance de réussir
                if (amount < 100m) success = _random.NextDouble() > 0.02; // 98% succès
                if (amount > 50000m) success = _random.NextDouble() > 0.2; // 80% succès

                var status = success ? "Settled" : "Failed";
                var payload = new { paymentTxId, status, signature = "sim", amount, currency };
                
                var resp = await _http.PostAsJsonAsync($"{_webhookUrl}/webhook", payload, ct);
                resp.EnsureSuccessStatusCode();
            }
            catch { /* swallow pour la démo */ }
        }, ct);
    }
}
