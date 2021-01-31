using System;
using System.Collections.Generic;

namespace StateFlux.Model.Repository
{
    public interface IPlayerRepository
    {
        public Player GetPlayerById(string id);
        public IEnumerable<Player> GetAllPlayers();
        public string InsertPlayer(Player player);
        public void UpdatePlayer(Player player);
    }
}
