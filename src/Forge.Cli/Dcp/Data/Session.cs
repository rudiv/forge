using System.Text.Json.Serialization;

namespace Forge.Cli.Dcp.Data;

public class Session
{
    [JsonPropertyName("launch_configurations")]
    public LaunchConfiguration[] LaunchConfigurations { get; set; } = [];

    [JsonPropertyName("env")] public Env[] Env { get; set; } = [];

    [JsonPropertyName("args")] public string[] Args { get; set; } = [];
}

public class Env
{
    [JsonPropertyName("name")] public string Name { get; set; } = default!;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("value")]
    public string? Value { get; set; }
}

public class LaunchConfiguration
{
    [JsonPropertyName("type")] public string Type { get; set; } = default!;

    [JsonPropertyName("mode")] public string Mode { get; set; } = default!;

    [JsonPropertyName("project_path")] public string ProjectPath { get; set; } = default!;

    [JsonPropertyName("launch_profile")]
    public string? LaunchProfile { get; set; }

    [JsonPropertyName("disable_launch_profile")]
    public bool? DisableLaunchProfile { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Mode
{
    Debug,
    NoDebug
}