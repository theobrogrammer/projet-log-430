namespace ProjetLog430.Domain.Ports.Outbound;

/// <summary>
/// Wrapper abstrait pour IHubContext de SignalR.
/// Architecture Hexagonale : permet d'éviter la dépendance directe à SignalR dans les adapters
/// </summary>
public interface ISignalRHubContextWrapper
{
    /// <summary>
    /// Envoie un message à tous les clients d'un groupe spécifique
    /// </summary>
    Task SendToGroupAsync(string groupName, string method, object data, CancellationToken ct = default);
}
