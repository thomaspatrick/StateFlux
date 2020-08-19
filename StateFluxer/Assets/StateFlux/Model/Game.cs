using System.Collections.Generic;
using Newtonsoft.Json;

namespace StateFlux.Model
{
    public class Game
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public int MinPlayers { get; set; }
        public int MaxPlayers { get; set; }
        [JsonIgnore]
        public List<GameInstance> Instances { get; set; }

        public Game()
        {
            Instances = new List<GameInstance>();
        }
    }
}
