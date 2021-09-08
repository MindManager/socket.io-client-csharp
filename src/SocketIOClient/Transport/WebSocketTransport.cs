﻿using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SocketIOClient.Transport
{
    public class WebSocketTransport : IReceivable, IDisposable
    {
        public WebSocketTransport(IClientWebSocket ws)
        {
            _ws = ws;
            ReceiveChunkSize = 1024 * 8;
            SendChunkSize = 1024 * 8;
            ConnectionTimeout = TimeSpan.FromSeconds(10);
            ReceiveWait = TimeSpan.FromSeconds(1);
            _listenCancellation = new CancellationTokenSource();
        }

        public int ReceiveChunkSize { get; set; }
        public int SendChunkSize { get; set; }
        public TimeSpan ConnectionTimeout { get; set; }
        public TimeSpan ReceiveWait { get; set; }
        public Action<string> OnTextReceived { get; set; }
        public Action<byte[]> OnBinaryReceived { get; set; }

        readonly IClientWebSocket _ws;
        readonly CancellationTokenSource _listenCancellation;


        /// <exception cref="WebSocketException"></exception>
        public async Task ConnectAsync(Uri uri)
        {
            var wsConnectionTokenSource = new CancellationTokenSource(ConnectionTimeout);
            await _ws.ConnectAsync(uri, wsConnectionTokenSource.Token).ConfigureAwait(false);
            _ = Task.Factory.StartNew(ListenAsync, TaskCreationOptions.LongRunning);
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken)
        {
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, cancellationToken).ConfigureAwait(false);
        }

        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="WebSocketException"></exception>
        /// <exception cref="TaskCanceledException"></exception>
        public async Task SendAsync(byte[] bytes, CancellationToken cancellationToken)
        {
            await SendAsync(WebSocketMessageType.Binary, bytes, cancellationToken);
        }

        private async Task SendAsync(WebSocketMessageType type, byte[] bytes, CancellationToken cancellationToken)
        {
            int pages = (int)Math.Ceiling(bytes.Length * 1.0 / SendChunkSize);
            for (int i = 0; i < pages; i++)
            {
                int offset = i * SendChunkSize;
                int length = SendChunkSize;
                if (offset + length > bytes.Length)
                {
                    length = bytes.Length - offset;
                }
                byte[] subBuffer = new byte[length];
                Buffer.BlockCopy(bytes, offset, subBuffer, 0, subBuffer.Length);
                bool endOfMessage = pages - 1 == i;
                await _ws.SendAsync(new ArraySegment<byte>(subBuffer), type, endOfMessage, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="WebSocketException"></exception>
        /// <exception cref="TaskCanceledException"></exception>
        public async Task SendAsync(string text, CancellationToken cancellationToken)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            await SendAsync(WebSocketMessageType.Text, bytes, cancellationToken);
        }

        private async Task ListenAsync()
        {
            while (true)
            {
                if (_listenCancellation.IsCancellationRequested)
                {
                    break;
                }
                if (_ws.State == WebSocketState.Open)
                {
                    var buffer = new byte[ReceiveChunkSize];
                    int count = 0;
                    WebSocketReceiveResult result = null;

                    while (true)
                    {
                        var subBuffer = new byte[ReceiveChunkSize];
                        try
                        {
                            result = await _ws.ReceiveAsync(new ArraySegment<byte>(subBuffer), CancellationToken.None).ConfigureAwait(false);

                            // resize
                            if (buffer.Length - count < result.Count)
                            {
                                Array.Resize(ref buffer, buffer.Length + result.Count);
                            }
                            Buffer.BlockCopy(subBuffer, 0, buffer, count, result.Count);
                            count += result.Count;
                            if (result.EndOfMessage)
                            {
                                break;
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e);
                            break;
                        }
                    }

                    if (result != null)
                    {
                        switch (result.MessageType)
                        {
                            case WebSocketMessageType.Text:
                                string text = Encoding.UTF8.GetString(buffer, 0, count);
                                OnTextReceived(text);
                                break;
                            case WebSocketMessageType.Binary:
                                OnBinaryReceived(buffer);
                                break;
                            case WebSocketMessageType.Close:
                                break;
                            default:
                                break;
                        }
                    }
                }
                else
                {
                    await Task.Delay(ReceiveWait);
                }
            }
        }

        public void Dispose()
        {
            _listenCancellation.Cancel();
            _ws.Dispose();
        }
    }
}