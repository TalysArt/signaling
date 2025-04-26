// SignalingService.cs
using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public class SignalingService
{
    // Lock para acesso concorrente
    private readonly object _lock = new();

    // Armazenamos no máximo um rover e um controle por vez
    private WebSocket? _roverSocket;
    private WebSocket? _controlSocket;

    /// <summary>
    /// Método principal que trata cada nova conexão WebSocket
    /// </summary>
    public async Task HandleWebSocketAsync(WebSocket socket)
    {
        // 1) Recebe a mensagem de registro inicial
        //    { "type":"register", "role":"rover" } ou { "type":"register", "role":"control" }
        var buffer = new byte[4 * 1024];
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        if (result.MessageType != WebSocketMessageType.Text)
        {
            await socket.CloseAsync(WebSocketCloseStatus.InvalidMessageType,
                                    "Somente texto permitido", CancellationToken.None);
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

        // 2) Armazena o socket no campo correspondente
        lock (_lock)
        {
            if (reg.Role == "rover")      _roverSocket = socket;
            else if (reg.Role == "control") _controlSocket = socket;
        }

        WebSocket? peerSocket = reg.Role == "rover" ? _controlSocket : _roverSocket;

        // 3) Loop de encaminhamento: tudo que chegar aqui é repassado ao peer
        while (socket.State == WebSocketState.Open)
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
                break;

            // Só envia se o peer existir e estiver aberto
            if (peerSocket?.State == WebSocketState.Open)
            {
                await peerSocket.SendAsync(
                    new ArraySegment<byte>(buffer, 0, result.Count),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken: CancellationToken.None);
            }
        }

        // 4) Cleanup: remove referência quando desconectar
        lock (_lock)
        {
            if (_roverSocket == socket)   _roverSocket   = null;
            if (_controlSocket == socket) _controlSocket = null;
        }

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Conexão encerrada", CancellationToken.None);
    }

    // Modelo para a mensagem de registro
    private class RegisterMessage
    {
        public string Type { get; set; } = default!;  // deve ser sempre "register"
        public string Role { get; set; } = default!;  // "rover" ou "control"
    }
}
