using System.Collections.Specialized;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Forge.Cli.Dcp.Data;
using Spectre.Console;

namespace Forge.Cli.Dcp.ExecutionModel;

public class DotnetWrapper
{
    public static List<DotnetWrapper> Instances = new();
    
    private readonly DotnetWrapperConfiguration config;
    public Process? InnerProcess;
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

    public async Task<int> StartAsync()
    {
        config.EnvironmentVariables.TryAdd("DOTNET_ENVIRONMENT", "Development");
        config.EnvironmentVariables.TryAdd("DOTNET_SHUTDOWNTIMEOUTSECONDS", "20");
        
        var psi = new ProcessStartInfo("dotnet")
        {
            Arguments = (config.Command == DotnetCommand.Run ? "run --project " : "watch --no-launch-profile --non-interactive --project ") + config.ProjectPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (var env in config.EnvironmentVariables)
        {
            psi.EnvironmentVariables[env.Key] = env.Value;
        }

        InnerProcess = new Process
        {
            EnableRaisingEvents = true,
            StartInfo = psi,
        };

        InnerProcess.Start();
        InnerProcess.ErrorDataReceived += InnerProcessOnErrorDataReceived;
        InnerProcess.Exited += InnerProcessOnExited;
        InnerProcess.OutputDataReceived += InnerProcessOnOutputDataReceived;
        InnerProcess.BeginOutputReadLine();
        InnerProcess.BeginErrorReadLine();

        Instances.Add(this);

        if (config.Command == DotnetCommand.Watch)
        {
            var projectName = Path.GetFileNameWithoutExtension(config.ProjectPath);
            AnsiConsole.MarkupLine("[dim]DCP Requested startup of {0}, overridden to run via dotnet watch.[/]", projectName);
        }

        return InnerProcess.Id;
    }

    private void InnerProcessOnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        writer?.TryWrite(new ServiceLogsNotification()
        {
            LogMessage = e.Data!,
            NotificationType = NotificationType.ServiceLogs,
            IsStdError = false,
            SessionId = sessionId!.ToString()!
        });
    }

    private void InnerProcessOnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        writer?.TryWrite(new ServiceLogsNotification()
        {
            LogMessage = e.Data!,
            NotificationType = NotificationType.ServiceLogs,
            IsStdError = true,
            SessionId = sessionId!.ToString()!
        });
    }

    private void InnerProcessOnExited(object? sender, EventArgs e)
    {
        writer?.TryWrite(new SessionTerminatedNotification()
        {
            NotificationType = NotificationType.SessionTerminated,
            SessionId = sessionId!.ToString()!
        });
    }

    public async Task StopAsync()
    {
        if (InnerProcess == null) return;
        var ct = new CancellationTokenSource();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            GenerateConsoleCtrlEvent(ConsoleCtrlEvent.CtrlC, (uint)InnerProcess.Id);
        }
        else
        {
            Process.Start("kill", "-s INT " + InnerProcess.Id);
        }

        ct.CancelAfter(30_000);
        await InnerProcess.WaitForExitAsync(ct.Token);
        if (!InnerProcess.HasExited)
        {
            InnerProcess.Kill();
        }

        Instances.Remove(this);
    }
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GenerateConsoleCtrlEvent(ConsoleCtrlEvent sigevent, uint dwProcessGroupId);

    private enum ConsoleCtrlEvent
    {
        CtrlC = 0,
    }
}

public class DotnetWrapperConfiguration
{
    public DotnetCommand Command { get; set; } = DotnetCommand.Run;

    public string ProjectPath { get; set; } = "";

    public bool ConnectChannel { get; set; } = false;

    public Dictionary<string, string?> EnvironmentVariables { get; set; } = [];
}

public enum DotnetCommand
{
    Run,
    Watch
}