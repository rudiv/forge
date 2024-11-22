using System.Net.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebSockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Forge.Cli.Dcp;

public class DcpSessionWebHost(ILogger<DcpSessionWebHost> logger, Endpoints endpoints, NotificationStreamHandler notificationStreamHandler)
{
    private IHost? currentHost;
    
    public async Task StartWebHostAsync(int port)
    {
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
        app.MapGet("/info", endpoints.InfoEndpoint);
        app.MapPut("/run_session", endpoints.RunSessionEndpoint);
        await app.StartAsync();
        currentHost = app;
    }
    
    public async Task StopWebHostAsync()
    {
        logger.LogTrace("Stopping DCP Session WebHost...");
        if (currentHost == null) return;
        await currentHost.StopAsync();
        currentHost = null;
    }
}