using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace StateFlux.Model.Repository
{
    public class PlayerRepository : IPlayerRepository
    {
        private Dictionary<string, Player> _players;
        private string _database = "playerdb.json";

        private const string _dateFormat = "yyyy-MM-ddTHH:mm:ss:ffff";

        public PlayerRepository()
        {
        }

        public Player GetPlayerById(string id)
        {
            lock(this)
            {
                LazyLoadPlayerDb();
                _players.TryGetValue(id, out Player p);
                return p;
            }
        }

        public IEnumerable<Player> GetAllPlayers()
        {
            lock (this)
            {
                LazyLoadPlayerDb();
                return _players.Values;
            }
        }
        public string InsertPlayer(Player player)
        {
            string result = null;
            lock (this)
            {
                LazyLoadPlayerDb();
                Player existingPlayer = GetPlayerById(player.Id);
                if(existingPlayer != null) {
                    throw new Exception("failed to insert player, GUID already in DB");
                }
                result = player.Id = ShortGuid.Generate();
                RemoveAllPlayersWithName(player.Name); // if there are duplicate records for this username, this one wins
                player.LastUpdated = DateTime.Now;
                _players.Add(player.Id, player);
                SaveDb();
            }
            return result;
        }
        public void UpdatePlayer(Player player)
        {
            lock (this)
            {
                LazyLoadPlayerDb();
                Player p = GetPlayerById(player.Id);
                if (p == null)
                {
                    player.LastUpdated = DateTime.Now;
                    InsertPlayer(player);
                }
                else
                {
                    p.Name = player.Name;
                    p.Bval = player.Bval;
                    p.Fval = player.Fval;
                    p.Sval = player.Sval;
                    p.LastUpdated = DateTime.Now;
                    p.SessionData = player.SessionData;
                    SaveDb();
                }
            }
        }

        private void LazyLoadPlayerDb()
        {
            if (_players == null) _players = LoadDb();
        }

        private Dictionary<string, Player> LoadDb()
        {
            Dictionary<string, Player> map = null;
            try
            {
                if (File.Exists(_database))
                {
                    map = JsonConvert.DeserializeObject<Dictionary<string, Player>>(File.ReadAllText(_database));
                    LogMessage($"Loaded player database from '{_database}'");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            if (map == null)
            {
                map = new Dictionary<string, Player>();
                LogMessage($"Created new player database");
            }

            return map;
        }

        public void SaveDb()
        {
            File.WriteAllText(_database, JsonConvert.SerializeObject(_players, Formatting.Indented));
            LogMessage($"Saved player database to '{_database}'");
        }

        private void RemoveAllPlayersWithName(string name)
        {
            var playersToRemove = _players.Values.Where(p => p.Name == name);
            foreach (var playr in playersToRemove) _players.Remove(playr.Id);
        }
        private void LogMessage(string message)
        {
            string playerName = "unknown";
            Console.WriteLine($"{DateTime.Now.ToString(_dateFormat)},{playerName},{message}");
        }

    }
}
