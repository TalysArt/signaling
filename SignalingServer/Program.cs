using System.Net;
using System.Net.WebSockets;
using System.Text;
public class SimpleWebSocketServer
{
	protected SimpleWebSocketServer()
	{}

	public static async Task Main(string[] args)
	{
		int port = 5296;
		string uriPrefix = $"http://localhost:{port}/ws/";

		HttpListener listener = new();
		listener.Prefixes.Add(uriPrefix);
		listener.Start();

		Console.WriteLine($"WebSocket server started at {uriPrefix}");

		while (true)
		{
			HttpListenerContext context = await listener.GetContextAsync();

			if (context.Request.IsWebSocketRequest)
			{
				Console.WriteLine("Client connected!");

				HttpListenerWebSocketContext webSocketContext = await context.AcceptWebSocketAsync(null);

				// Handle WebSocket in a separate task
				_ = Task.Run(() => HandleWebSocketAsync(webSocketContext.WebSocket));
			}
			else
			{
				context.Response.StatusCode = (int)HttpStatusCode.NotFound;
				context.Response.Close();
				Console.WriteLine("Not a WebSocket request.");
			}
		}
	}

	private static async Task HandleWebSocketAsync(WebSocket webSocket)
	{
		try
		{
			byte[] buffer = new byte[1024];

			// Keep the connection alive (you would typically handle messages here)
			while (webSocket.State == WebSocketState.Open)
			{
				Console.Write("Enter message (type 'exit' to disconnect): ");

				WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
				string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
				Console.WriteLine($"Received: {message}");

				if (message.ToLower() == "exit")
				{
					Console.WriteLine("Closing connection...");
					await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnected", CancellationToken.None);
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"WebSocket error: {ex.Message}");
		}
		finally
		{
			if (webSocket.State != WebSocketState.Closed)
			{
				await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
			}

			webSocket.Dispose();
			Console.WriteLine("Connection fully closed.");
		}
	}
}