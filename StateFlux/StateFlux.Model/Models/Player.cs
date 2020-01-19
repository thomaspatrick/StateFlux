
using Newtonsoft.Json;

namespace StateFlux.Model
{
    public class Player
    {
        public string Name { get; set; }
        public GameInstance GameInstance { get; set; }
        public bool[] Bval { get; set; }
        public float[] Fval { get; set; }
        public string[] Sval { get; set; }
        [JsonIgnore]
        public string SessionId { get; set; }
        [JsonIgnore]
        public string WebsocketSessionId { get; set; }
    }

}
