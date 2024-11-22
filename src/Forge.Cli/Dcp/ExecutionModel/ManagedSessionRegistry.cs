using System.Collections.Concurrent;

namespace Forge.Cli.Dcp.ExecutionModel;

public class ManagedSessionRegistry(NotificationStreamHandler notificationStreamHandler)
{
    public ConcurrentDictionary<Guid, DotnetWrapper> Sessions { get; set; } = new();

    public async Task<Guid> NewSession(DotnetWrapper wrapper)
    {
        var sessId = Guid.NewGuid();
        Sessions.TryAdd(sessId, wrapper);
        wrapper.SetStreamHandler(sessId, notificationStreamHandler);
        await wrapper.StartAsync();

        return sessId;
    }
}