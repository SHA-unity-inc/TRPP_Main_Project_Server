using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.Diagnostics;

namespace shooter_server
{
    class WebSocketServerExample
    {
        private static Lobby mainLobby = new Lobby();

        static async Task Main()
        {
            Console.WriteLine("START");
            int port = 7825;
            HttpListener httpListener = new HttpListener();
            httpListener.Prefixes.Add($"http://+:{port}/");
            httpListener.Start();

            Console.WriteLine($"WebSocket Server started on port {port}");
            Console.WriteLine("Waiting for connections...");

            while (true)
            {
                var context = await httpListener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    await ProcessWebSocketRequest(context);
                }
            }
        }

        private static async Task ProcessWebSocketRequest(HttpListenerContext context)
        {
            try {
                var webSocketContext = await context.AcceptWebSocketAsync(null);
                var webSocket = webSocketContext.WebSocket;

                Console.WriteLine($"WebSocket connection established from: {context.Request.RemoteEndPoint}");

                // Добавьте нового клиента в словарь с уникальным идентификатором (может быть IP-адрес)
                mainLobby.AddPlayer(webSocket, new Player());

                // Уведомите всех клиентов о новом подключении
                await NotifyClients($"{context.Request.RemoteEndPoint} has joined.");

                byte[] buffer = new byte[1024];
                WebSocketReceiveResult result;

                do
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Console.WriteLine($"Received: {message}");

                        // Обработайте полученное сообщение здесь...

                        // Опционально отправьте ответ клиенту
                        //await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), WebSocketMessageType.Text, true, CancellationToken.None);

                        // Проверьте, является ли сообщение командой
                        if (mainLobby.Players[webSocket].isPlay.isPlayed)
                        {
                            if (message.StartsWith("/sql"))
                            {
                                message = message.Substring("/sql".Length).Trim();
                                await mainLobby.gameLobbys[mainLobby.Players[webSocket].isPlay.lobbyId].SqlCommander.ExecuteSqlCommand(mainLobby.gameLobbys[mainLobby.Players[webSocket].isPlay.lobbyId], webSocket, message, mainLobby.Players[webSocket], mainLobby.worldMap);
                            }
                        }
                        else
                        {
                            if (message.StartsWith("/sql"))
                            {
                                message = message.Substring("/sql".Length).Trim();
                                await mainLobby.SqlCommander.ExecuteSqlCommand(mainLobby, webSocket, message, mainLobby.Players[webSocket], mainLobby.worldMap);
                            }
                        }
                    }
                } while (!result.CloseStatus.HasValue || result.CloseStatus != WebSocketCloseStatus.NormalClosure);
                
                // После завершения цикла, закрываем соединение
                await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);

                // Обновите статус закрытия клиента в словаре
                mainLobby.RemovePlayer(webSocket);

                // Уведомите всех клиентов о завершении соединения
                await NotifyClients($"{context.Request.RemoteEndPoint} has left. Reason: {result.CloseStatusDescription}");

                Console.WriteLine($"WebSocket connection closed from: {context.Request.RemoteEndPoint}. Close status: {result.CloseStatus}, Reason: {result.CloseStatusDescription}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"An error occurred while reading from WebSocket: {e}");
            }
        }

        private static async Task NotifyClients(string message)
        {
            // Итерируйтесь по всем подключенным клиентам и отправляйте уведомление
            foreach (var client in mainLobby.Players.Keys)
            {
                if (client.State != WebSocketState.Open)
                    continue;
                try
                {
                    await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message), 0, message.Length),
                                           WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending message to client: {ex.Message}");
                }
            }
        }
    }
}