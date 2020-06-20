using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace StateFlux.Model.Repository
{
    public class PlayerRepository : IPlayerRepository
    {
        private Dictionary<Guid, Player> _players;
        private string _database = "playerdb.json";

        public PlayerRepository()
        {
        }

        public Player GetPlayerById(Guid id)
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
        public Guid InsertPlayer(Player player)
        {
            Guid result = Guid.Empty;
            lock (this)
            {
                LazyLoadPlayerDb();
                Player existingPlayer = GetPlayerById(player.Id);
                if(existingPlayer != null) {
                    throw new Exception("failed to insert player, GUID already in DB");
                }
                result = player.Id = Guid.NewGuid();
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
                    InsertPlayer(player);
                else
                {
                    p.Name = player.Name;
                    p.Bval = player.Bval;
                    p.Fval = player.Fval;
                    p.Sval = player.Sval;
                    p.SessionData = player.SessionData;
                    SaveDb();
                }
            }
        }

        private void LazyLoadPlayerDb()
        {
            if (_players == null) _players = LoadDb();
        }

        private Dictionary<Guid, Player> LoadDb()
        {
            Dictionary<Guid, Player> map = null;
            try
            {
                if (File.Exists(_database))
                {
                    map = JsonConvert.DeserializeObject<Dictionary<Guid, Player>>(File.ReadAllText(_database));
                    Console.WriteLine($"Loaded player repository from '{_database}'");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            if (map == null)
            {
                map = new Dictionary<Guid, Player>();
                Console.WriteLine("Created new player repository");
            }

            return map;
        }

        public void SaveDb()
        {
            File.WriteAllText(_database, JsonConvert.SerializeObject(_players, Formatting.Indented));
            Console.WriteLine($"Saved player database to '{_database}'");
        }
    }
}
