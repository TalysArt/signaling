using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// Permitir arquivos estáticos (html, js, etc)
app.UseDefaultFiles();
app.UseStaticFiles();

// Ativar WebSockets
app.UseWebSockets();

app.Map("/", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        Console.WriteLine("WebSocket conectado!");

        var buffer = new byte[1024 * 4];
        WebSocketReceiveResult result;
        do
        {
            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            Console.WriteLine($"Mensagem recebida: {message}");

            // Responde de volta
            var serverMsg = Encoding.UTF8.GetBytes($"Server recebeu: {message}");
            await webSocket.SendAsync(new ArraySegment<byte>(serverMsg), result.MessageType, result.EndOfMessage, CancellationToken.None);

        } while (!result.CloseStatus.HasValue);

        await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        Console.WriteLine("WebSocket desconectado.");
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

app.Run("http://0.0.0.0:9090");
