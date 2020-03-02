using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using StateFlux.Model;
using StateFlux.Model.Repository;

namespace StateFlux.Service
{
    public class Server
    {
        private const string _database = "playerdb.json";

        public List<Player> Players { get; set; }
        public List<Game> Games { get; set; }
        public List<ChatSaid> Chat { get; set; }
        public IPlayerRepository playerRepository = new PlayerRepository();
        private Server()
        {
            Players = new List<Player>();
            Games = new List<Game>();
            Chat = new List<ChatSaid>();

            Game game = new Game
            {
                Name = "AssetCollapse",
                Description = "Asset Collapse Game",
                MinPlayers = 2
            };
            Games.Add(game);
        }

        public GameInstance HostGameInstance(Player hostPlayer, Game game, string gameInstanceName)
        {
            GameInstance gameInstance = new GameInstance(game, gameInstanceName);
            game.Instances.Add(gameInstance);
            hostPlayer.GameInstanceRef = new GameInstanceRef(gameInstance);
            gameInstance.HostPlayer = hostPlayer;
            JoinGameInstance(gameInstance, hostPlayer);
            return gameInstance;
        }


        public void JoinGameInstance(GameInstance gameInstance, Player player)
        {
            bool playerAlreadyIn = gameInstance.Players.Any(p => p.Id == player.Id);
            if (playerAlreadyIn)
            {
                throw new Exception($"Player {player.Name} already in game instance {gameInstance.Game.Name}:{gameInstance.Name}");
            }
            player.GameInstanceRef = new GameInstanceRef(gameInstance);
            gameInstance.Players.Add(player);
        }


        /*
        private Dictionary<string, Player> LoadPlayerDatabase()
        {
            Dictionary<string, Player> map = null;
            try
            {
                if (File.Exists(_database))
                {
                    map = JsonConvert.DeserializeObject<Dictionary<string, Player>>(File.ReadAllText(_database));
                    foreach(string key in map.Keys)
                    {
                        map[key].SessionId = key;
                    }
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e);
            }

            if(map == null)
            {
                map = new Dictionary<string, Player>();
            }

            return map;
        }

        public void SavePlayerDatabase()
        {
            File.WriteAllText(_database, JsonConvert.SerializeObject(PlayerDatabase,Formatting.Indented));
        }
        */

        static public Server Instance { get; } = new Server();
    }
}
