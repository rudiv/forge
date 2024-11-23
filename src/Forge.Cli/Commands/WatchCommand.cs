using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Forge.Cli.Dcp;
using Forge.Cli.Dcp.ExecutionModel;
using Forge.Cli.Infra;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Forge.Cli.Commands;

public class WatchCommand(AppHostResolution appHostResolution, DcpSessionWebHost dcpSessionWebHost, ILogger<WatchCommand> logger) : AsyncCommand<WatchCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, WatchCommandSettings settings)
    {
        if (appHostResolution.AppHostPath == null)
        {
            AnsiConsole.MarkupLine(
                "[red]No AppHost project was found, you need to run forge from within the Solution or AppHost project folder.[/]");
            return 1;
        }
        
        var withoutExt = Path.GetFileNameWithoutExtension(appHostResolution.AppHostPath);
        AnsiConsole.MarkupLine("[green]:fire: Forging {0}.[/]", withoutExt);
        DotnetWrapper wrapper = null!;

        await AnsiConsole.Status()
            .StartAsync(":fire: Firing...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Hamburger);
                await dcpSessionWebHost.StartWebHostAsync(settings.Port);
                var startupTs = new TaskCompletionSource<bool>();
                logger.LogTrace("Started...");
                wrapper = new DotnetWrapper(o =>
                {
                    o.Command = DotnetCommand.Run;
                    o.EnvironmentVariables = new()
                    {
                        { "DEBUG_SESSION_PORT", settings.Port.ToString() },
                        { "DEBUG_SESSION_TOKEN", Guid.NewGuid().ToString() }
                    };
                    o.ProjectPath = appHostResolution.AppHostPath;
                });
                await wrapper.StartAsync();
                wrapper.InnerProcess!.OutputDataReceived += (sender, args) =>
                {
                    if (args.Data == null) return;
                    if (args.Data.Contains("Login to the dashboard at"))
                    {
                        AnsiConsole.MarkupLine("[bold]AppHost started: " + args.Data.Trim().EscapeMarkup() + "[/]");
                        startupTs.TrySetResult(true);
                    }
                    if (settings.ShowBuildOutput)
                    {
                        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[grey]{args.Data!}[/]");
                    }
                };
                wrapper.InnerProcess!.ErrorDataReceived += (sender, args) =>
                {
                    if (settings.ShowBuildOutput) {
                        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]{args.Data!}[/]");
                    }
                };
                wrapper.InnerProcess.Exited += (sender, args) =>
                {
                    logger.LogTrace("Exiting?");
                    startupTs.TrySetResult(false); // Stop the endless loop
                    _ = dcpSessionWebHost.StopWebHostAsync();
                };
                var started = await startupTs.Task;
                if (started)
                {
                    await Task.Delay(5000);
                    AnsiConsole.MarkupLine("[green]:fire: forge fired. Enjoy hacking on Aspire with a working [italic]dotnet watch[/].[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]:fire_extinguisher: forge failed to fire. This probably means your AppHost failed to build.[/]");
                    if (!settings.ShowBuildOutput)
                    {
                        AnsiConsole.MarkupLine("Re-run forge with --apphost-build-output to see the build output.");
                    }
                }
                // Just wait a lil for DCP to request some stuff
            });

        if (wrapper.InnerProcess is { HasExited: false })
        {
            await wrapper!.InnerProcess!.WaitForExitAsync();
        }
        
        return 0;
    }
    
    [DllImport("libc", SetLastError = true)]
    private static extern int kill(int pid, int sig);

    private const int SIGINT = 2;
}

public class WatchCommandSettings : CommandSettings
{
    [CommandOption("--apphost-build-output")]
    public bool ShowBuildOutput { get; set; }
    
    [CommandOption("-p|--port")]
    [DefaultValue(6969)]
    [Description("Set the runtime port for the DCP Integration host.")]
    public int Port { get; set; }
}

public static class WatchCommandExtensions
{
    public static IConfigurator AddWatchCommand(this IConfigurator app)
    {
        app.AddCommand<WatchCommand>("fire")
            .WithAlias("f")
            .WithAlias("watch")
            .WithDescription("Fire up the AppHost for local development (dotnet watch).")
            .WithExample(new[] { "fire" });
        return app;
    }
}