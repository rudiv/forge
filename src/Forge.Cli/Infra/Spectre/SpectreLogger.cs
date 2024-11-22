using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace Forge.Cli.Infra.Spectre;

public sealed class SpectreLoggerConfiguration
{
    public int EventId { get; set; }
}

public sealed class SpectreLogger(
    string name,
    Func<SpectreLoggerConfiguration> getCurrentConfig) : ILogger
{
    public Func<SpectreLoggerConfiguration> GetCurrentConfig { get; } = getCurrentConfig;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => default!;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var shortName = name.Length > 28 ? "..." + name[^25..] : name.PadLeft(28);
        var (dimMode, markup) = LevelMarkup(logLevel);
        AnsiConsole.MarkupLine(markup +
                               (dimMode == Markup.Closed ? " [dim]" : " ") +
                               (Runtime.Debug ? "(" + shortName + "): " : "") +
                               formatter(state, exception).EscapeMarkup() +
                               (dimMode != Markup.Off ? "[/]" : ""));
    }

    private (Markup dimMode, string markup) LevelMarkup(LogLevel level) => level switch
    {
        LogLevel.Trace => (Markup.Open, "[dim]\u25a0 trace"),
        LogLevel.Debug => (Markup.Closed, "[dim yellow1]\u25a0 debug[/]"),
        LogLevel.Information => (Markup.Off, "[dim blue]\u25a0 info [/]"),
        LogLevel.Warning => (Markup.Off, "[bold orange3]\u25a0 warn [/]"),
        LogLevel.Error => (Markup.Off, "[bold red]\u25a0 error[/]"),
        LogLevel.Critical => (Markup.Open, "[bold underline red on white]\u25a0 CRIT "),
        _ => (Markup.Off, "     ")
    };

    private enum Markup
    {
        Open,
        Closed,
        Off
    }
}

public sealed class SpectreLoggingProvider : ILoggerProvider
{
    private readonly IDisposable? _onChangeToken;
    private SpectreLoggerConfiguration _currentConfig;
    private readonly ConcurrentDictionary<string, SpectreLogger> _loggers =
        new(StringComparer.OrdinalIgnoreCase);

    public SpectreLoggingProvider(
        IOptionsMonitor<SpectreLoggerConfiguration> config)
    {
        _currentConfig = config.CurrentValue;
        _onChangeToken = config.OnChange(updatedConfig => _currentConfig = updatedConfig);
    }

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new SpectreLogger(name, GetCurrentConfig));

    private SpectreLoggerConfiguration GetCurrentConfig() => _currentConfig;

    public void Dispose()
    {
        _loggers.Clear();
        _onChangeToken?.Dispose();
    }
}

public static class ColorConsoleLoggerExtensions
{
    public static ILoggingBuilder AddSpectreLogger(
        this ILoggingBuilder builder)
    {
        builder.AddConfiguration();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, SpectreLoggingProvider>());

        LoggerProviderOptions.RegisterProviderOptions <SpectreLoggerConfiguration, SpectreLoggingProvider>(builder.Services);
        return builder;
    }
}