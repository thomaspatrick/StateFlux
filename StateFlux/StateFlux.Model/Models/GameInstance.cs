using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace StateFlux.Model
{
    public class GameInstanceRef
    {
        public GameInstanceRef()
        {
            Id = Guid.Empty;
        }

        public GameInstanceRef(GameInstance that)
        {
            Id = that.Id;
            Name = that.Name;
            GameName = that.Game.Name;
        }

        public Guid Id { get; set; }
        public string Name { get; set; }
        public string GameName { get; set; }
    }

    public enum GameInstanceState { WaitingForPlayers, Starting, InProgress, Stopping, Finished }
    public class GameInstance
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public Game Game { get; set; }

        public GameInstanceState State { get; set; }

        public List<Player> Players { get; set; }

        public Player HostPlayer { get; set; }

        public GameInstance(Game game, string name)
        {
            Players = new List<Player>();
            Game = game;
            Name = name;
            State = GameInstanceState.WaitingForPlayers;
            Id = Guid.NewGuid();
        }
    }
}
