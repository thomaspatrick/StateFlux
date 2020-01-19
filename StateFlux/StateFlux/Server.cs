using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using StateFlux.Model;

namespace StateFlux.Service
{
    public class Server
    {
        private const string _database = "playerdb.json";

        public List<Player> Players { get; set; }
        public List<Game> Games { get; set; }
        public List<ChatSaid> Chat { get; set; }
        public Dictionary<string, Player> PlayerDatabase { get; set; }
        private Server()
        {
            Players = new List<Player>();
            Games = new List<Game>();
            Chat = new List<ChatSaid>();
            PlayerDatabase = LoadPlayerDatabase();

            Game game = new Game
            {
                Name = "AssetCollapse",
                Description = "Asset Collapse Game",
                MinPlayers = 2
            };
            Games.Add(game);
        }

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

        static public Server Instance { get; } = new Server();
    }
}
