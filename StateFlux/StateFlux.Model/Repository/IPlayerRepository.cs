using System;
using System.Collections.Generic;

namespace StateFlux.Model.Repository
{
    public interface IPlayerRepository
    {
        public Player GetPlayerById(Guid id);
        public IEnumerable<Player> GetAllPlayers();
        public Guid InsertPlayer(Player player);
        public void UpdatePlayer(Player player);
    }
}
