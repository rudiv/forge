using Forge.Cli.Infra;
using Microsoft.Build.Construction;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Forge.Cli.Commands;

public class DefaultNoOpCommand(AppHostResolution appHostResolution) : Command
{
    public override int Execute(CommandContext context)
    {
        if (appHostResolution.AppHostPath == null)
        {
            AnsiConsole.MarkupLine(
                "[red]No AppHost project was found, you need to run forge from within the Solution or AppHost project folder.[/]");
            return 1;
        }
        
        var withoutExt = Path.GetFileNameWithoutExtension(appHostResolution.AppHostPath);
        AnsiConsole.MarkupLine("[green]:hammer_and_wrench: Ready to forge {0}.[/]", withoutExt);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("To get started, try running [bold]forge fire[/] :fire: to start your project with dotnet watch per-project.");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("Run [bold]forge --help[/] to see more options.");
        return 0;
    }
}