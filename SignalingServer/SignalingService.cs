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
    private WebSocket? _monitoringSocket;

    public async Task HandleWebSocketAsync(WebSocket socket)
    {
        var buffer = new byte[8 * 1024]; // Aumentado para lidar com mensagens grandes
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        if (result.MessageType != WebSocketMessageType.Text)
        {
            await socket.CloseAsync(WebSocketCloseStatus.InvalidMessageType,
                "Somente mensagens de texto permitidas", CancellationToken.None);
            return;
        }

        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        RegisterMessage? reg;
        try
        {
            reg = JsonSerializer.Deserialize<RegisterMessage>(json, options);
        }
        catch
        {
            await socket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData,
                "Formato de registro inválido", CancellationToken.None);
            return;
        }

        if (reg?.Type != "register" ||
            (reg.Role != "rover" && reg.Role != "control" && reg.Role != "monitoring"))
        {
            await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation,
                "Role inválido", CancellationToken.None);
            return;
        }

        lock (_lock)
        {
            switch (reg.Role)
            {
                case "rover":
                    if (_roverSocket != null && _roverSocket.State == WebSocketState.Open)
                        _roverSocket.Abort();
                    _roverSocket = socket;
                    Console.WriteLine("[Server] Rover registrado!");
                    break;
                case "control":
                    if (_controlSocket != null && _controlSocket.State == WebSocketState.Open)
                        _controlSocket.Abort();
                    _controlSocket = socket;
                    Console.WriteLine("[Server] Controle registrado!");
                    break;
                case "monitoring":
                    if (_monitoringSocket != null && _monitoringSocket.State == WebSocketState.Open)
                        _monitoringSocket.Abort();
                    _monitoringSocket = socket;
                    Console.WriteLine("[Server] Monitoramento registrado!");
                    break;
            }
        }

        await HandleMessagesAsync(socket, reg.Role);
    }

    private async Task HandleMessagesAsync(WebSocket socket, string role)
    {
        var buffer = new byte[8 * 1024];

        while (socket.State == WebSocketState.Open)
        {
            try
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var baseMessage = JsonSerializer.Deserialize<BaseMessage>(json, options);

                if (baseMessage == null || string.IsNullOrEmpty(baseMessage.Type))
                {
                    Console.WriteLine("[Server] Mensagem ignorada: inválida");
                    continue;
                }

                switch (baseMessage.Type)
                {
                    case "ping":
                        Console.WriteLine($"[Server] Ping recebido de {role}");
                        break;

                    case "command":
                        if (role == "control" && _roverSocket?.State == WebSocketState.Open)
                        {
                            await _roverSocket.SendAsync(
                                new ArraySegment<byte>(buffer, 0, result.Count),
                                WebSocketMessageType.Text,
                                true,
                                CancellationToken.None);
                            Console.WriteLine("[Server] Comando enviado para Rover");
                        }
                        break;

                    case "sensor_data":
                        if (role == "rover")
                        {
                            if (_controlSocket?.State == WebSocketState.Open)
                            {
                                await _controlSocket.SendAsync(
                                    new ArraySegment<byte>(buffer, 0, result.Count),
                                    WebSocketMessageType.Text,
                                    true,
                                    CancellationToken.None);
                                Console.WriteLine("[Server] Dados de sensor enviados para Controle");
                            }
                            if (_monitoringSocket?.State == WebSocketState.Open)
                            {
                                await _monitoringSocket.SendAsync(
                                    new ArraySegment<byte>(buffer, 0, result.Count),
                                    WebSocketMessageType.Text,
                                    true,
                                    CancellationToken.None);
                                Console.WriteLine("[Server] Dados de sensor enviados para Monitoramento");
                            }
                        }
                        break;

                    default:
                        Console.WriteLine($"[Server] Tipo de mensagem desconhecido: {baseMessage.Type}");
                        break;
                }
            }
            catch (WebSocketException ex)
            {
                Console.WriteLine($"[Server] Erro de WebSocket: {ex.Message}");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] Erro inesperado: {ex.Message}");
                break;
            }
        }

        // Cleanup
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
            if (_monitoringSocket == socket)
            {
                _monitoringSocket = null;
                Console.WriteLine("[Server] Monitoramento desconectado");
            }
        }

        try
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Conexão encerrada", CancellationToken.None);
        }
        catch { /* Ignore close exceptions */ }
    }

    private class RegisterMessage
    {
        public string Type { get; set; } = default!;
        public string Role { get; set; } = default!;
    }

    private class BaseMessage
    {
        public string Type { get; set; } = default!;
    }
}
