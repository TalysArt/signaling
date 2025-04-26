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
                if (_roverSocket != null && _roverSocket.State == WebSocketState.Open)
                {
                    _roverSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Substituído", CancellationToken.None).Wait();
                    Console.WriteLine("[Server] Rover anterior desconectado!");
                }
                _roverSocket = socket;
                Console.WriteLine("[Server] Rover registrado!");
            }
            else if (reg.Role == "control")
            {
                if (_controlSocket != null && _controlSocket.State == WebSocketState.Open)
                {
                    _controlSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Substituído", CancellationToken.None).Wait();
                    Console.WriteLine("[Server] Controle anterior desconectado!");
                }
                _controlSocket = socket;
                Console.WriteLine("[Server] Controle registrado!");
            }
        }

        // Loop principal
        while (socket.State == WebSocketState.Open)
        {
            try
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                var msgText = Encoding.UTF8.GetString(buffer, 0, result.Count);
                Console.WriteLine($"[Server] Mensagem recebida de {reg.Role}: {msgText}");

                WebSocket? peerSocket;
                lock (_lock)
                {
                    peerSocket = reg.Role == "rover" ? _controlSocket : _roverSocket;
                }

                if (peerSocket != null && peerSocket.State == WebSocketState.Open)
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
                    Console.WriteLine("[Server] Peer desconectado ou inválido. Mensagem descartada.");
                }
            }
            catch (WebSocketException ex)
            {
                Console.WriteLine($"[Server] Exceção WebSocket: {ex.Message}");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] Erro inesperado: {ex.Message}");
                break;
            }
        }

        // Cleanup no fim
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

        try
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Conexão encerrada", CancellationToken.None);
        }
        catch { /* socket já pode estar fechado */ }
    }

    private class RegisterMessage
    {
        public string Type { get; set; } = default!;
        public string Role { get; set; } = default!;
    }
}
