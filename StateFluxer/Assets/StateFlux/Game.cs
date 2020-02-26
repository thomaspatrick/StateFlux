using System.Collections.Generic;
using Newtonsoft.Json;

namespace StateFlux.Model
{
    public class Game
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public int MinPlayers { get; set; }
        [JsonIgnore]
        public List<GameInstance> Instances { get; set; }


        public Game()
        {
            Instances = new List<GameInstance>();
        }

        public GameInstance StartInstance(Player hostPlayer, string gameInstanceName)
        {
            GameInstance instance = new GameInstance(this,gameInstanceName);
            Instances.Add(instance);
            hostPlayer.GameInstance = instance;
            instance.HostPlayer = hostPlayer;
            instance.Join(hostPlayer);
            return instance;
        }
    }
}
