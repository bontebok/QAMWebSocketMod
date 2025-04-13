using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class WebSocketManager
{
    private readonly ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();
    private ClientWebSocket _webSocket;
    private CancellationTokenSource _cancellationTokenSource;
    private const int ReconnectDelayMs = 5000; // Delay before attempting to reconnect

    public async Task StartWebSocketAsync(Uri uri)
    {
        _cancellationTokenSource = new CancellationTokenSource();

        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                _webSocket = new ClientWebSocket();

                await _webSocket.ConnectAsync(uri, _cancellationTokenSource.Token);

                // Start a task to receive messages
                await ReceiveMessagesAsync(_cancellationTokenSource.Token);
            }
            catch
            {
                // Wait before attempting to reconnect
                await Task.Delay(ReconnectDelayMs, _cancellationTokenSource.Token);
            }
        }
    }

    private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
    {
        //byte[] buffer = new byte[1024]; // Initial buffer size
        var messageBuffer = new ArraySegment<byte>(new byte[8192]); // Larger buffer for rebuilding messages
        var messageBuilder = new StringBuilder();

        while (!cancellationToken.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
        {
            try
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await _webSocket.ReceiveAsync(messageBuffer, cancellationToken);

                    // Append received data to the message builder
                    messageBuilder.Append(Encoding.UTF8.GetString(messageBuffer.Array, 0, result.Count));
                }
                while (!result.EndOfMessage); // Continue until the complete message is received

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by server", cancellationToken);
                    break;
                }

                // Process the complete message
                string message = messageBuilder.ToString();

                // Add the complete message to the queue
                _messageQueue.Enqueue(message);

                // Clear the builder for the next message
                messageBuilder.Clear();
            }
            catch
            {
                break;
            }
        }

        // Cleanup WebSocket state
        if (_webSocket != null)
        {
            _webSocket.Dispose();
            _webSocket = null;
        }
    }


    public void StopWebSocket()
    {
        _cancellationTokenSource.Cancel();

        if (_webSocket != null)
        {
            _webSocket.Dispose();
            _webSocket = null;
        }
    }

    public List<string> GetAllMessages()
    {
        List<string> messages = new List<string>();

        while (!_messageQueue.IsEmpty)
        {
            if (_messageQueue.TryDequeue(out string message))
            {
                messages.Add(message);
            }
        }

        return messages;
    }
}
