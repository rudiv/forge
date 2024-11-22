using System.Collections.Concurrent;

namespace Forge.Cli.Dcp;

public class RunSessions
{
    public ConcurrentDictionary<Guid, RunSession> Sessions { get; set; } = [];
}

public class RunSession
{
    
}