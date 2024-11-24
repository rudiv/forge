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

AnsiConsole.WriteLine();
AnsiConsole.MarkupLine("[dim]:fire_extinguisher: Shutting down...[/]");

// Give a little time for processes to stop
await Task.Delay(750);
var openProcesses = DotnetWrapper.Instances.Where(dnw => dnw.InnerTask?.Task.IsCompleted == false).ToList();
if (openProcesses.Count > 0)
{
    AnsiConsole.MarkupLine("[dim]Still have {0} running instances, stopping...[/]", openProcesses.Count);
    foreach (var dnw in openProcesses)
    {
        await dnw.StopAsync();
    }
}
return 0;