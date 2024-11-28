using Forge.Cli.Dcp.Data;
using Forge.Cli.Dcp.ExecutionModel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebSockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Forge.Cli.Dcp;

public class DcpSessionWebHost(
    ILogger<DcpSessionWebHost> logger,
    NotificationStreamHandler notificationStreamHandler,
    ManagedSessionRegistry sessionRegistry)
{
    private WebApplication? currentHost;
    private bool hotReload = true;
    // ReSharper disable once InconsistentNaming
    private static readonly string[] protocols_supported = [ "2024-03-03" ];
    
    public async Task StartWebHostAsync(int port, bool withHotReload = true)
    {
        hotReload = withHotReload;
        logger.LogTrace("Starting DCP Session WebHost on port {0}...", port);
        if (currentHost != null) return;

        var bld = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        bld.WebHost.UseKestrelCore()
            .ConfigureKestrel(o =>
            {
                o.ListenLocalhost(port);
            });
        bld.Services.AddRoutingCore();
        bld.Services.AddWebSockets(o =>
        {
            o.KeepAliveInterval = TimeSpan.FromSeconds(30);
        });
        var app = bld.Build();
        app.UseWebSockets();
        app.UseRouting();
        app.Use(notificationStreamHandler.Middleware);
        app.MapGet("/info", InfoEndpoint);
        app.MapPut("/run_session", RunSessionEndpoint);
        app.MapDelete("/run_session/{id}", DeleteSessionEndpoint);
        await app.StartAsync();
        logger.LogTrace("WebHost started.");
        currentHost = app;
    }

    private Task InfoEndpoint(HttpContext ctx) => ctx.Response.WriteAsJsonAsync(new { protocols_supported });

    private async Task<IResult> RunSessionEndpoint([FromBody] Session session, HttpContext ctx)
    {
        logger.LogTrace("DCP is trying to start a new session!");
        try
        {
            var process = new DotnetWrapper(o =>
            {
                o.Command = hotReload ? DotnetCommand.Watch : DotnetCommand.WatchNoHotReload;
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

    private async Task<IResult> DeleteSessionEndpoint([FromRoute] Guid id)
    {
        logger.LogTrace("DCP is trying to stop a session!");
        logger.LogTrace("Session: {Session}", id);
        try
        {
            await sessionRegistry.StopSession(id);
            return TypedResults.Ok();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Failed to stop session - " + ex.Message);
            logger.LogCritical(ex.StackTrace);
            return TypedResults.InternalServerError();
        }
    }
    
    public async Task StopWebHostAsync()
    {
        logger.LogTrace("Stopping DCP Session WebHost...");
        if (currentHost == null) return;
        await currentHost.StopAsync();
        currentHost = null;
    }
}