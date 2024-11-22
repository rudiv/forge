// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using System.Runtime.InteropServices;
using Forge.Cli;
using Forge.Cli.Commands;
using Forge.Cli.Dcp;
using Forge.Cli.Dcp.ExecutionModel;
using Forge.Cli.Infra;
using Forge.Cli.Infra.Spectre;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

var registrations = new ServiceCollection()
    .AddLogging(b =>
    {
        b.SetMinimumLevel(Runtime.Debug ? LogLevel.Trace : LogLevel.Warning);
        b.AddSpectreLogger();
    });
registrations.AddSingleton<AppHostResolution>();
registrations.AddSingleton<DcpSessionWebHost>();
registrations.AddSingleton<NotificationStreamHandler>();
registrations.AddSingleton<ManagedSessionRegistry>();
registrations.AddSingleton<Endpoints>();
var registrar = new TypeRegistrar(registrations);

AnsiConsole.MarkupLine(@"[red]   (\_/)
  ( >_<)  [/][grey]o[/]     [purple][b]forge[/] for .NET Aspire (MVW)[/]
[red] <|  | )--[/][grey]|[/]     [dim]Issues / PRs welcome at github.com/rudiv/forge[/]
[red]  |___|[/]
");

var app = new CommandApp<DefaultNoOpCommand>(registrar);
app.Configure(o =>
{
    o.UseAssemblyInformationalVersion();
    o.SetApplicationName("forge");
    o.AddWatchCommand();
    o.Settings.PropagateExceptions = true;
});
await app.RunAsync(args);

if (DotnetWrapper.Instances.Count > 0)
{
    AnsiConsole.MarkupLine("[red bold]Still have {0} running instances, stopping...[/]", DotnetWrapper.Instances.Count);
    AnsiConsole.MarkupLine("This may take a while...");
    foreach(var dnw in DotnetWrapper.Instances.ToList())
    {
        await dnw.StopAsync();
    }

    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        AnsiConsole.MarkupLine("Done. Hold on, checking DCP in a few seconds...");
        await Task.Delay(5_000);
        Process.Start("/bin/sh", "-c -- \"ps -A | grep dcp\"");
        AnsiConsole.MarkupLine("If any dcp processes are still there, you should kill them.");
    }
    else
    {
        AnsiConsole.MarkupLine("Done.");
    }
}