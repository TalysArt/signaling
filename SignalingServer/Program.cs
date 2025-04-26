using System.Net;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

// 1) Configure Kestrel para ouvir em ANY IP na porta 9090
builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Any, 9090); 
});

var app = builder.Build();

// 2) Habilita handshake WebSocket
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });

// 3) Intercepta TODO Upgrade WebSocket (raiz “/” ou qualquer rota)
app.Use(async (context, next) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var socket = await context.WebSockets.AcceptWebSocketAsync();

        // Envia “Hello World” imediatamente
        var hello = Encoding.UTF8.GetBytes("Hello World");
        await socket.SendAsync(hello, WebSocketMessageType.Text, true, CancellationToken.None);

        // Loop de echo
        var buffer = new byte[1024];
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (!result.CloseStatus.HasValue)
            {
                await socket.SendAsync(
                    new ArraySegment<byte>(buffer, 0, result.Count),
                    result.MessageType,
                    result.EndOfMessage,
                    CancellationToken.None);
            }
        }
        while (!result.CloseStatus.HasValue);

        await socket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        return; // não cai no next(), já tratamos o WS
    }

    await next();
});

// 4) Serve apenas index.html e client.js em wwwroot/
app.UseDefaultFiles();
app.UseStaticFiles();

// 5) Rota HTTP “/” redireciona para /index.html
app.MapGet("/", () => Results.Redirect("/index.html"));

app.Run();
