using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;


public class UserConnection
{
    public UserConnection(WebSocket webSocket, string username)
    {
        WebSocket = webSocket;
        Username = username;
    }

    public WebSocket WebSocket { get; set; }
    public string Username { get; set; }
    public string? OtherUsername { get; set; }
}

public class SignalingService
{
    private readonly ConcurrentDictionary<string, UserConnection> _users = new();
    private readonly ConcurrentDictionary<WebSocket, UserConnection> _connections = new();

    public async Task HandleWebSocketAsync(WebSocket webSocket)
    {
        UserConnection currentUser = null;
        var buffer = new byte[1024 * 4];

        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Text)
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                try
                {
                    var jsonDoc = JsonDocument.Parse(message);
                    var type = jsonDoc.RootElement.GetProperty("type").GetString();

                    if (currentUser == null && type != "login")
                    {
                        await SendMessageAsync(webSocket, new { type = "error", message = "Must login first" });
                        continue;
                    }

                    switch (type)
                    {
                        case "login":
                            var name = jsonDoc.RootElement.GetProperty("name").GetString();
                            if (name == null)
                            {
                                await SendMessageAsync(webSocket, new { type = "error", message = "Username cannot be null" });
                                continue;
                            }
                            if (_users.ContainsKey(name))
                            {
                                await SendMessageAsync(webSocket, new { type = "login", success = false });
                            }
                            else
                            {
                                currentUser = new UserConnection(webSocket, name);
                                _users[name] = currentUser;
                                _connections[webSocket] = currentUser;
                                await SendMessageAsync(webSocket, new { type = "login", success = true });
                            }
                            break;

                        case "offer":
                            if (currentUser != null)
                            {
                                var targetName = jsonDoc.RootElement.GetProperty("name").GetString();
                                var offer = jsonDoc.RootElement.GetProperty("offer");
                                if (targetName != null && _users.TryGetValue(targetName, out var targetUser))
                                {
                                    currentUser.OtherUsername = targetName;
                                    await SendMessageAsync(targetUser.WebSocket, new { type = "offer", offer = offer, name = currentUser.Username });
                                }
                                else
                                {
                                    await SendMessageAsync(webSocket, new { type = "error", message = "Target user not found" });
                                }
                            }
                            break;

                        case "answer":
                            if (currentUser != null)
                            {
                                var targetName = jsonDoc.RootElement.GetProperty("name").GetString();
                                var answer = jsonDoc.RootElement.GetProperty("answer");
                                if (targetName != null && _users.TryGetValue(targetName, out var targetUser))
                                {
                                    currentUser.OtherUsername = targetName;
                                    await SendMessageAsync(targetUser.WebSocket, new { type = "answer", answer = answer });
                                }
                                else
                                {
                                    await SendMessageAsync(webSocket, new { type = "error", message = "Target user not found" });
                                }
                            }
                            break;

                        case "candidate":
                            if (currentUser != null)
                            {
                                var targetName = jsonDoc.RootElement.GetProperty("name").GetString();
                                var candidate = jsonDoc.RootElement.GetProperty("candidate");
                                if (targetName != null && _users.TryGetValue(targetName, out var targetUser))
                                {
                                    await SendMessageAsync(targetUser.WebSocket, new { type = "candidate", candidate = candidate });
                                }
                                else
                                {
                                    await SendMessageAsync(webSocket, new { type = "error", message = "Target user not found" });
                                }
                            }
                            break;

                        case "leave":
                            if (currentUser != null)
                            {
                                var targetName = jsonDoc.RootElement.GetProperty("name").GetString();
                                if (targetName != null && _users.TryGetValue(targetName, out var targetUser))
                                {
                                    targetUser.OtherUsername = null;
                                    await SendMessageAsync(targetUser.WebSocket, new { type = "leave" });
                                }
                                else
                                {
                                    await SendMessageAsync(webSocket, new { type = "error", message = "Target user not found" });
                                }
                            }
                            break;

                        default:
                            await SendMessageAsync(webSocket, new { type = "error", message = "Unrecognized command: " + type });
                            break;
                    }
                }
                catch (JsonException)
                {
                    await SendMessageAsync(webSocket, new { type = "error", message = "Invalid JSON" });
                }
            }
            else if (result.MessageType == WebSocketMessageType.Close)
            {
                if (currentUser != null)
                {
                    _users.TryRemove(currentUser.Username, out _);
                    _connections.TryRemove(webSocket, out _);
                    if (currentUser.OtherUsername != null && _users.TryGetValue(currentUser.OtherUsername, out var otherUser))
                    {
                        otherUser.OtherUsername = null;
                        await SendMessageAsync(otherUser.WebSocket, new { type = "leave" });
                    }
                }
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", CancellationToken.None);
            }
        }
    }

    private async Task SendMessageAsync(WebSocket webSocket, object message)
    {
        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }
}