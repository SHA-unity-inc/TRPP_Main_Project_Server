using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace shooter_server
{
    public class Lobby
    {
        private Dictionary<WebSocket, Player> players = new Dictionary<WebSocket, Player>();

        public SqlCommander SqlCommander = new SqlCommander(
                "localhost",
                "postgres",
                "postgres",
                "TRPP_Database",
                5432
            );

        public Dictionary<WebSocket, Player> Players { get => players; }

        public async void SendMessageAll(string message)
        {
            foreach (var player in Players)
            {
                await player.Value.SendMessageAsync(player.Key, message);
            }
        }

        public async void SendMessageExcept(string message, WebSocket ws)
        {
            foreach (var player in Players)
            {
                if (player.Key != ws)
                {
                    await player.Value.SendMessageAsync(player.Key, message);
                }
            }
        }

        public async void SendMessagePlayer(string message, WebSocket ws, int idRequest)
        {
            await Players[ws].SendMessageAsync(ws, idRequest.ToString() + " " + message);
        }


        public virtual void AddPlayer(WebSocket ws, Player player)
        {
            if (Players.ContainsKey(ws))
                Players[ws] = player;
            else
                Players.Add(ws, player);
        }

        public void RemovePlayer(WebSocket ws)
        {
            if (Players.ContainsKey(ws))
                Players.Remove(ws);
        }
    }
}