﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace shooter_server
{
    public class Player
    {
        public int Id { get; set; }

        // Конструктор для инициализации объекта Player
        public Player(int id)
        {
            Id = id;
        }

        public Player()
        {
            Id = -1;
        }

        public async Task SendMessageAsync(WebSocket webSocket, string message)
        {
            try
            {
                await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message), 0, message.Length),
                                       WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message to client: {ex.Message}");
            }
        }
    }
}