using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace StateFlux.Model
{
    public class GameInstance
    {
        public string Name { get; set; }

        public Game Game { get; set; }

        [JsonIgnore]
        public List<Player> Players { get; set; }

        [JsonIgnore]
        public Player HostPlayer { get; set; }

        public GameInstance(Game game, string name)
        {
            Players = new List<Player>();
            Game = game;
            Name = name;
        }

        public void Join(Player player)
        {
            bool playerAlreadyIn = Players.Any(p => p.SessionId == player.SessionId);
            if (playerAlreadyIn)
            {
                throw new Exception($"Player {player.Name} already in game instance {Game.Name}:{Name}");
            }

            player.GameInstance = this;
            Players.Add(player);
        }
    }
}
