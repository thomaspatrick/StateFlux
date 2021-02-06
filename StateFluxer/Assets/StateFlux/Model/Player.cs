
using Newtonsoft.Json;
using System;

namespace StateFlux.Model
{
    public class PlayerSessionData
    {
        public string SessionId { get; set; }
        public string WebsocketSessionId { get; set; }
    }

    public class Player
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public Color Color { get; set; }
        public int Icon { get; set; }
        public int Score { get; set; }
        public bool[] Bval { get; set; }
        public float[] Fval { get; set; }
        public string[] Sval { get; set; }
        public DateTime? LastUpdated { get; set; }
        public GameInstanceRef GameInstanceRef { get; set; }
        public PlayerSessionData SessionData { get; set; }
    }

}
