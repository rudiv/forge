using System.Text.Json;
using Forge.Cli.Dcp.Data;
using Forge.Cli.Dcp.ExecutionModel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Forge.Cli.Dcp;

public class Endpoints(ILogger<Endpoints> logger, ManagedSessionRegistry sessionRegistry)
{
    // ReSharper disable once InconsistentNaming
    private static readonly string[] protocols_supported = [ "2024-03-03" ];

    public Task InfoEndpoint(HttpContext ctx) => ctx.Response.WriteAsJsonAsync(new { protocols_supported });
    
    public async Task<IResult> RunSessionEndpoint([FromBody] Session session, HttpContext ctx)
    {
        logger.LogTrace("DCP is trying to start a new session!");
        logger.LogTrace("Session: {Session}", JsonSerializer.Serialize(session));
        try
        {
            var process = new DotnetWrapper(o =>
            {
                o.Command = DotnetCommand.Watch;
                o.ConnectChannel = true;
                o.EnvironmentVariables = session.Env.ToDictionary(m => m.Name, m => m.Value);
                o.ProjectPath = session.LaunchConfigurations.First().ProjectPath;
            });
            var sessionId = await sessionRegistry.NewSession(process);

            ctx.Response.Headers.Append("Location",
                "http://localhost:" + ctx.Request.Host.Port + "/run_session/" + sessionId);
            return TypedResults.Ok();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Failed to start session - " + ex.Message);
            logger.LogCritical(ex.StackTrace);
            return TypedResults.InternalServerError();
        }
    }
}