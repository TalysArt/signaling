using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Registra o serviço WebSocket
builder.Services.AddSingleton<SignalingService>();

var app = builder.Build();

// Log de requisições para debug
app.Use(async (context, next) =>
{
    Console.WriteLine($"[Request] {context.Request.Path}");
    await next();
});

// Habilita servir arquivos estáticos do wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

// Habilita WebSocket
app.UseWebSockets();

// Endpoint de WebSocket
app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var signalingService = context.RequestServices.GetRequiredService<SignalingService>();
        await signalingService.HandleWebSocketAsync(webSocket);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

app.Run();
