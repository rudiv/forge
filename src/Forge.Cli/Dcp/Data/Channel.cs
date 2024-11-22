using System.Text.Json.Serialization;

namespace Forge.Cli.Dcp.Data;

[JsonDerivedType(typeof(ProcessRestartedNotification))]
[JsonDerivedType(typeof(SessionTerminatedNotification))]
[JsonDerivedType(typeof(ServiceLogsNotification))]
public abstract class ChannelMessage
{
    [JsonPropertyName("notification_type")]
    public NotificationType NotificationType { get; set; }

    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = default!;
}

public class ProcessRestartedNotification : ChannelMessage
{
    [JsonPropertyName("pid")]
    public int ProcessId { get; set; }
}

public class SessionTerminatedNotification : ChannelMessage
{
    [JsonPropertyName("exit_code")]
    public int ExitCode { get; set; }
}

public class ServiceLogsNotification : ChannelMessage
{
    [JsonPropertyName("is_std_err")]
    public bool IsStdError { get; set; }
    
    [JsonPropertyName("log_message")]
    public string LogMessage { get; set; } = default!;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationType {
    [JsonStringEnumMemberName("processRestarted")]
    ProcessRestarted,
    [JsonStringEnumMemberName("sessionTerminated")]
    SessionTerminated,
    [JsonStringEnumMemberName("serviceLogs")]
    ServiceLogs
}