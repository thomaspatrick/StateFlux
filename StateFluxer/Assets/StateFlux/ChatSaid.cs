using System;

namespace StateFlux.Model
{
    public class ChatSaid
    {
        public DateTime DateTime { get; set; }
        public string PlayerName { get; set; }
        public string Saying { get; set; }

        public ChatSaid(string playerName, string saying)
        {
            DateTime = DateTime.Now;
            PlayerName = playerName;
            Saying = saying;
        }
    }
}
