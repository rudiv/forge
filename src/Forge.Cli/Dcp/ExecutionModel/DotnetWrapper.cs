using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using CliWrap;
using CliWrap.EventStream;
using Forge.Cli.Dcp.Data;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Forge.Cli.Dcp.ExecutionModel;

public class DotnetWrapper
{
    public static List<DotnetWrapper> Instances = new();
    
    private readonly DotnetWrapperConfiguration config;
    private readonly CancellationTokenSource gracefulShutdown = new();
    private readonly CancellationTokenSource forcefulShutdown = new();
    public Command? InnerProcess;
    public CommandTask<CommandResult> InnerTask = default!;
    private Guid? sessionId;
    private ChannelWriter<ChannelMessage>? writer;
    
    public DotnetWrapper(Action<DotnetWrapperConfiguration> configure)
    {
        config = new DotnetWrapperConfiguration();
        configure(config);
    }

    public void SetStreamHandler(Guid session, NotificationStreamHandler handler)
    {
        sessionId = session;
        writer = handler.GetChannelWriter();
    }

    public int Start()
    {
        config.EnvironmentVariables.TryAdd("DOTNET_ENVIRONMENT", "Development");
        config.EnvironmentVariables.TryAdd("DOTNET_SHUTDOWNTIMEOUTSECONDS", "20");
        
        string[] cmdArgs = config.Command switch
        {
            DotnetCommand.Run => ["run", "--project", $"{config.ProjectPath}"],
            DotnetCommand.Watch => ["watch", "--no-launch-profile", "--non-interactive", "--project", $"{config.ProjectPath}" ],
            _ => throw new ArgumentOutOfRangeException()
        };

        List<PipeTarget> outputPipes =
        [
            PipeTarget.ToDelegate(o => WriteToChannel(o, false))
        ];
        if (config.OutputPipe != null)
        {
            outputPipes.Add(PipeTarget.ToDelegate(o => config.OutputPipe!(o)));
        }
        List<PipeTarget> errorPipes =
        [
            PipeTarget.ToDelegate(o => WriteToChannel(o, true))
        ];
        if (config.ErrorPipe != null)
        {
            errorPipes.Add(PipeTarget.ToDelegate(o => config.ErrorPipe!(o)));
        }

        InnerProcess = CliWrap.Cli.Wrap("dotnet")
            .WithArguments(cmdArgs)
            .WithEnvironmentVariables(config.EnvironmentVariables)
            .WithStandardOutputPipe(PipeTarget.Merge(outputPipes))
            .WithStandardErrorPipe(PipeTarget.Merge(errorPipes));

        Instances.Add(this);

        if (config.Command == DotnetCommand.Watch)
        {
            var projectName = Path.GetFileNameWithoutExtension(config.ProjectPath);
            AnsiConsole.MarkupLine("[dim]DCP Requested startup of {0}, overridden to run via dotnet watch.[/]", projectName);
        }

        InnerTask = InnerProcess.ExecuteAsync(forcefulShutdown.Token, gracefulShutdown.Token);
        _ = InnerTask.Task.ContinueWith(o =>
        {
            writer?.TryWrite(new SessionTerminatedNotification()
            {
                NotificationType = NotificationType.SessionTerminated,
                SessionId = sessionId!.ToString()!
            });

            Instances.Remove(this);
        }, TaskContinuationOptions.ExecuteSynchronously);
        
        return InnerTask.ProcessId;
    }
    
    private void WriteToChannel(string message, bool isError)
    {
        writer?.TryWrite(new ServiceLogsNotification
        {
            LogMessage = message,
            NotificationType = NotificationType.ServiceLogs,
            IsStdError = isError,
            SessionId = sessionId?.ToString() ?? string.Empty
        });
    }

    public async Task StopAsync()
    {
        if (InnerProcess == null) return;

        forcefulShutdown.CancelAfter(15_000);
        await gracefulShutdown.CancelAsync();

        Instances.Remove(this);
    }
}

public class DotnetWrapperConfiguration
{
    public DotnetCommand Command { get; set; } = DotnetCommand.Run;

    public string ProjectPath { get; set; } = "";

    public bool ConnectChannel { get; set; } = false;

    public Dictionary<string, string?> EnvironmentVariables { get; set; } = [];
    
    public Action<string>? OutputPipe { get; set; }
    public Action<string>? ErrorPipe { get; set; }
}

public enum DotnetCommand
{
    Run,
    Watch
}