using System.Xml.Linq;
using Microsoft.Build.Construction;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Forge.Cli.Infra;

public class AppHostResolution
{
    private readonly ILogger<AppHostResolution> logger;
    public string? AppHostPath { get; private set; }

    public AppHostResolution(ILogger<AppHostResolution> logger)
    {
        this.logger = logger;
        AppHostPath = FindClosestAppHost();
    }
    
    private string? FindClosestAppHost()
    {
        string? foundCsProjPath = default!;
        AnsiConsole.Status()
            .Start("Detecting nearest AppHost...", (ctx) =>
            {
                ctx.Spinner(Spinner.Known.Hamburger);
                var rootPath = Environment.CurrentDirectory;
                foundCsProjPath = Directory.GetFiles(rootPath, "*.csproj").FirstOrDefault();
                if (foundCsProjPath == null)
                {
                    logger.LogTrace("No Immediate .csproj file found, looking for solution file...");
                    // Check if there's an SLN in here
                    var slnFile = Directory.GetFiles(rootPath, "*.sln").FirstOrDefault();
                    if (slnFile != null)
                    {
                        logger.LogTrace("Found {Solution}, looking for an AppHost...", slnFile);
                        var solution = SolutionFile.Parse(slnFile);
                        var foundAppHosts = 0;
                        foreach (var proj in solution.ProjectsInOrder)
                        {
                            if (!proj.AbsolutePath.EndsWith(".csproj")) continue;
                            logger.LogTrace("Testing csproj {Project}", proj.AbsolutePath);
                            
                            if (CsprojIsAppHost(proj.AbsolutePath))
                            {
                                if (foundAppHosts++ == 0)
                                {
                                    logger.LogInformation("Working with this AppHost: {AppHost}", proj.AbsolutePath);
                                    foundCsProjPath = proj.AbsolutePath;
                                }
                                else
                                {
                                    logger.LogWarning("Found additional AppHost at {Path}, ignoring.", proj.AbsolutePath);
                                }
                            }
                        }
                    }
                } else
                {
                    if (CsprojIsAppHost(foundCsProjPath))
                    {
                        logger.LogInformation("Working with this AppHost: {AppHost}", foundCsProjPath);
                    }
                    else
                    {
                        logger.LogWarning("Found a .csproj file, but it's not an AppHost.");
                        foundCsProjPath = null;
                    }
                }

                if (foundCsProjPath == null)
                {
                    logger.LogDebug("No AppHost found in this directory.");
                }
            });
        
        return foundCsProjPath;
    }

    private bool CsprojIsAppHost(string pathToCsproj)
    {
        var doc = XDocument.Load(pathToCsproj);
        return doc
            .Descendants("Sdk")
            .Any(sdk => (string?)sdk.Attribute("Name") == "Aspire.AppHost.Sdk");
    }
}