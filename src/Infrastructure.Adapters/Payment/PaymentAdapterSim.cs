// src/Infrastructure.Adapters/Payment/PaymentAdapterSim.cs
using System.Net.Http.Json;
using ProjetLog430.Domain.Ports.Outbound;

namespace ProjetLog430.Infrastructure.Adapters.Payment;

public sealed class PaymentAdapterSim : IPaymentPort
{
    private readonly HttpClient _http;
    private readonly string _webhookUrl; // ex.: http://localhost:8080/api/v1/payment/webhook

    public PaymentAdapterSim(HttpClient http, string webhookUrl)
    {
        _http = http;
        _webhookUrl = webhookUrl.TrimEnd('/');
    }

    public async Task RequestDepositAsync(Guid paymentTxId, Guid accountId, decimal amount, string currency, CancellationToken ct = default)
    {
        // Démo: on simule un "provider" qui règle la transaction 1s plus tard
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1000, ct);
                var payload = new { paymentTxId, status = "Settled", signature = "sim" };
                var resp = await _http.PostAsJsonAsync($"{_webhookUrl}/webhook", payload, ct);
                resp.EnsureSuccessStatusCode();
            }
            catch { /* swallow pour la démo */ }
        }, ct);
    }
}
