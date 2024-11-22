namespace Forge.Cli;

public static class Runtime
{
    public static bool Debug => Environment.GetEnvironmentVariable("FORGE_DEBUG") != null;
}

public class Wut
{
    
}