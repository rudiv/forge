using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Forge.Cli.Dcp.ExecutionModel;

public class ManagedSessionRegistry(NotificationStreamHandler notificationStreamHandler, ILogger<ManagedSessionRegistry> logger)
{
    public ConcurrentDictionary<Guid, DotnetWrapper> Sessions { get; set; } = new();

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously It may do something funky soon
    public async Task<Guid> NewSession(DotnetWrapper wrapper)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        var sessId = Guid.NewGuid();
        Sessions.TryAdd(sessId, wrapper);
        wrapper.SetStreamHandler(sessId, notificationStreamHandler);
        wrapper.Start();

        return sessId;
    }
    
    public async Task<Guid> StopSession(Guid sessionId)
    {
        if (Sessions.TryGetValue(sessionId, out var wrapper))
        {
            logger.LogInformation("Stopping session {SessionId}", sessionId);
            await wrapper.StopAsync();
            Sessions.TryRemove(sessionId, out _);
        }
        return sessionId;
    }
}