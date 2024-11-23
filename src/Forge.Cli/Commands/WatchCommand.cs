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

public class WatchCommand(AppHostResolution appHostResolution, DcpSessionWebHost dcpSessionWebHost, Endpoints endpoints, ILogger<WatchCommand> logger) : AsyncCommand<WatchCommandSettings>
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
                if (settings.NoHotReload) endpoints.SetNoHotReload();
                await dcpSessionWebHost.StartWebHostAsync(settings.Port);
                var startupTs = new TaskCompletionSource<bool>();
                wrapper = new DotnetWrapper(o =>
                {
                    o.Command = DotnetCommand.Run;
                    o.EnvironmentVariables = new()
                    {
                        { "DEBUG_SESSION_PORT", settings.Port.ToString() },
                        { "DEBUG_SESSION_TOKEN", Guid.NewGuid().ToString() }
                    };
                    o.ProjectPath = appHostResolution.AppHostPath;
                    o.OutputPipe = (op) =>
                    {
                        if (op.Contains("Login to the dashboard at"))
                        {
                            AnsiConsole.MarkupLine("[bold]AppHost started: " + op.Trim().EscapeMarkup() + "[/]");
                            
                            startupTs.TrySetResult(true);
                        }
                        if (settings.ShowBuildOutput)
                        {
                            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[grey]{op}[/]");
                        }
                    };
                    o.ErrorPipe = (op) =>
                    {
                        if (settings.ShowBuildOutput)
                        {
                            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]{op}[/]");
                        }
                    };
                });
                wrapper.Start();
                _ = wrapper.InnerTask.Task.ContinueWith(o =>
                {
                    logger.LogTrace("AppHost exited with code {0} after {1}", o.Result.ExitCode, o.Result.RunTime);
                    startupTs.TrySetResult(false);
                }, TaskContinuationOptions.ExecuteSynchronously);
                var started = await startupTs.Task;
                if (started)
                {
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
            });

        if (!wrapper.InnerTask.Task.IsCompleted)
        {
            await wrapper!.InnerTask;
        }
        
        return 0;
    }
}

public class WatchCommandSettings : CommandSettings
{
    [CommandOption("--apphost-build-output")]
    public bool ShowBuildOutput { get; set; }
    
    [CommandOption("--no-hot-reload")]
    public bool NoHotReload { get; set; }
    
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