using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Forge.Cli.Dcp.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Forge.Cli.Dcp;

public class NotificationStreamHandler(ILogger<NotificationStreamHandler> logger) : IDisposable
{
    private WebSocket? socket;
    private Channel<ChannelMessage>? channel;
    private CancellationTokenSource cts = new();

    public async Task AcceptSocketAsync(WebSocket socket)
    {
        logger.LogTrace("DCP Notification socket accepted.");
        this.socket = socket;
        channel = Channel.CreateUnbounded<ChannelMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
        });
        await Task.WhenAll(HandleSocketAsync(cts.Token), HandleChannelAsync(cts.Token));
        await cts.CancelAsync();
    }

    public ChannelWriter<ChannelMessage> GetChannelWriter()
    {
        return channel!.Writer;
    }
    
    public async Task HandleChannelAsync(CancellationToken ct)
    {
        if (channel == null || socket == null) return;
        var reader = channel.Reader;
        while (await reader.WaitToReadAsync(ct))
        {
            while (reader.TryRead(out var message))
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(message);
                await socket!.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    ct);
            }
        }
    }

    public async Task HandleSocketAsync(CancellationToken ct)
    {
        if (socket == null) return;
        var buffer = new byte[1024];
        var receiveResult = await socket.ReceiveAsync(
            new ArraySegment<byte>(buffer), ct);

        while (!receiveResult.CloseStatus.HasValue)
        {
            logger.LogTrace("Received message from socket.");
            receiveResult = await socket.ReceiveAsync(
                new ArraySegment<byte>(buffer), ct);
        }

        await socket.CloseAsync(
            receiveResult.CloseStatus.Value,
            receiveResult.CloseStatusDescription,
            ct);
    }
    
    public async Task Middleware(HttpContext ctx, RequestDelegate next)
    {
        logger.LogTrace("Received request to {Path}", ctx.Request.Path);
        if (ctx.Request.Path == "/run_session/notify")
        {
            if (ctx.WebSockets.IsWebSocketRequest)
            {
                logger.LogTrace("Is Websocket, Accepting...");
                await AcceptSocketAsync(await ctx.WebSockets.AcceptWebSocketAsync());
            }
            else
            {
                logger.LogWarning("Did not receive Websocket upgrade...");
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }
        else
        {
            logger.LogTrace("Continuing middleware.");
            await next(ctx);
        }
    }

    public void Dispose()
    {
        socket?.Dispose();
    }
}