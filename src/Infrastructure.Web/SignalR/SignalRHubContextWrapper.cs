using Microsoft.AspNetCore.SignalR;
using ProjetLog430.Domain.Ports.Outbound;
using ProjetLog430.Infrastructure.Web.Hubs;

namespace ProjetLog430.Infrastructure.Web.SignalR;

/// <summary>
/// Implémentation concrète du wrapper SignalR.
/// Architecture Hexagonale : 
/// - Implémente ISignalRHubContextWrapper (Port Outbound)
/// - Encapsule IHubContext<MarketDataHub> de SignalR
/// </summary>
public sealed class SignalRHubContextWrapper : ISignalRHubContextWrapper
{
    private readonly IHubContext<MarketDataHub> _hubContext;

    public SignalRHubContextWrapper(IHubContext<MarketDataHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendToGroupAsync(string groupName, string method, object data, CancellationToken ct = default)
    {
        await _hubContext.Clients
            .Group(groupName)
            .SendAsync(method, data, ct);
    }
}
