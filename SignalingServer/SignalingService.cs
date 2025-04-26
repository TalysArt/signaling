using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public class SignalingService
{
    private readonly object _lock = new();
    private WebSocket? _roverSocket;
    private WebSocket? _controlSocket;

    public async Task HandleWebSocketAsync(WebSocket socket)
    {
        var buffer = new byte[4 * 1024];
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        if (result.MessageType != WebSocketMessageType.Text)
        {
            await socket.CloseAsync(WebSocketCloseStatus.InvalidMessageType,
                "Somente texto permitido", CancellationToken.None);
            return;
        }

        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        RegisterMessage? reg;
        try
        {
            reg = JsonSerializer.Deserialize<RegisterMessage>(json, options);
        }
        catch
        {
            await socket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData,
                "JSON inválido", CancellationToken.None);
            return;
        }

        if (reg?.Type != "register" ||
            (reg.Role != "rover" && reg.Role != "control"))
        {
            await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation,
                "Tipo ou role inválido", CancellationToken.None);
            return;
        }

        lock (_lock)
        {
            if (reg.Role == "rover")
            {
                _roverSocket = socket;
                Console.WriteLine("[Server] Rover registrado!");
            }
            else if (reg.Role == "control")
            {
                _controlSocket = socket;
                Console.WriteLine("[Server] Controle registrado!");
            }
        }

        // Pega dinamicamente quem é o peer A CADA mensagem recebida
WebSocket? peerSocket;

lock (_lock)
{
    peerSocket = reg.Role == "rover" ? _controlSocket : _roverSocket;
}

if (peerSocket?.State == WebSocketState.Open)
{
    await peerSocket.SendAsync(
        new ArraySegment<byte>(buffer, 0, result.Count),
        WebSocketMessageType.Text,
        true,
        CancellationToken.None
    );

    Console.WriteLine($"[Server] Mensagem repassada para {(reg.Role == "rover" ? "controle" : "rover")}");
}
else
{
    Console.WriteLine("[Server] Peer não conectado no momento do envio, descartando mensagem...");
}


        while (socket.State == WebSocketState.Open)
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
                break;

            var msgText = Encoding.UTF8.GetString(buffer, 0, result.Count);
            Console.WriteLine($"[Server] Mensagem recebida de {reg.Role}: {msgText}");

            if (peerSocket?.State == WebSocketState.Open)
            {
                await peerSocket.SendAsync(
                    new ArraySegment<byte>(buffer, 0, result.Count),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);

                Console.WriteLine($"[Server] Mensagem repassada para {(reg.Role == "rover" ? "controle" : "rover")}");
            }
            else
            {
                Console.WriteLine("[Server] Peer não conectado, descartando mensagem...");
            }
        }

        lock (_lock)
        {
            if (_roverSocket == socket)
            {
                _roverSocket = null;
                Console.WriteLine("[Server] Rover desconectado");
            }
            if (_controlSocket == socket)
            {
                _controlSocket = null;
                Console.WriteLine("[Server] Controle desconectado");
            }
        }

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Conexão encerrada", CancellationToken.None);
    }

    private class RegisterMessage
    {
        public string Type { get; set; } = default!;
        public string Role { get; set; } = default!;
    }
}
